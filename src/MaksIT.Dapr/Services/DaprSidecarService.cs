using Microsoft.Extensions.Logging;
using Dapr.Client;
using MaksIT.Results;
using MaksIT.Core.Extensions;


namespace MaksIT.Dapr.Services;

/// <summary>
/// Dapr sidecar health and metadata with <see cref="Result"/> outcomes.
/// </summary>
public interface IDaprSidecarService {
  /// <summary>
  /// Checks sidecar health.
  /// </summary>
  Task<Result<bool>> CheckHealthAsync(CancellationToken cancellationToken = default);

  /// <summary>
  /// Checks outbound health.
  /// </summary>
  Task<Result<bool>> CheckOutboundHealthAsync(CancellationToken cancellationToken = default);

  /// <summary>
  /// Blocks until the sidecar is ready.
  /// </summary>
  Task<Result> WaitForSidecarAsync(CancellationToken cancellationToken = default);

  /// <summary>
  /// Gets sidecar metadata.
  /// </summary>
  Task<Result<DaprMetadata>> GetMetadataAsync(CancellationToken cancellationToken = default);

  /// <summary>
  /// Requests sidecar shutdown.
  /// </summary>
  Task<Result> ShutdownAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IDaprSidecarService"/>.
/// </summary>
public class DaprSidecarService(
  ILogger<DaprSidecarService> logger,
  DaprClient client
) : IDaprSidecarService {
  private const string ErrorMessage = "MaksIT.Dapr - Sidecar error";

  /// <inheritdoc />
  public async Task<Result<bool>> CheckHealthAsync(CancellationToken cancellationToken = default) {
    try {
      var healthy = await client.CheckHealthAsync(cancellationToken);
      return Result<bool>.Ok(healthy);
    }
    catch (OperationCanceledException) {
      throw;
    }
    catch (Exception ex) {
      logger.LogError(ex, ErrorMessage);
      return Result<bool>.InternalServerError(false, [ErrorMessage, .. ex.ExtractMessages()]);
    }
  }

  /// <inheritdoc />
  public async Task<Result<bool>> CheckOutboundHealthAsync(CancellationToken cancellationToken = default) {
    try {
      var healthy = await client.CheckOutboundHealthAsync(cancellationToken);
      return Result<bool>.Ok(healthy);
    }
    catch (OperationCanceledException) {
      throw;
    }
    catch (Exception ex) {
      logger.LogError(ex, ErrorMessage);
      return Result<bool>.InternalServerError(false, [ErrorMessage, .. ex.ExtractMessages()]);
    }
  }

  /// <inheritdoc />
  public async Task<Result> WaitForSidecarAsync(CancellationToken cancellationToken = default) {
    try {
      await client.WaitForSidecarAsync(cancellationToken);
      return Result.Ok();
    }
    catch (OperationCanceledException) {
      throw;
    }
    catch (Exception ex) {
      logger.LogError(ex, ErrorMessage);
      return Result.InternalServerError([ErrorMessage, .. ex.ExtractMessages()]);
    }
  }

  /// <inheritdoc />
  public async Task<Result<DaprMetadata>> GetMetadataAsync(CancellationToken cancellationToken = default) {
    try {
      var metadata = await client.GetMetadataAsync(cancellationToken);
      return Result<DaprMetadata>.Ok(metadata);
    }
    catch (OperationCanceledException) {
      throw;
    }
    catch (Exception ex) {
      logger.LogError(ex, ErrorMessage);
      return Result<DaprMetadata>.InternalServerError(default!, [ErrorMessage, .. ex.ExtractMessages()]);
    }
  }

  /// <inheritdoc />
  public async Task<Result> ShutdownAsync(CancellationToken cancellationToken = default) {
    try {
      await client.ShutdownSidecarAsync(cancellationToken);
      return Result.Ok();
    }
    catch (OperationCanceledException) {
      throw;
    }
    catch (Exception ex) {
      logger.LogError(ex, ErrorMessage);
      return Result.InternalServerError([ErrorMessage, .. ex.ExtractMessages()]);
    }
  }
}
