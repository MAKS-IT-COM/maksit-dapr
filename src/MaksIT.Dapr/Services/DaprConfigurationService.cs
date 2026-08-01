using Microsoft.Extensions.Logging;
using Dapr.Client;
using MaksIT.Results;
using MaksIT.Core.Extensions;


namespace MaksIT.Dapr.Services;

/// <summary>
/// Dapr configuration API with <see cref="Result"/> outcomes.
/// </summary>
public interface IDaprConfigurationService {
  /// <summary>
  /// Gets configuration items.
  /// </summary>
  Task<Result<GetConfigurationResponse>> GetAsync(
    string storeName,
    IReadOnlyList<string> keys,
    IReadOnlyDictionary<string, string>? metadata = null,
    CancellationToken cancellationToken = default);

  /// <summary>
  /// Subscribes to configuration changes.
  /// </summary>
  Task<Result<SubscribeConfigurationResponse>> SubscribeAsync(
    string storeName,
    IReadOnlyList<string> keys,
    IReadOnlyDictionary<string, string>? metadata = null,
    CancellationToken cancellationToken = default);

  /// <summary>
  /// Unsubscribes from configuration changes.
  /// </summary>
  Task<Result> UnsubscribeAsync(string storeName, string id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IDaprConfigurationService"/>.
/// </summary>
public class DaprConfigurationService(
  ILogger<DaprConfigurationService> logger,
  DaprClient client
) : IDaprConfigurationService {
  private const string ErrorMessage = "MaksIT.Dapr - Configuration error";

  /// <inheritdoc />
  public async Task<Result<GetConfigurationResponse>> GetAsync(
    string storeName,
    IReadOnlyList<string> keys,
    IReadOnlyDictionary<string, string>? metadata = null,
    CancellationToken cancellationToken = default) {
    if (string.IsNullOrWhiteSpace(storeName) || keys is null || keys.Count == 0)
      return Result<GetConfigurationResponse>.BadRequest(default!, "storeName and keys are required.");

    try {
      var response = await client.GetConfiguration(storeName, keys, metadata, cancellationToken);
      return Result<GetConfigurationResponse>.Ok(response);
    }
    catch (OperationCanceledException) {
      throw;
    }
    catch (Exception ex) {
      logger.LogError(ex, ErrorMessage);
      return Result<GetConfigurationResponse>.InternalServerError(default!, [ErrorMessage, .. ex.ExtractMessages()]);
    }
  }

  /// <inheritdoc />
  public async Task<Result<SubscribeConfigurationResponse>> SubscribeAsync(
    string storeName,
    IReadOnlyList<string> keys,
    IReadOnlyDictionary<string, string>? metadata = null,
    CancellationToken cancellationToken = default) {
    if (string.IsNullOrWhiteSpace(storeName) || keys is null || keys.Count == 0)
      return Result<SubscribeConfigurationResponse>.BadRequest(default!, "storeName and keys are required.");

    try {
      var response = await client.SubscribeConfiguration(storeName, keys, metadata, cancellationToken);
      return Result<SubscribeConfigurationResponse>.Ok(response);
    }
    catch (OperationCanceledException) {
      throw;
    }
    catch (Exception ex) {
      logger.LogError(ex, ErrorMessage);
      return Result<SubscribeConfigurationResponse>.InternalServerError(default!, [ErrorMessage, .. ex.ExtractMessages()]);
    }
  }

  /// <inheritdoc />
  public async Task<Result> UnsubscribeAsync(string storeName, string id, CancellationToken cancellationToken = default) {
    if (string.IsNullOrWhiteSpace(storeName) || string.IsNullOrWhiteSpace(id))
      return Result.BadRequest("storeName and id are required.");

    try {
      _ = await client.UnsubscribeConfiguration(storeName, id, cancellationToken);
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
