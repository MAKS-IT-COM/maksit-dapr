using MaksIT.Results;


namespace MaksIT.Dapr.Services.WorkLease;

/// <summary>
/// HA work coordination via Dapr state (broker-agnostic).
/// Keys are product-defined; store name comes from the Dapr Component or <see cref="IDaprWorkLeaseOptions"/>.
/// </summary>
public interface IDaprWorkLeaseService {
  /// <summary>
  /// Tries to acquire or take over an expired lease for <paramref name="holderId"/>.
  /// </summary>
  Task<Result<bool>> TryAcquireAsync(string storeName, string workKey, string holderId, TimeSpan ttl, CancellationToken cancellationToken = default);

  /// <summary>
  /// Extends an existing lease when <paramref name="holderId"/> still holds it.
  /// </summary>
  Task<Result<bool>> TryRenewAsync(string storeName, string workKey, string holderId, TimeSpan ttl, CancellationToken cancellationToken = default);

  /// <summary>
  /// Releases the lease when held by <paramref name="holderId"/>.
  /// </summary>
  Task<Result> ReleaseAsync(string storeName, string workKey, string holderId, CancellationToken cancellationToken = default);

  /// <summary>
  /// Returns the current lease document, or <c>null</c> when missing.
  /// </summary>
  Task<Result<DaprWorkLease?>> GetAsync(string storeName, string workKey, CancellationToken cancellationToken = default);

  /// <summary>
  /// Acquires a scoped hold with optional auto-renew. <c>Ok(null)</c> when not acquired; unsuccessful on infra errors.
  /// </summary>
  Task<Result<DaprWorkLeaseHold?>> TryHoldAsync(
    string storeName,
    string workKey,
    string holderId,
    TimeSpan ttl,
    bool autoRenew = true,
    CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IDaprWorkLeaseService"/> using ETag concurrency on <see cref="IDaprStateStoreService"/>.
/// </summary>
public sealed class DaprWorkLeaseService(
  IDaprStateStoreService stateStore
) : IDaprWorkLeaseService {

  /// <inheritdoc />
  public async Task<Result<bool>> TryAcquireAsync(
    string storeName,
    string workKey,
    string holderId,
    TimeSpan ttl,
    CancellationToken cancellationToken = default) {
    var validation = Validate(storeName, workKey, holderId, ttl);
    if (!validation.IsSuccess)
      return validation.ToResultOfType<bool>(false);

    var existing = await stateStore.GetStateAndETagAsync<DaprWorkLease>(
      storeName,
      workKey,
      cancellationToken: cancellationToken).ConfigureAwait(false);
    if (!existing.IsSuccess)
      return existing.ToResultOfType<bool>(false);

    var (lease, etag) = existing.Value;
    var now = DateTimeOffset.UtcNow;

    if (lease is not null && lease.ExpiresAtUtc > now && !string.Equals(lease.HolderId, holderId, StringComparison.Ordinal))
      return Result<bool>.Ok(false);

    var generation = lease is null
      ? 1L
      : string.Equals(lease.HolderId, holderId, StringComparison.Ordinal)
        ? lease.Generation
        : lease.Generation + 1;

    var next = new DaprWorkLease(holderId, now, now.Add(ttl), generation);
    var saved = await stateStore.TrySaveStateAsync(
      storeName,
      workKey,
      next,
      etag,
      cancellationToken: cancellationToken).ConfigureAwait(false);
    if (!saved.IsSuccess)
      return saved;

    return Result<bool>.Ok(saved.Value);
  }

  /// <inheritdoc />
  public async Task<Result<bool>> TryRenewAsync(
    string storeName,
    string workKey,
    string holderId,
    TimeSpan ttl,
    CancellationToken cancellationToken = default) {
    var validation = Validate(storeName, workKey, holderId, ttl);
    if (!validation.IsSuccess)
      return validation.ToResultOfType<bool>(false);

    var existing = await stateStore.GetStateAndETagAsync<DaprWorkLease>(
      storeName,
      workKey,
      cancellationToken: cancellationToken).ConfigureAwait(false);
    if (!existing.IsSuccess)
      return existing.ToResultOfType<bool>(false);

    var (lease, etag) = existing.Value;
    if (lease is null || !string.Equals(lease.HolderId, holderId, StringComparison.Ordinal))
      return Result<bool>.Ok(false);

    var now = DateTimeOffset.UtcNow;
    var next = lease with { ExpiresAtUtc = now.Add(ttl) };
    var saved = await stateStore.TrySaveStateAsync(
      storeName,
      workKey,
      next,
      etag,
      cancellationToken: cancellationToken).ConfigureAwait(false);
    if (!saved.IsSuccess)
      return saved;

    return Result<bool>.Ok(saved.Value);
  }

  /// <inheritdoc />
  public async Task<Result> ReleaseAsync(
    string storeName,
    string workKey,
    string holderId,
    CancellationToken cancellationToken = default) {
    if (string.IsNullOrWhiteSpace(storeName) || string.IsNullOrWhiteSpace(workKey) || string.IsNullOrWhiteSpace(holderId))
      return Result.BadRequest("storeName, workKey, and holderId are required.");

    var existing = await stateStore.GetStateAndETagAsync<DaprWorkLease>(
      storeName,
      workKey,
      cancellationToken: cancellationToken).ConfigureAwait(false);
    if (!existing.IsSuccess)
      return existing.ToResult();

    var (lease, _) = existing.Value;
    if (lease is null)
      return Result.Ok();

    if (!string.Equals(lease.HolderId, holderId, StringComparison.Ordinal))
      return Result.Conflict("Lease is held by another instance.");

    return await stateStore.DeleteStateAsync(storeName, workKey, cancellationToken: cancellationToken).ConfigureAwait(false);
  }

  /// <inheritdoc />
  public async Task<Result<DaprWorkLease?>> GetAsync(
    string storeName,
    string workKey,
    CancellationToken cancellationToken = default) {
    if (string.IsNullOrWhiteSpace(storeName) || string.IsNullOrWhiteSpace(workKey))
      return Result<DaprWorkLease?>.BadRequest(default, "storeName and workKey are required.");

    var existing = await stateStore.GetStateAndETagAsync<DaprWorkLease>(
      storeName,
      workKey,
      cancellationToken: cancellationToken).ConfigureAwait(false);
    if (!existing.IsSuccess)
      return existing.ToResultOfType((DaprWorkLease?)null);

    return Result<DaprWorkLease?>.Ok(existing.Value.Value);
  }

  /// <inheritdoc />
  public async Task<Result<DaprWorkLeaseHold?>> TryHoldAsync(
    string storeName,
    string workKey,
    string holderId,
    TimeSpan ttl,
    bool autoRenew = true,
    CancellationToken cancellationToken = default) {
    var acquired = await TryAcquireAsync(storeName, workKey, holderId, ttl, cancellationToken).ConfigureAwait(false);
    if (!acquired.IsSuccess)
      return acquired.ToResultOfType((DaprWorkLeaseHold?)null);
    if (!acquired.Value)
      return Result<DaprWorkLeaseHold?>.Ok(null);

    var current = await GetAsync(storeName, workKey, cancellationToken).ConfigureAwait(false);
    if (!current.IsSuccess)
      return current.ToResultOfType((DaprWorkLeaseHold?)null);

    var generation = current.Value?.Generation ?? 0;
    return Result<DaprWorkLeaseHold?>.Ok(new DaprWorkLeaseHold(this, storeName, workKey, holderId, ttl, generation, autoRenew));
  }

  private static Result Validate(string storeName, string workKey, string holderId, TimeSpan ttl) {
    if (string.IsNullOrWhiteSpace(storeName) || string.IsNullOrWhiteSpace(workKey) || string.IsNullOrWhiteSpace(holderId))
      return Result.BadRequest("storeName, workKey, and holderId are required.");
    if (ttl <= TimeSpan.Zero)
      return Result.BadRequest("ttl must be positive.");
    return Result.Ok();
  }
}
