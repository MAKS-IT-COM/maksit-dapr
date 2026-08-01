using Microsoft.Extensions.Logging;
using Grpc.Core;
using Dapr.Client;
using MaksIT.Results;
using MaksIT.Core.Extensions;


namespace MaksIT.Dapr.Services;

/// <summary>
/// Dapr state store operations with <see cref="Result"/> outcomes.
/// </summary>
public interface IDaprStateStoreService {
  /// <summary>
  /// Saves <paramref name="value"/> under <paramref name="key"/> in <paramref name="storeName"/>.
  /// </summary>
  Task<Result> SetStateAsync<T>(
    string storeName,
    string key,
    T value,
    StateOptions? options = null,
    IReadOnlyDictionary<string, string>? metadata = null,
    CancellationToken cancellationToken = default);

  /// <summary>
  /// Gets state for <paramref name="key"/>. Missing keys return <c>Ok(null)</c>; infra failures are unsuccessful.
  /// </summary>
  Task<Result<T?>> GetStateAsync<T>(
    string storeName,
    string key,
    ConsistencyMode? consistencyMode = null,
    IReadOnlyDictionary<string, string>? metadata = null,
    CancellationToken cancellationToken = default);

  /// <summary>
  /// Gets state and ETag for optimistic concurrency. Missing keys return <c>Ok((null, null))</c>.
  /// </summary>
  Task<Result<(T? Value, string? ETag)>> GetStateAndETagAsync<T>(
    string storeName,
    string key,
    ConsistencyMode? consistencyMode = null,
    IReadOnlyDictionary<string, string>? metadata = null,
    CancellationToken cancellationToken = default);

  /// <summary>
  /// Attempts an ETag-conditional save. <see cref="Result{T}.Value"/> is <c>false</c> on conflict.
  /// </summary>
  Task<Result<bool>> TrySaveStateAsync<T>(
    string storeName,
    string key,
    T value,
    string? etag,
    StateOptions? options = null,
    IReadOnlyDictionary<string, string>? metadata = null,
    CancellationToken cancellationToken = default);

  /// <summary>
  /// Deletes <paramref name="key"/>. Missing keys are treated as success (idempotent).
  /// </summary>
  Task<Result> DeleteStateAsync(
    string storeName,
    string key,
    StateOptions? options = null,
    IReadOnlyDictionary<string, string>? metadata = null,
    CancellationToken cancellationToken = default);

  /// <summary>
  /// Attempts an ETag-conditional delete.
  /// </summary>
  Task<Result<bool>> TryDeleteStateAsync(
    string storeName,
    string key,
    string? etag,
    StateOptions? options = null,
    IReadOnlyDictionary<string, string>? metadata = null,
    CancellationToken cancellationToken = default);

  /// <summary>
  /// Gets multiple keys from the store.
  /// </summary>
  Task<Result<IReadOnlyList<BulkStateItem>>> GetBulkStateAsync(
    string storeName,
    IReadOnlyList<string> keys,
    int? parallelism = null,
    IReadOnlyDictionary<string, string>? metadata = null,
    CancellationToken cancellationToken = default);

  /// <summary>
  /// Saves multiple items.
  /// </summary>
  Task<Result> SaveBulkStateAsync<T>(
    string storeName,
    IReadOnlyList<SaveStateItem<T>> items,
    CancellationToken cancellationToken = default);

  /// <summary>
  /// Deletes multiple items.
  /// </summary>
  Task<Result> DeleteBulkStateAsync(
    string storeName,
    IReadOnlyList<BulkDeleteStateItem> items,
    CancellationToken cancellationToken = default);

  /// <summary>
  /// Queries state with a JSON query document.
  /// </summary>
  Task<Result<StateQueryResponse<T>>> QueryStateAsync<T>(
    string storeName,
    string jsonQuery,
    IReadOnlyDictionary<string, string>? metadata = null,
    CancellationToken cancellationToken = default);

  /// <summary>
  /// Executes a state transaction.
  /// </summary>
  Task<Result> ExecuteStateTransactionAsync(
    string storeName,
    IReadOnlyList<StateTransactionRequest> operations,
    IReadOnlyDictionary<string, string>? metadata = null,
    CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IDaprStateStoreService"/> using <see cref="DaprClient"/>.
/// </summary>
public class DaprStateStoreService : IDaprStateStoreService {
  private const string ErrorMessage = "MaksIT.Dapr - State store error";

  private readonly DaprClient _client;
  private readonly ILogger<DaprStateStoreService> _logger;

  /// <summary>
  /// Creates a state store service backed by <paramref name="client"/>.
  /// </summary>
  public DaprStateStoreService(ILogger<DaprStateStoreService> logger, DaprClient client) {
    _logger = logger;
    _client = client;
  }

  /// <inheritdoc />
  public async Task<Result> SetStateAsync<T>(
    string storeName,
    string key,
    T value,
    StateOptions? options = null,
    IReadOnlyDictionary<string, string>? metadata = null,
    CancellationToken cancellationToken = default) {
    if (string.IsNullOrWhiteSpace(storeName) || string.IsNullOrWhiteSpace(key))
      return Result.BadRequest("storeName and key are required.");

    try {
      await _client.SaveStateAsync(storeName, key, value, options, metadata, cancellationToken);
      return Result.Ok();
    }
    catch (OperationCanceledException) {
      throw;
    }
    catch (Exception ex) {
      _logger.LogError(ex, ErrorMessage);
      return Result.InternalServerError([ErrorMessage, .. ex.ExtractMessages()]);
    }
  }

  /// <inheritdoc />
  public async Task<Result<T?>> GetStateAsync<T>(
    string storeName,
    string key,
    ConsistencyMode? consistencyMode = null,
    IReadOnlyDictionary<string, string>? metadata = null,
    CancellationToken cancellationToken = default) {
    if (string.IsNullOrWhiteSpace(storeName) || string.IsNullOrWhiteSpace(key))
      return Result<T?>.BadRequest(default, "storeName and key are required.");

    try {
      var state = await _client.GetStateAsync<T?>(storeName, key, consistencyMode, metadata, cancellationToken);
      return Result<T?>.Ok(state);
    }
    catch (Exception ex) when (IsStateKeyNotFound(ex)) {
      return Result<T?>.Ok(default);
    }
    catch (OperationCanceledException) {
      throw;
    }
    catch (Exception ex) {
      _logger.LogError(ex, ErrorMessage);
      return Result<T?>.InternalServerError(default, [ErrorMessage, .. ex.ExtractMessages()]);
    }
  }

  /// <inheritdoc />
  public async Task<Result<(T? Value, string? ETag)>> GetStateAndETagAsync<T>(
    string storeName,
    string key,
    ConsistencyMode? consistencyMode = null,
    IReadOnlyDictionary<string, string>? metadata = null,
    CancellationToken cancellationToken = default) {
    if (string.IsNullOrWhiteSpace(storeName) || string.IsNullOrWhiteSpace(key))
      return Result<(T? Value, string? ETag)>.BadRequest(default, "storeName and key are required.");

    try {
      var (value, etag) = await _client.GetStateAndETagAsync<T?>(storeName, key, consistencyMode, metadata, cancellationToken);
      return Result<(T? Value, string? ETag)>.Ok((value, etag));
    }
    catch (Exception ex) when (IsStateKeyNotFound(ex)) {
      return Result<(T? Value, string? ETag)>.Ok((default, null));
    }
    catch (OperationCanceledException) {
      throw;
    }
    catch (Exception ex) {
      _logger.LogError(ex, ErrorMessage);
      return Result<(T? Value, string? ETag)>.InternalServerError(default, [ErrorMessage, .. ex.ExtractMessages()]);
    }
  }

  /// <inheritdoc />
  public async Task<Result<bool>> TrySaveStateAsync<T>(
    string storeName,
    string key,
    T value,
    string? etag,
    StateOptions? options = null,
    IReadOnlyDictionary<string, string>? metadata = null,
    CancellationToken cancellationToken = default) {
    if (string.IsNullOrWhiteSpace(storeName) || string.IsNullOrWhiteSpace(key))
      return Result<bool>.BadRequest(false, "storeName and key are required.");

    try {
      var saved = await _client.TrySaveStateAsync(storeName, key, value, etag ?? string.Empty, options, metadata, cancellationToken);
      return Result<bool>.Ok(saved);
    }
    catch (OperationCanceledException) {
      throw;
    }
    catch (Exception ex) {
      _logger.LogError(ex, ErrorMessage);
      return Result<bool>.InternalServerError(false, [ErrorMessage, .. ex.ExtractMessages()]);
    }
  }

  /// <inheritdoc />
  public async Task<Result> DeleteStateAsync(
    string storeName,
    string key,
    StateOptions? options = null,
    IReadOnlyDictionary<string, string>? metadata = null,
    CancellationToken cancellationToken = default) {
    if (string.IsNullOrWhiteSpace(storeName) || string.IsNullOrWhiteSpace(key))
      return Result.BadRequest("storeName and key are required.");

    try {
      await _client.DeleteStateAsync(storeName, key, options, metadata, cancellationToken);
      return Result.Ok();
    }
    catch (Exception ex) when (IsStateKeyNotFound(ex)) {
      return Result.Ok();
    }
    catch (OperationCanceledException) {
      throw;
    }
    catch (Exception ex) {
      _logger.LogError(ex, ErrorMessage);
      return Result.InternalServerError([ErrorMessage, .. ex.ExtractMessages()]);
    }
  }

  /// <inheritdoc />
  public async Task<Result<bool>> TryDeleteStateAsync(
    string storeName,
    string key,
    string? etag,
    StateOptions? options = null,
    IReadOnlyDictionary<string, string>? metadata = null,
    CancellationToken cancellationToken = default) {
    if (string.IsNullOrWhiteSpace(storeName) || string.IsNullOrWhiteSpace(key))
      return Result<bool>.BadRequest(false, "storeName and key are required.");

    try {
      var deleted = await _client.TryDeleteStateAsync(storeName, key, etag ?? string.Empty, options, metadata, cancellationToken);
      return Result<bool>.Ok(deleted);
    }
    catch (OperationCanceledException) {
      throw;
    }
    catch (Exception ex) {
      _logger.LogError(ex, ErrorMessage);
      return Result<bool>.InternalServerError(false, [ErrorMessage, .. ex.ExtractMessages()]);
    }
  }

  /// <inheritdoc />
  public async Task<Result<IReadOnlyList<BulkStateItem>>> GetBulkStateAsync(
    string storeName,
    IReadOnlyList<string> keys,
    int? parallelism = null,
    IReadOnlyDictionary<string, string>? metadata = null,
    CancellationToken cancellationToken = default) {
    if (string.IsNullOrWhiteSpace(storeName) || keys is null || keys.Count == 0)
      return Result<IReadOnlyList<BulkStateItem>>.BadRequest(default!, "storeName and keys are required.");

    try {
      var items = await _client.GetBulkStateAsync(storeName, keys, parallelism, metadata, cancellationToken);
      return Result<IReadOnlyList<BulkStateItem>>.Ok(items);
    }
    catch (OperationCanceledException) {
      throw;
    }
    catch (Exception ex) {
      _logger.LogError(ex, ErrorMessage);
      return Result<IReadOnlyList<BulkStateItem>>.InternalServerError(default!, [ErrorMessage, .. ex.ExtractMessages()]);
    }
  }

  /// <inheritdoc />
  public async Task<Result> SaveBulkStateAsync<T>(
    string storeName,
    IReadOnlyList<SaveStateItem<T>> items,
    CancellationToken cancellationToken = default) {
    if (string.IsNullOrWhiteSpace(storeName) || items is null || items.Count == 0)
      return Result.BadRequest("storeName and items are required.");

    try {
      await _client.SaveBulkStateAsync(storeName, items, cancellationToken);
      return Result.Ok();
    }
    catch (OperationCanceledException) {
      throw;
    }
    catch (Exception ex) {
      _logger.LogError(ex, ErrorMessage);
      return Result.InternalServerError([ErrorMessage, .. ex.ExtractMessages()]);
    }
  }

  /// <inheritdoc />
  public async Task<Result> DeleteBulkStateAsync(
    string storeName,
    IReadOnlyList<BulkDeleteStateItem> items,
    CancellationToken cancellationToken = default) {
    if (string.IsNullOrWhiteSpace(storeName) || items is null || items.Count == 0)
      return Result.BadRequest("storeName and items are required.");

    try {
      await _client.DeleteBulkStateAsync(storeName, items, cancellationToken);
      return Result.Ok();
    }
    catch (OperationCanceledException) {
      throw;
    }
    catch (Exception ex) {
      _logger.LogError(ex, ErrorMessage);
      return Result.InternalServerError([ErrorMessage, .. ex.ExtractMessages()]);
    }
  }

  /// <inheritdoc />
  public async Task<Result<StateQueryResponse<T>>> QueryStateAsync<T>(
    string storeName,
    string jsonQuery,
    IReadOnlyDictionary<string, string>? metadata = null,
    CancellationToken cancellationToken = default) {
    if (string.IsNullOrWhiteSpace(storeName) || string.IsNullOrWhiteSpace(jsonQuery))
      return Result<StateQueryResponse<T>>.BadRequest(default!, "storeName and jsonQuery are required.");

    try {
      var response = await _client.QueryStateAsync<T>(storeName, jsonQuery, metadata, cancellationToken);
      return Result<StateQueryResponse<T>>.Ok(response);
    }
    catch (OperationCanceledException) {
      throw;
    }
    catch (Exception ex) {
      _logger.LogError(ex, ErrorMessage);
      return Result<StateQueryResponse<T>>.InternalServerError(default!, [ErrorMessage, .. ex.ExtractMessages()]);
    }
  }

  /// <inheritdoc />
  public async Task<Result> ExecuteStateTransactionAsync(
    string storeName,
    IReadOnlyList<StateTransactionRequest> operations,
    IReadOnlyDictionary<string, string>? metadata = null,
    CancellationToken cancellationToken = default) {
    if (string.IsNullOrWhiteSpace(storeName) || operations is null || operations.Count == 0)
      return Result.BadRequest("storeName and operations are required.");

    try {
      await _client.ExecuteStateTransactionAsync(storeName, operations, metadata, cancellationToken);
      return Result.Ok();
    }
    catch (OperationCanceledException) {
      throw;
    }
    catch (Exception ex) {
      _logger.LogError(ex, ErrorMessage);
      return Result.InternalServerError([ErrorMessage, .. ex.ExtractMessages()]);
    }
  }

  private static bool IsStateKeyNotFound(Exception ex) {
    for (var current = ex; current is not null; current = current.InnerException) {
      if (current is RpcException rpc &&
          (rpc.StatusCode == StatusCode.NotFound || ContainsKeyNotFound(rpc.Status.Detail)))
        return true;

      if (ContainsKeyNotFound(current.Message))
        return true;
    }

    return false;
  }

  private static bool ContainsKeyNotFound(string? text) =>
    !string.IsNullOrEmpty(text) &&
    text.Contains("key not found", StringComparison.OrdinalIgnoreCase);
}
