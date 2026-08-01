using Microsoft.Extensions.Logging;
using Dapr.Actors;
using Dapr.Actors.Client;
using MaksIT.Results;
using MaksIT.Core.Extensions;


namespace MaksIT.Dapr.Services;

/// <summary>
/// Facade over Dapr actors: create typed clients and invoke methods with <see cref="Result"/> outcomes.
/// </summary>
public interface IDaprActorService {
  /// <summary>
  /// Creates a strongly typed actor client for <typeparamref name="TActor"/>.
  /// </summary>
  Result<TActor> Create<TActor>(string actorId, string actorType) where TActor : IActor;

  /// <summary>
  /// Creates a weakly typed <see cref="ActorProxy"/> for dynamic method invocation.
  /// </summary>
  Result<ActorProxy> Create(string actorId, string actorType);

  /// <summary>
  /// Invokes an actor method with no request or response payload.
  /// </summary>
  Task<Result> InvokeAsync(string actorId, string actorType, string methodName, CancellationToken cancellationToken = default);

  /// <summary>
  /// Invokes an actor method with a request payload and no response.
  /// </summary>
  Task<Result> InvokeAsync<TRequest>(string actorId, string actorType, string methodName, TRequest data, CancellationToken cancellationToken = default);

  /// <summary>
  /// Invokes an actor method with no request payload and a typed response.
  /// </summary>
  Task<Result<TResponse?>> InvokeAsync<TResponse>(string actorId, string actorType, string methodName, CancellationToken cancellationToken = default);

  /// <summary>
  /// Invokes an actor method with a request payload and a typed response.
  /// </summary>
  Task<Result<TResponse?>> InvokeAsync<TRequest, TResponse>(string actorId, string actorType, string methodName, TRequest data, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IDaprActorService"/> using Dapr's <see cref="IActorProxyFactory"/>.
/// </summary>
public class DaprActorService : IDaprActorService {
  private const string ErrorMessage = "MaksIT.Dapr - Actor service error";

  private readonly IActorProxyFactory _actorProxyFactory;
  private readonly ILogger<DaprActorService> _logger;

  /// <summary>
  /// Creates an actor service backed by <paramref name="actorProxyFactory"/>.
  /// </summary>
  public DaprActorService(ILogger<DaprActorService> logger, IActorProxyFactory actorProxyFactory) {
    _logger = logger;
    _actorProxyFactory = actorProxyFactory;
  }

  /// <inheritdoc />
  public Result<TActor> Create<TActor>(string actorId, string actorType) where TActor : IActor {
    var validation = ValidateActorKey(actorId, actorType);
    if (!validation.IsSuccess)
      return Result<TActor>.BadRequest(default!, validation.Messages.ToArray());

    try {
      var actor = _actorProxyFactory.CreateActorProxy<TActor>(new ActorId(actorId), actorType);
      return Result<TActor>.Ok(actor);
    }
    catch (Exception ex) {
      _logger.LogError(ex, ErrorMessage);
      return Result<TActor>.InternalServerError(default!, [ErrorMessage, .. ex.ExtractMessages()]);
    }
  }

  /// <inheritdoc />
  public Result<ActorProxy> Create(string actorId, string actorType) {
    var validation = ValidateActorKey(actorId, actorType);
    if (!validation.IsSuccess)
      return Result<ActorProxy>.BadRequest(default!, validation.Messages.ToArray());

    try {
      var actor = _actorProxyFactory.Create(new ActorId(actorId), actorType);
      return Result<ActorProxy>.Ok(actor);
    }
    catch (Exception ex) {
      _logger.LogError(ex, ErrorMessage);
      return Result<ActorProxy>.InternalServerError(default!, [ErrorMessage, .. ex.ExtractMessages()]);
    }
  }

  /// <inheritdoc />
  public async Task<Result> InvokeAsync(
    string actorId,
    string actorType,
    string methodName,
    CancellationToken cancellationToken = default) {
    var validation = ValidateInvoke(actorId, actorType, methodName);
    if (!validation.IsSuccess)
      return validation;

    try {
      var actor = _actorProxyFactory.Create(new ActorId(actorId), actorType);
      await actor.InvokeMethodAsync(methodName, cancellationToken);
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
  public async Task<Result> InvokeAsync<TRequest>(
    string actorId,
    string actorType,
    string methodName,
    TRequest data,
    CancellationToken cancellationToken = default) {
    var validation = ValidateInvoke(actorId, actorType, methodName);
    if (!validation.IsSuccess)
      return validation;

    try {
      var actor = _actorProxyFactory.Create(new ActorId(actorId), actorType);
      await actor.InvokeMethodAsync(methodName, data, cancellationToken);
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
  public async Task<Result<TResponse?>> InvokeAsync<TResponse>(
    string actorId,
    string actorType,
    string methodName,
    CancellationToken cancellationToken = default) {
    var validation = ValidateInvoke(actorId, actorType, methodName);
    if (!validation.IsSuccess)
      return validation.ToResultOfType<TResponse?>(default);

    try {
      var actor = _actorProxyFactory.Create(new ActorId(actorId), actorType);
      var response = await actor.InvokeMethodAsync<TResponse>(methodName, cancellationToken);
      return Result<TResponse?>.Ok(response);
    }
    catch (OperationCanceledException) {
      throw;
    }
    catch (Exception ex) {
      _logger.LogError(ex, ErrorMessage);
      return Result<TResponse?>.InternalServerError(default, [ErrorMessage, .. ex.ExtractMessages()]);
    }
  }

  /// <inheritdoc />
  public async Task<Result<TResponse?>> InvokeAsync<TRequest, TResponse>(
    string actorId,
    string actorType,
    string methodName,
    TRequest data,
    CancellationToken cancellationToken = default) {
    var validation = ValidateInvoke(actorId, actorType, methodName);
    if (!validation.IsSuccess)
      return validation.ToResultOfType<TResponse?>(default);

    try {
      var actor = _actorProxyFactory.Create(new ActorId(actorId), actorType);
      var response = await actor.InvokeMethodAsync<TRequest, TResponse>(methodName, data, cancellationToken);
      return Result<TResponse?>.Ok(response);
    }
    catch (OperationCanceledException) {
      throw;
    }
    catch (Exception ex) {
      _logger.LogError(ex, ErrorMessage);
      return Result<TResponse?>.InternalServerError(default, [ErrorMessage, .. ex.ExtractMessages()]);
    }
  }

  private static Result ValidateActorKey(string actorId, string actorType) {
    if (string.IsNullOrWhiteSpace(actorId) || string.IsNullOrWhiteSpace(actorType))
      return Result.BadRequest("actorId and actorType are required.");
    return Result.Ok();
  }

  private static Result ValidateInvoke(string actorId, string actorType, string methodName) {
    if (string.IsNullOrWhiteSpace(actorId) || string.IsNullOrWhiteSpace(actorType) || string.IsNullOrWhiteSpace(methodName))
      return Result.BadRequest("actorId, actorType, and methodName are required.");
    return Result.Ok();
  }
}
