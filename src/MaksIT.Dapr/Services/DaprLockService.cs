#pragma warning disable DAPR_DISTRIBUTEDLOCK

using Microsoft.Extensions.Logging;
using Dapr.Client;
using MaksIT.Results;
using MaksIT.Core.Extensions;
using MaksIT.Dapr.Services.WorkLease;


namespace MaksIT.Dapr.Services;

/// <summary>
/// Dapr distributed lock API with <see cref="Result"/> outcomes.
/// </summary>
/// <remarks>
/// Short-lived mutex over a Dapr lock Component. For MaksIT multi-replica leader/bootstrap/sweep
/// coordination prefer <see cref="IDaprWorkLeaseService"/> (state-backed named leases with renew/hold helpers).
/// </remarks>
public interface IDaprLockService {
  /// <summary>
  /// Attempts to acquire a lock. Check <see cref="TryLockResponse.Success"/>; dispose the response when done (or call <see cref="UnlockAsync"/>).
  /// </summary>
  Task<Result<TryLockResponse>> LockAsync(
    string storeName,
    string resourceId,
    string lockOwner,
    int expiryInSeconds,
    CancellationToken cancellationToken = default);

  /// <summary>
  /// Releases a lock held by <paramref name="lockOwner"/>.
  /// </summary>
  Task<Result<UnlockResponse>> UnlockAsync(
    string storeName,
    string resourceId,
    string lockOwner,
    CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IDaprLockService"/>.
/// </summary>
public class DaprLockService(
  ILogger<DaprLockService> logger,
  DaprClient client
) : IDaprLockService {
  private const string ErrorMessage = "MaksIT.Dapr - Lock error";

  /// <inheritdoc />
  public async Task<Result<TryLockResponse>> LockAsync(
    string storeName,
    string resourceId,
    string lockOwner,
    int expiryInSeconds,
    CancellationToken cancellationToken = default) {
    if (string.IsNullOrWhiteSpace(storeName) || string.IsNullOrWhiteSpace(resourceId) || string.IsNullOrWhiteSpace(lockOwner))
      return Result<TryLockResponse>.BadRequest(default!, "storeName, resourceId, and lockOwner are required.");
    if (expiryInSeconds <= 0)
      return Result<TryLockResponse>.BadRequest(default!, "expiryInSeconds must be positive.");

    try {
      var response = await client.Lock(storeName, resourceId, lockOwner, expiryInSeconds, cancellationToken);
      return Result<TryLockResponse>.Ok(response);
    }
    catch (OperationCanceledException) {
      throw;
    }
    catch (Exception ex) {
      logger.LogError(ex, ErrorMessage);
      return Result<TryLockResponse>.InternalServerError(default!, [ErrorMessage, .. ex.ExtractMessages()]);
    }
  }

  /// <inheritdoc />
  public async Task<Result<UnlockResponse>> UnlockAsync(
    string storeName,
    string resourceId,
    string lockOwner,
    CancellationToken cancellationToken = default) {
    if (string.IsNullOrWhiteSpace(storeName) || string.IsNullOrWhiteSpace(resourceId) || string.IsNullOrWhiteSpace(lockOwner))
      return Result<UnlockResponse>.BadRequest(default!, "storeName, resourceId, and lockOwner are required.");

    try {
      var response = await client.Unlock(storeName, resourceId, lockOwner, cancellationToken);
      return Result<UnlockResponse>.Ok(response);
    }
    catch (OperationCanceledException) {
      throw;
    }
    catch (Exception ex) {
      logger.LogError(ex, ErrorMessage);
      return Result<UnlockResponse>.InternalServerError(default!, [ErrorMessage, .. ex.ExtractMessages()]);
    }
  }
}

#pragma warning restore DAPR_DISTRIBUTEDLOCK
