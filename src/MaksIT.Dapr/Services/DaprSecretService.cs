using Microsoft.Extensions.Logging;
using Dapr.Client;
using MaksIT.Results;
using MaksIT.Core.Extensions;


namespace MaksIT.Dapr.Services;

/// <summary>
/// Dapr secret store operations with <see cref="Result"/> outcomes.
/// </summary>
public interface IDaprSecretService {
  /// <summary>
  /// Gets a secret by name.
  /// </summary>
  Task<Result<IReadOnlyDictionary<string, string>>> GetAsync(
    string storeName,
    string key,
    IReadOnlyDictionary<string, string>? metadata = null,
    CancellationToken cancellationToken = default);

  /// <summary>
  /// Gets all secrets from a store.
  /// </summary>
  Task<Result<IReadOnlyDictionary<string, Dictionary<string, string>>>> GetBulkAsync(
    string storeName,
    IReadOnlyDictionary<string, string>? metadata = null,
    CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IDaprSecretService"/>.
/// </summary>
public class DaprSecretService(
  ILogger<DaprSecretService> logger,
  DaprClient client
) : IDaprSecretService {
  private const string ErrorMessage = "MaksIT.Dapr - Secret error";

  /// <inheritdoc />
  public async Task<Result<IReadOnlyDictionary<string, string>>> GetAsync(
    string storeName,
    string key,
    IReadOnlyDictionary<string, string>? metadata = null,
    CancellationToken cancellationToken = default) {
    if (string.IsNullOrWhiteSpace(storeName) || string.IsNullOrWhiteSpace(key))
      return Result<IReadOnlyDictionary<string, string>>.BadRequest(default!, "storeName and key are required.");

    try {
      var secrets = await client.GetSecretAsync(storeName, key, metadata, cancellationToken);
      return Result<IReadOnlyDictionary<string, string>>.Ok(secrets);
    }
    catch (OperationCanceledException) {
      throw;
    }
    catch (Exception ex) {
      logger.LogError(ex, ErrorMessage);
      return Result<IReadOnlyDictionary<string, string>>.InternalServerError(default!, [ErrorMessage, .. ex.ExtractMessages()]);
    }
  }

  /// <inheritdoc />
  public async Task<Result<IReadOnlyDictionary<string, Dictionary<string, string>>>> GetBulkAsync(
    string storeName,
    IReadOnlyDictionary<string, string>? metadata = null,
    CancellationToken cancellationToken = default) {
    if (string.IsNullOrWhiteSpace(storeName))
      return Result<IReadOnlyDictionary<string, Dictionary<string, string>>>.BadRequest(default!, "storeName is required.");

    try {
      var secrets = await client.GetBulkSecretAsync(storeName, metadata, cancellationToken);
      return Result<IReadOnlyDictionary<string, Dictionary<string, string>>>.Ok(secrets);
    }
    catch (OperationCanceledException) {
      throw;
    }
    catch (Exception ex) {
      logger.LogError(ex, ErrorMessage);
      return Result<IReadOnlyDictionary<string, Dictionary<string, string>>>.InternalServerError(default!, [ErrorMessage, .. ex.ExtractMessages()]);
    }
  }
}
