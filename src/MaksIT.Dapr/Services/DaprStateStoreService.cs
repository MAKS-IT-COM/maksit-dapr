using Dapr.Client;
using MaksIT.Core.Extensions;
using MaksIT.Results;
using Microsoft.Extensions.Logging;


namespace MaksIT.Dapr.Services;

public interface IDaprStateStoreService {
  Task<Result> SetStateAsync<T>(string storeName, string key, T value);
  Task<Result<T?>> GetStateAsync<T>(string storeName, string key);
  Task<Result<(T? Value, string? ETag)>> GetStateAndETagAsync<T>(string storeName, string key, CancellationToken cancellationToken = default);
  Task<Result<bool>> TrySaveStateAsync<T>(string storeName, string key, T value, string? etag, CancellationToken cancellationToken = default);
  Task<Result> DeleteStateAsync(string storeName, string key);
}

public class DaprStateStoreService : IDaprStateStoreService {
  private const string ErrorMessage = "MaksIT.Dapr - Data provider error";

  private readonly DaprClient _client;
  private readonly ILogger<DaprStateStoreService> _logger;

  public DaprStateStoreService(ILogger<DaprStateStoreService> logger, DaprClient client) {
    _logger = logger;
    _client = client;
  }

  public async Task<Result> SetStateAsync<T>(string storeName, string key, T value) {
    try {
      await _client.SaveStateAsync(storeName, key, value);
      return Result.Ok();
    }
    catch (Exception ex) {
      _logger.LogError(ex, ErrorMessage);
      return Result.InternalServerError([ErrorMessage, .. ex.ExtractMessages()]);
    }
  }

  public async Task<Result<T?>> GetStateAsync<T>(string storeName, string key) {
    try {
      var state = await _client.GetStateAsync<T?>(storeName, key);
      if (state is null)
        return Result<T?>.NotFound(default, $"State from the store {storeName} with the {key} not found.");

      return Result<T?>.Ok(state);
    }
    catch (Exception ex) {
      _logger.LogError(ex, ErrorMessage);
      return Result<T?>.InternalServerError(default, [ErrorMessage, .. ex.ExtractMessages()]);
    }
  }

  public async Task<Result<(T? Value, string? ETag)>> GetStateAndETagAsync<T>(
    string storeName,
    string key,
    CancellationToken cancellationToken = default) {
    try {
      var (value, etag) = await _client.GetStateAndETagAsync<T?>(storeName, key, cancellationToken: cancellationToken);
      return Result<(T? Value, string? ETag)>.Ok((value, etag));
    }
    catch (Exception ex) {
      _logger.LogError(ex, ErrorMessage);
      return Result<(T? Value, string? ETag)>.InternalServerError(default, [ErrorMessage, .. ex.ExtractMessages()]);
    }
  }

  public async Task<Result<bool>> TrySaveStateAsync<T>(
    string storeName,
    string key,
    T value,
    string? etag,
    CancellationToken cancellationToken = default) {
    try {
      var saved = await _client.TrySaveStateAsync(storeName, key, value, etag ?? string.Empty, cancellationToken: cancellationToken);
      return Result<bool>.Ok(saved);
    }
    catch (Exception ex) {
      _logger.LogError(ex, ErrorMessage);
      return Result<bool>.InternalServerError(false, [ErrorMessage, .. ex.ExtractMessages()]);
    }
  }

  public async Task<Result> DeleteStateAsync(string storeName, string key) {
    try {
      await _client.DeleteStateAsync(storeName, key);
      return Result.Ok();
    }
    catch (Exception ex) {
      _logger.LogError(ex, ErrorMessage);
      return Result.InternalServerError([ErrorMessage, .. ex.ExtractMessages()]);
    }
  }
}
