using Microsoft.Extensions.Logging;
using Dapr.Client;
using MaksIT.Results;
using MaksIT.Core.Extensions;


namespace MaksIT.Dapr.Services;

/// <summary>
/// Dapr output bindings with <see cref="Result"/> outcomes.
/// </summary>
public interface IDaprBindingService {
  /// <summary>
  /// Invokes a binding operation.
  /// </summary>
  Task<Result> InvokeAsync<TRequest>(
    string bindingName,
    string operation,
    TRequest data,
    IReadOnlyDictionary<string, string>? metadata = null,
    CancellationToken cancellationToken = default);

  /// <summary>
  /// Invokes a binding operation and deserializes the response.
  /// </summary>
  Task<Result<TResponse?>> InvokeAsync<TRequest, TResponse>(
    string bindingName,
    string operation,
    TRequest data,
    IReadOnlyDictionary<string, string>? metadata = null,
    CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IDaprBindingService"/>.
/// </summary>
public class DaprBindingService(
  ILogger<DaprBindingService> logger,
  DaprClient client
) : IDaprBindingService {
  private const string ErrorMessage = "MaksIT.Dapr - Binding error";

  /// <inheritdoc />
  public async Task<Result> InvokeAsync<TRequest>(
    string bindingName,
    string operation,
    TRequest data,
    IReadOnlyDictionary<string, string>? metadata = null,
    CancellationToken cancellationToken = default) {
    if (string.IsNullOrWhiteSpace(bindingName) || string.IsNullOrWhiteSpace(operation))
      return Result.BadRequest("bindingName and operation are required.");

    try {
      await client.InvokeBindingAsync(bindingName, operation, data, metadata, cancellationToken);
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
  public async Task<Result<TResponse?>> InvokeAsync<TRequest, TResponse>(
    string bindingName,
    string operation,
    TRequest data,
    IReadOnlyDictionary<string, string>? metadata = null,
    CancellationToken cancellationToken = default) {
    if (string.IsNullOrWhiteSpace(bindingName) || string.IsNullOrWhiteSpace(operation))
      return Result<TResponse?>.BadRequest(default, "bindingName and operation are required.");

    try {
      var response = await client.InvokeBindingAsync<TRequest, TResponse>(bindingName, operation, data, metadata, cancellationToken);
      return Result<TResponse?>.Ok(response);
    }
    catch (OperationCanceledException) {
      throw;
    }
    catch (Exception ex) {
      logger.LogError(ex, ErrorMessage);
      return Result<TResponse?>.InternalServerError(default, [ErrorMessage, .. ex.ExtractMessages()]);
    }
  }
}
