using MaksIT.Results;


namespace MaksIT.Dapr.Services;

public sealed record DaprWorkLease(
  string HolderId,
  DateTimeOffset AcquiredAtUtc,
  DateTimeOffset ExpiresAtUtc
);

/// <summary>
/// HA work coordination via Dapr state store (broker-agnostic).
/// Keys are product-defined; store name comes from the Dapr Component.
/// </summary>
public interface IDaprWorkLeaseStore {
  Task<Result<bool>> TryAcquireAsync(string storeName, string workKey, string holderId, TimeSpan ttl, CancellationToken cancellationToken = default);
  Task<Result<bool>> TryRenewAsync(string storeName, string workKey, string holderId, TimeSpan ttl, CancellationToken cancellationToken = default);
  Task<Result> ReleaseAsync(string storeName, string workKey, string holderId, CancellationToken cancellationToken = default);
  Task<Result<DaprWorkLease?>> GetAsync(string storeName, string workKey, CancellationToken cancellationToken = default);
}

public sealed class DaprWorkLeaseStore(
  IDaprStateStoreService stateStore
) : IDaprWorkLeaseStore {

  public async Task<Result<bool>> TryAcquireAsync(
    string storeName,
    string workKey,
    string holderId,
    TimeSpan ttl,
    CancellationToken cancellationToken = default) {
    var validation = Validate(storeName, workKey, holderId, ttl);
    if (!validation.IsSuccess)
      return validation.ToResultOfType<bool>(false);

    var existing = await stateStore.GetStateAndETagAsync<DaprWorkLease>(storeName, workKey, cancellationToken).ConfigureAwait(false);
    if (!existing.IsSuccess)
      return existing.ToResult().ToResultOfType<bool>(false);

    var (lease, etag) = existing.Value;
    var now = DateTimeOffset.UtcNow;

    if (lease is not null && lease.ExpiresAtUtc > now && !string.Equals(lease.HolderId, holderId, StringComparison.Ordinal))
      return Result<bool>.Ok(false);

    var next = new DaprWorkLease(holderId, now, now.Add(ttl));
    // First write: etag may be null/empty when key missing.
    var saved = await stateStore.TrySaveStateAsync(storeName, workKey, next, etag, cancellationToken).ConfigureAwait(false);
    if (!saved.IsSuccess)
      return saved;

    return Result<bool>.Ok(saved.Value);
  }

  public async Task<Result<bool>> TryRenewAsync(
    string storeName,
    string workKey,
    string holderId,
    TimeSpan ttl,
    CancellationToken cancellationToken = default) {
    var validation = Validate(storeName, workKey, holderId, ttl);
    if (!validation.IsSuccess)
      return validation.ToResultOfType<bool>(false);

    var existing = await stateStore.GetStateAndETagAsync<DaprWorkLease>(storeName, workKey, cancellationToken).ConfigureAwait(false);
    if (!existing.IsSuccess)
      return existing.ToResult().ToResultOfType<bool>(false);

    var (lease, etag) = existing.Value;
    if (lease is null || !string.Equals(lease.HolderId, holderId, StringComparison.Ordinal))
      return Result<bool>.Ok(false);

    var now = DateTimeOffset.UtcNow;
    var next = lease with { ExpiresAtUtc = now.Add(ttl) };
    var saved = await stateStore.TrySaveStateAsync(storeName, workKey, next, etag, cancellationToken).ConfigureAwait(false);
    if (!saved.IsSuccess)
      return saved;

    return Result<bool>.Ok(saved.Value);
  }

  public async Task<Result> ReleaseAsync(
    string storeName,
    string workKey,
    string holderId,
    CancellationToken cancellationToken = default) {
    if (string.IsNullOrWhiteSpace(storeName) || string.IsNullOrWhiteSpace(workKey) || string.IsNullOrWhiteSpace(holderId))
      return Result.BadRequest("storeName, workKey, and holderId are required.");

    var existing = await stateStore.GetStateAndETagAsync<DaprWorkLease>(storeName, workKey, cancellationToken).ConfigureAwait(false);
    if (!existing.IsSuccess)
      return existing.ToResult();

    var (lease, _) = existing.Value;
    if (lease is null)
      return Result.Ok();

    if (!string.Equals(lease.HolderId, holderId, StringComparison.Ordinal))
      return Result.Conflict("Lease is held by another instance.");

    return await stateStore.DeleteStateAsync(storeName, workKey).ConfigureAwait(false);
  }

  public async Task<Result<DaprWorkLease?>> GetAsync(
    string storeName,
    string workKey,
    CancellationToken cancellationToken = default) {
    if (string.IsNullOrWhiteSpace(storeName) || string.IsNullOrWhiteSpace(workKey))
      return Result<DaprWorkLease?>.BadRequest(null, "storeName and workKey are required.");

    var existing = await stateStore.GetStateAndETagAsync<DaprWorkLease>(storeName, workKey, cancellationToken).ConfigureAwait(false);
    if (!existing.IsSuccess)
      return existing.ToResult().ToResultOfType<DaprWorkLease?>(null);

    return Result<DaprWorkLease?>.Ok(existing.Value.Value);
  }

  private static Result Validate(string storeName, string workKey, string holderId, TimeSpan ttl) {
    if (string.IsNullOrWhiteSpace(storeName) || string.IsNullOrWhiteSpace(workKey) || string.IsNullOrWhiteSpace(holderId))
      return Result.BadRequest("storeName, workKey, and holderId are required.");
    if (ttl <= TimeSpan.Zero)
      return Result.BadRequest("ttl must be positive.");
    return Result.Ok();
  }
}
