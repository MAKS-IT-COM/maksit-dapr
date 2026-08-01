using Microsoft.Extensions.Logging;
using MaksIT.Results;


namespace MaksIT.Dapr.Services.WorkLease;

/// <summary>
/// Scoped lease hold: releases on dispose; optional background renew at ~½ TTL.
/// Exposes <see cref="Generation"/> for fencing long exclusive work.
/// </summary>
public sealed class DaprWorkLeaseHold : IAsyncDisposable {
  private readonly IDaprWorkLeaseService _leases;
  private readonly string _storeName;
  private readonly string _workKey;
  private readonly string _holderId;
  private readonly TimeSpan _ttl;
  private readonly CancellationTokenSource _renewCts = new();
  private readonly Task? _renewLoop;
  private int _disposed;

  /// <summary>
  /// Creates a hold after a successful acquire.
  /// </summary>
  public DaprWorkLeaseHold(
    IDaprWorkLeaseService leases,
    string storeName,
    string workKey,
    string holderId,
    TimeSpan ttl,
    long generation,
    bool autoRenew) {
    _leases = leases;
    _storeName = storeName;
    _workKey = workKey;
    _holderId = holderId;
    _ttl = ttl;
    Generation = generation;

    if (autoRenew && ttl > TimeSpan.Zero)
      _renewLoop = RunRenewLoopAsync(_renewCts.Token);
  }

  /// <summary>
  /// Fencing generation observed at acquire time.
  /// </summary>
  public long Generation { get; }

  /// <summary>
  /// Renews once. Returns <c>Ok(false)</c> when the lease is no longer held by this holder.
  /// </summary>
  public Task<Result<bool>> RenewAsync(CancellationToken cancellationToken = default) =>
    _leases.TryRenewAsync(_storeName, _workKey, _holderId, _ttl, cancellationToken);

  /// <summary>
  /// Returns <c>Ok(true)</c> when still held by this holder at the same generation; otherwise <c>Ok(false)</c> or Conflict.
  /// </summary>
  public async Task<Result<bool>> EnsureStillHeldAsync(CancellationToken cancellationToken = default) {
    var current = await _leases.GetAsync(_storeName, _workKey, cancellationToken).ConfigureAwait(false);
    if (!current.IsSuccess)
      return current.ToResultOfType<bool>(false);

    var lease = current.Value;
    if (lease is null)
      return Result<bool>.Ok(false);

    if (!string.Equals(lease.HolderId, _holderId, StringComparison.Ordinal) || lease.Generation != Generation)
      return Result<bool>.Conflict(false, "Lease generation or holder changed.");

    if (lease.ExpiresAtUtc <= DateTimeOffset.UtcNow)
      return Result<bool>.Ok(false);

    return Result<bool>.Ok(true);
  }

  /// <inheritdoc />
  public async ValueTask DisposeAsync() {
    if (Interlocked.Exchange(ref _disposed, 1) != 0)
      return;

    try {
      _renewCts.Cancel();
    }
    catch (ObjectDisposedException) {
      // ignore
    }

    if (_renewLoop is not null) {
      try {
        await _renewLoop.ConfigureAwait(false);
      }
      catch (OperationCanceledException) {
        // expected
      }
    }

    _renewCts.Dispose();
    await _leases.ReleaseAsync(_storeName, _workKey, _holderId, CancellationToken.None).ConfigureAwait(false);
  }

  private async Task RunRenewLoopAsync(CancellationToken cancellationToken) {
    var delay = TimeSpan.FromTicks(Math.Max(_ttl.Ticks / 2, TimeSpan.FromSeconds(1).Ticks));
    while (!cancellationToken.IsCancellationRequested) {
      try {
        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        var renewed = await RenewAsync(cancellationToken).ConfigureAwait(false);
        if (!renewed.IsSuccess || !renewed.Value)
          break;
      }
      catch (OperationCanceledException) {
        break;
      }
    }
  }
}

/// <summary>
/// Bootstrap helper: one replica runs work under a lease; others wait until ready.
/// </summary>
public static class DaprWorkLeaseBootstrap {
  /// <summary>
  /// Leader acquires the lease and runs <paramref name="bootstrap"/>; followers poll <paramref name="isReady"/> until true or cancel.
  /// </summary>
  public static async Task<Result> RunBootstrapUnderLeaseAsync(
    IDaprWorkLeaseService leases,
    string storeName,
    string workKey,
    string holderId,
    TimeSpan ttl,
    Func<CancellationToken, Task<Result>> bootstrap,
    Func<CancellationToken, Task<Result<bool>>> isReady,
    TimeSpan? followerPollInterval = null,
    CancellationToken cancellationToken = default) {
    if (bootstrap is null)
      return Result.BadRequest("bootstrap is required.");
    if (isReady is null)
      return Result.BadRequest("isReady is required.");

    var hold = await leases.TryHoldAsync(storeName, workKey, holderId, ttl, autoRenew: true, cancellationToken).ConfigureAwait(false);
    if (!hold.IsSuccess)
      return hold.ToResult();

    if (hold.Value is not null) {
      await using (hold.Value.ConfigureAwait(false)) {
        return await bootstrap(cancellationToken).ConfigureAwait(false);
      }
    }

    var poll = followerPollInterval ?? TimeSpan.FromSeconds(2);
    while (!cancellationToken.IsCancellationRequested) {
      var ready = await isReady(cancellationToken).ConfigureAwait(false);
      if (!ready.IsSuccess)
        return ready.ToResult();
      if (ready.Value)
        return Result.Ok();

      await Task.Delay(poll, cancellationToken).ConfigureAwait(false);
    }

    throw new OperationCanceledException(cancellationToken);
  }
}

/// <summary>
/// Background loop that runs exclusive work only while a named work lease is held.
/// Failed work <see cref="Result"/> values are logged; the host is not crashed.
/// </summary>
public abstract class LeasedBackgroundService(
  IDaprWorkLeaseService leases,
  IDaprRuntimeInstanceId runtimeInstance,
  IDaprWorkLeaseOptions options,
  ILogger logger
) : Microsoft.Extensions.Hosting.BackgroundService {
  /// <summary>
  /// Product lease key (not the Dapr store name).
  /// </summary>
  protected abstract string WorkKey { get; }

  /// <summary>
  /// Lease TTL while work runs.
  /// </summary>
  protected virtual TimeSpan LeaseTtl => TimeSpan.FromMinutes(1);

  /// <summary>
  /// Delay after a successful work cycle.
  /// </summary>
  protected virtual TimeSpan IdleDelay => TimeSpan.FromSeconds(30);

  /// <summary>
  /// Delay when the lease is busy / not acquired.
  /// </summary>
  protected virtual TimeSpan BusyBackoff => TimeSpan.FromSeconds(5);

  /// <summary>
  /// Exclusive work while the lease is held. Return unsuccessful <see cref="Result"/> to log and continue.
  /// </summary>
  protected abstract Task<Result> ExecuteLeasedAsync(DaprWorkLeaseHold hold, CancellationToken stoppingToken);

  /// <inheritdoc />
  protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
    while (!stoppingToken.IsCancellationRequested) {
      try {
        var holdResult = await leases.TryHoldAsync(
          options.StoreName,
          WorkKey,
          runtimeInstance.InstanceId,
          LeaseTtl,
          autoRenew: true,
          stoppingToken).ConfigureAwait(false);

        if (!holdResult.IsSuccess) {
          logger.LogError("LeasedBackgroundService acquire failed for {WorkKey}: {Messages}", WorkKey, string.Join("; ", holdResult.Messages));
          await Task.Delay(BusyBackoff, stoppingToken).ConfigureAwait(false);
          continue;
        }

        if (holdResult.Value is null) {
          await Task.Delay(BusyBackoff, stoppingToken).ConfigureAwait(false);
          continue;
        }

        await using (holdResult.Value.ConfigureAwait(false)) {
          var work = await ExecuteLeasedAsync(holdResult.Value, stoppingToken).ConfigureAwait(false);
          if (!work.IsSuccess)
            logger.LogWarning("LeasedBackgroundService work failed for {WorkKey}: {Messages}", WorkKey, string.Join("; ", work.Messages));
        }

        await Task.Delay(IdleDelay, stoppingToken).ConfigureAwait(false);
      }
      catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
        break;
      }
    }
  }
}
