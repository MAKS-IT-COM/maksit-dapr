using Microsoft.Extensions.Logging;

using Dapr.Client;

using MaksIT.Results;
using MaksIT.Core.Extensions;

namespace MaksIT.Dapr.Services;
public interface IDaprStateStoreService {
  Task<Result> SetStateAsync<T>(string storeName, string key, T value);
  Task<Result<T?>> GetStateAsync<T>(string storeName, string key);
  Task<Result> DeleteStateAsync(string storeName, string key);
}


public class DaprStateStoreService : IDaprStateStoreService {
  private const string _errorMessage = "MaksIT.Dapr - Data provider error";

  private readonly DaprClient _client;
  private readonly ILogger<DaprStateStoreService> _logger;

  public DaprStateStoreService(
    ILogger<DaprStateStoreService> logger,
    DaprClient client
  ) {
    _logger = logger;
    _client = client;
  }
  /// <summary>
  /// Saves a state to a Dapr state store
  /// </summary>
  /// <typeparam name="T"></typeparam>
  /// <param name="storeName"></param>
  /// <param name="key"></param>
  /// <param name="value"></param>
  /// <returns></returns>
  public async Task<Result> SetStateAsync<T>(string storeName, string key, T value) {
    try {
      await _client.SaveStateAsync(storeName, key, value);
      return Result.Ok();
    }
    catch (Exception ex) {
      _logger.LogError(ex, _errorMessage);
      return Result.InternalServerError(new[] {_errorMessage}.Concat(ex.ExtractMessages()).ToArray());
    }
  }

  /// <summary>
  /// Gets a state from a Dapr state store
  /// </summary>
  /// <typeparam name="T"></typeparam>
  /// <param name="storeName"></param>
  /// <param name="key"></param>
  /// <returns></returns>
  public async Task<Result<T?>> GetStateAsync<T>(string storeName, string key) {
    try {
      var state = await _client.GetStateAsync<T?>(storeName, key);
      if (state == null)
        return Result<T?>.NotFound(default, $"State from the store {storeName} with the {key} not found.");

      return Result<T?>.Ok(state);
    }
    catch (Exception ex) {
      _logger.LogError(ex, _errorMessage);
      return Result<T?>.InternalServerError(default, new[] {_errorMessage}.Concat(ex.ExtractMessages()).ToArray());
    }
  }

  /// <summary>
  /// Deletes a state from a Dapr state store
  /// </summary>
  /// <param name="storeName"></param>
  /// <param name="key"></param>
  /// <returns></returns>
  public async Task<Result> DeleteStateAsync(string storeName, string key) {
    try {
      await _client.DeleteStateAsync(storeName, key);
      return Result.Ok();
    }
    catch (Exception ex) {
      _logger.LogError(ex, _errorMessage);
      return Result.InternalServerError([_errorMessage, .. ex.ExtractMessages()]);
    }
  }
}