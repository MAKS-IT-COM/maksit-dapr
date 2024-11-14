using Microsoft.Extensions.Logging;

using Dapr.Client;

using MaksIT.Results;

namespace MaksIT.Dapr;

public interface IDaprPublisherService {
  Task<Result> PublishEventAsync(string pubSubName, string topicName, string payload);
}

public interface IDaprStateStoreService {
  Task<Result> SetStateAsync<T>(string storeName, string key, T value);
  Task<Result<T?>> GetStateAsync<T>(string storeName, string key);
  Task<Result> DeleteStateAsync(string storeName, string key);
}

public class DaprService : IDaprPublisherService, IDaprStateStoreService {
  private const string _errorMessage = "MaksIT.Dapr - Data provider error";

  private readonly DaprClient _client;
  private readonly ILogger<DaprService> _logger;

  public DaprService(
    ILogger<DaprService> logger,
    DaprClient client
  ) {
    _logger = logger;
    _client = client;
  }

  /// <summary>
  /// Publishes an event to a Dapr topic
  /// </summary>
  /// <param name="pubSubName"></param>
  /// <param name="topicName"></param>
  /// <param name="payload"></param>
  /// <returns></returns>
  public async Task<Result> PublishEventAsync(string pubSubName, string topicName, string payload) {
    try {
      await _client.PublishEventAsync(pubSubName, topicName, payload);
      return Result.Ok();
    }
    catch (Exception ex) {
      _logger.LogError(ex, _errorMessage);
      return Result.InternalServerError(ex.Message);
    }
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
      return Result.InternalServerError(ex.Message);
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
      return Result<T?>.InternalServerError(default, ex.Message);
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
      return Result.InternalServerError(ex.Message);
    }
  }
}