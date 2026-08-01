using System.Collections.Concurrent;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Dapr.Client;
using MaksIT.Results;
using MaksIT.Core.Extensions;


namespace MaksIT.Dapr.Services;

/// <summary>
/// Dapr service invocation with <see cref="Result"/> outcomes.
/// </summary>
public interface IDaprInvocationService {
  /// <summary>
  /// Invokes a method with no request body (HTTP POST).
  /// </summary>
  Task<Result> InvokeAsync(string appId, string methodName, CancellationToken cancellationToken = default);

  /// <summary>
  /// Invokes a method with a JSON request body (HTTP POST).
  /// </summary>
  Task<Result> InvokeAsync<TRequest>(string appId, string methodName, TRequest data, CancellationToken cancellationToken = default);

  /// <summary>
  /// Invokes a method with no request body and deserializes the JSON response (HTTP POST).
  /// </summary>
  Task<Result<TResponse?>> InvokeAsync<TResponse>(string appId, string methodName, CancellationToken cancellationToken = default);

  /// <summary>
  /// Invokes a method with a JSON request body and deserializes the JSON response (HTTP POST).
  /// </summary>
  Task<Result<TResponse?>> InvokeAsync<TRequest, TResponse>(string appId, string methodName, TRequest data, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IDaprInvocationService"/> using <see cref="DaprClient.CreateInvokableHttpClient"/>.
/// </summary>
public sealed class DaprInvocationService(
  ILogger<DaprInvocationService> logger,
  DaprClient client
) : IDaprInvocationService, IDisposable {
  private const string ErrorMessage = "MaksIT.Dapr - Invocation error";

  private readonly ConcurrentDictionary<string, HttpClient> _clients = new(StringComparer.Ordinal);
  private bool _disposed;

  /// <inheritdoc />
  public async Task<Result> InvokeAsync(string appId, string methodName, CancellationToken cancellationToken = default) {
    if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(methodName))
      return Result.BadRequest("appId and methodName are required.");

    try {
      using var response = await GetClient(appId).PostAsync(methodName, content: null, cancellationToken);
      response.EnsureSuccessStatusCode();
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
  public async Task<Result> InvokeAsync<TRequest>(string appId, string methodName, TRequest data, CancellationToken cancellationToken = default) {
    if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(methodName))
      return Result.BadRequest("appId and methodName are required.");

    try {
      using var response = await GetClient(appId).PostAsJsonAsync(methodName, data, client.JsonSerializerOptions, cancellationToken);
      response.EnsureSuccessStatusCode();
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
  public async Task<Result<TResponse?>> InvokeAsync<TResponse>(string appId, string methodName, CancellationToken cancellationToken = default) {
    if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(methodName))
      return Result<TResponse?>.BadRequest(default, "appId and methodName are required.");

    try {
      using var response = await GetClient(appId).PostAsync(methodName, content: null, cancellationToken);
      response.EnsureSuccessStatusCode();
      var body = await response.Content.ReadFromJsonAsync<TResponse>(client.JsonSerializerOptions, cancellationToken);
      return Result<TResponse?>.Ok(body);
    }
    catch (OperationCanceledException) {
      throw;
    }
    catch (Exception ex) {
      logger.LogError(ex, ErrorMessage);
      return Result<TResponse?>.InternalServerError(default, [ErrorMessage, .. ex.ExtractMessages()]);
    }
  }

  /// <inheritdoc />
  public async Task<Result<TResponse?>> InvokeAsync<TRequest, TResponse>(
    string appId,
    string methodName,
    TRequest data,
    CancellationToken cancellationToken = default) {
    if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(methodName))
      return Result<TResponse?>.BadRequest(default, "appId and methodName are required.");

    try {
      using var response = await GetClient(appId).PostAsJsonAsync(methodName, data, client.JsonSerializerOptions, cancellationToken);
      response.EnsureSuccessStatusCode();
      var body = await response.Content.ReadFromJsonAsync<TResponse>(client.JsonSerializerOptions, cancellationToken);
      return Result<TResponse?>.Ok(body);
    }
    catch (OperationCanceledException) {
      throw;
    }
    catch (Exception ex) {
      logger.LogError(ex, ErrorMessage);
      return Result<TResponse?>.InternalServerError(default, [ErrorMessage, .. ex.ExtractMessages()]);
    }
  }

  /// <inheritdoc />
  public void Dispose() {
    if (_disposed)
      return;

    _disposed = true;
    foreach (var http in _clients.Values)
      http.Dispose();

    _clients.Clear();
  }

  private HttpClient GetClient(string appId) {
    ObjectDisposedException.ThrowIf(_disposed, this);

    if (_clients.TryGetValue(appId, out var existing))
      return existing;

    var created = client.CreateInvokableHttpClient(appId);
    if (_clients.TryAdd(appId, created))
      return created;

    created.Dispose();
    return _clients[appId];
  }
}
