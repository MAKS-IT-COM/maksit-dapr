using Microsoft.Extensions.Logging;
using Dapr.Workflow;
using Dapr.Workflow.Client;
using MaksIT.Results;
using MaksIT.Core.Extensions;


namespace MaksIT.Dapr.Services;

/// <summary>
/// Schedules and manages Dapr workflow instances with <see cref="Result"/> outcomes.
/// </summary>
public interface IDaprWorkflowService {
  /// <summary>
  /// Schedules a new workflow instance. Returns the instance id.
  /// </summary>
  Task<Result<string>> ScheduleAsync(
    string workflowName,
    object? input = null,
    string? instanceId = null,
    DateTimeOffset? startTime = null,
    CancellationToken cancellationToken = default);

  /// <summary>
  /// Gets the current state of a workflow instance.
  /// </summary>
  Task<Result<WorkflowState>> GetStateAsync(string instanceId, bool getInputsAndOutputs = true, CancellationToken cancellationToken = default);

  /// <summary>
  /// Waits until the workflow has started.
  /// </summary>
  Task<Result<WorkflowState>> WaitForStartAsync(string instanceId, bool getInputsAndOutputs = true, CancellationToken cancellationToken = default);

  /// <summary>
  /// Waits until the workflow has completed.
  /// </summary>
  Task<Result<WorkflowState>> WaitForCompletionAsync(string instanceId, bool getInputsAndOutputs = true, CancellationToken cancellationToken = default);

  /// <summary>
  /// Raises an external event to a waiting workflow.
  /// </summary>
  Task<Result> RaiseEventAsync(string instanceId, string eventName, object? eventPayload = null, CancellationToken cancellationToken = default);

  /// <summary>
  /// Terminates a running workflow instance.
  /// </summary>
  Task<Result> TerminateAsync(string instanceId, object? output = null, CancellationToken cancellationToken = default);

  /// <summary>
  /// Suspends a running workflow instance.
  /// </summary>
  Task<Result> SuspendAsync(string instanceId, string? reason = null, CancellationToken cancellationToken = default);

  /// <summary>
  /// Resumes a suspended workflow instance.
  /// </summary>
  Task<Result> ResumeAsync(string instanceId, string? reason = null, CancellationToken cancellationToken = default);

  /// <summary>
  /// Purges history for a completed workflow instance.
  /// </summary>
  Task<Result> PurgeAsync(string instanceId, CancellationToken cancellationToken = default);

  /// <summary>
  /// Lists workflow instance IDs with optional pagination.
  /// </summary>
  Task<Result<WorkflowInstancePage>> ListInstanceIdsAsync(
    string? continuationToken = null,
    int? pageSize = null,
    CancellationToken cancellationToken = default);

  /// <summary>
  /// Gets the full execution history of a workflow instance.
  /// </summary>
  Task<Result<IReadOnlyList<WorkflowHistoryEvent>>> GetInstanceHistoryAsync(
    string instanceId,
    CancellationToken cancellationToken = default);

  /// <summary>
  /// Reruns a workflow from a history event, returning the new instance id.
  /// </summary>
  Task<Result<string>> RerunFromEventAsync(
    string sourceInstanceId,
    uint eventId,
    RerunWorkflowFromEventOptions? options = null,
    CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IDaprWorkflowService"/> using <see cref="IDaprWorkflowClient"/>.
/// </summary>
public class DaprWorkflowService : IDaprWorkflowService {
  private const string ErrorMessage = "MaksIT.Dapr - Workflow error";

  private readonly IDaprWorkflowClient _client;
  private readonly ILogger<DaprWorkflowService> _logger;

  /// <summary>
  /// Creates a workflow facade backed by <paramref name="client"/>.
  /// </summary>
  public DaprWorkflowService(ILogger<DaprWorkflowService> logger, IDaprWorkflowClient client) {
    _logger = logger;
    _client = client;
  }

  /// <inheritdoc />
  public async Task<Result<string>> ScheduleAsync(
    string workflowName,
    object? input = null,
    string? instanceId = null,
    DateTimeOffset? startTime = null,
    CancellationToken cancellationToken = default) {
    if (string.IsNullOrWhiteSpace(workflowName))
      return Result<string>.BadRequest(default!, "workflowName is required.");

    try {
      var id = await _client.ScheduleNewWorkflowAsync(workflowName, instanceId, input, startTime, cancellationToken);
      return Result<string>.Ok(id);
    }
    catch (OperationCanceledException) {
      throw;
    }
    catch (Exception ex) {
      _logger.LogError(ex, ErrorMessage);
      return Result<string>.InternalServerError(default!, [ErrorMessage, .. ex.ExtractMessages()]);
    }
  }

  /// <inheritdoc />
  public async Task<Result<WorkflowState>> GetStateAsync(
    string instanceId,
    bool getInputsAndOutputs = true,
    CancellationToken cancellationToken = default) {
    var validation = ValidateInstanceId(instanceId);
    if (!validation.IsSuccess)
      return Result<WorkflowState>.BadRequest(default!, validation.Messages.ToArray());

    try {
      var state = await _client.GetWorkflowStateAsync(instanceId, getInputsAndOutputs, cancellationToken);
      return Result<WorkflowState>.Ok(state);
    }
    catch (OperationCanceledException) {
      throw;
    }
    catch (Exception ex) {
      _logger.LogError(ex, ErrorMessage);
      return Result<WorkflowState>.InternalServerError(default!, [ErrorMessage, .. ex.ExtractMessages()]);
    }
  }

  /// <inheritdoc />
  public async Task<Result<WorkflowState>> WaitForStartAsync(
    string instanceId,
    bool getInputsAndOutputs = true,
    CancellationToken cancellationToken = default) {
    var validation = ValidateInstanceId(instanceId);
    if (!validation.IsSuccess)
      return Result<WorkflowState>.BadRequest(default!, validation.Messages.ToArray());

    try {
      var state = await _client.WaitForWorkflowStartAsync(instanceId, getInputsAndOutputs, cancellationToken);
      return Result<WorkflowState>.Ok(state);
    }
    catch (OperationCanceledException) {
      throw;
    }
    catch (Exception ex) {
      _logger.LogError(ex, ErrorMessage);
      return Result<WorkflowState>.InternalServerError(default!, [ErrorMessage, .. ex.ExtractMessages()]);
    }
  }

  /// <inheritdoc />
  public async Task<Result<WorkflowState>> WaitForCompletionAsync(
    string instanceId,
    bool getInputsAndOutputs = true,
    CancellationToken cancellationToken = default) {
    var validation = ValidateInstanceId(instanceId);
    if (!validation.IsSuccess)
      return Result<WorkflowState>.BadRequest(default!, validation.Messages.ToArray());

    try {
      var state = await _client.WaitForWorkflowCompletionAsync(instanceId, getInputsAndOutputs, cancellationToken);
      return Result<WorkflowState>.Ok(state);
    }
    catch (OperationCanceledException) {
      throw;
    }
    catch (Exception ex) {
      _logger.LogError(ex, ErrorMessage);
      return Result<WorkflowState>.InternalServerError(default!, [ErrorMessage, .. ex.ExtractMessages()]);
    }
  }

  /// <inheritdoc />
  public async Task<Result> RaiseEventAsync(
    string instanceId,
    string eventName,
    object? eventPayload = null,
    CancellationToken cancellationToken = default) {
    if (string.IsNullOrWhiteSpace(instanceId) || string.IsNullOrWhiteSpace(eventName))
      return Result.BadRequest("instanceId and eventName are required.");

    try {
      await _client.RaiseEventAsync(instanceId, eventName, eventPayload, cancellationToken);
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
  public async Task<Result> TerminateAsync(
    string instanceId,
    object? output = null,
    CancellationToken cancellationToken = default) {
    var validation = ValidateInstanceId(instanceId);
    if (!validation.IsSuccess)
      return validation;

    try {
      await _client.TerminateWorkflowAsync(instanceId, output, cancellationToken);
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
  public async Task<Result> SuspendAsync(
    string instanceId,
    string? reason = null,
    CancellationToken cancellationToken = default) {
    var validation = ValidateInstanceId(instanceId);
    if (!validation.IsSuccess)
      return validation;

    try {
      await _client.SuspendWorkflowAsync(instanceId, reason, cancellationToken);
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
  public async Task<Result> ResumeAsync(
    string instanceId,
    string? reason = null,
    CancellationToken cancellationToken = default) {
    var validation = ValidateInstanceId(instanceId);
    if (!validation.IsSuccess)
      return validation;

    try {
      await _client.ResumeWorkflowAsync(instanceId, reason, cancellationToken);
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
  public async Task<Result> PurgeAsync(string instanceId, CancellationToken cancellationToken = default) {
    var validation = ValidateInstanceId(instanceId);
    if (!validation.IsSuccess)
      return validation;

    try {
      await _client.PurgeInstanceAsync(instanceId, cancellationToken);
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
  public async Task<Result<WorkflowInstancePage>> ListInstanceIdsAsync(
    string? continuationToken = null,
    int? pageSize = null,
    CancellationToken cancellationToken = default) {
    try {
      var page = await _client.ListInstanceIdsAsync(continuationToken, pageSize, cancellationToken);
      return Result<WorkflowInstancePage>.Ok(page);
    }
    catch (OperationCanceledException) {
      throw;
    }
    catch (Exception ex) {
      _logger.LogError(ex, ErrorMessage);
      return Result<WorkflowInstancePage>.InternalServerError(default!, [ErrorMessage, .. ex.ExtractMessages()]);
    }
  }

  /// <inheritdoc />
  public async Task<Result<IReadOnlyList<WorkflowHistoryEvent>>> GetInstanceHistoryAsync(
    string instanceId,
    CancellationToken cancellationToken = default) {
    var validation = ValidateInstanceId(instanceId);
    if (!validation.IsSuccess)
      return Result<IReadOnlyList<WorkflowHistoryEvent>>.BadRequest(default!, validation.Messages.ToArray());

    try {
      var history = await _client.GetInstanceHistoryAsync(instanceId, cancellationToken);
      return Result<IReadOnlyList<WorkflowHistoryEvent>>.Ok(history);
    }
    catch (OperationCanceledException) {
      throw;
    }
    catch (Exception ex) {
      _logger.LogError(ex, ErrorMessage);
      return Result<IReadOnlyList<WorkflowHistoryEvent>>.InternalServerError(default!, [ErrorMessage, .. ex.ExtractMessages()]);
    }
  }

  /// <inheritdoc />
  public async Task<Result<string>> RerunFromEventAsync(
    string sourceInstanceId,
    uint eventId,
    RerunWorkflowFromEventOptions? options = null,
    CancellationToken cancellationToken = default) {
    var validation = ValidateInstanceId(sourceInstanceId);
    if (!validation.IsSuccess)
      return Result<string>.BadRequest(default!, validation.Messages.ToArray());

    try {
      var id = await _client.RerunWorkflowFromEventAsync(sourceInstanceId, eventId, options, cancellationToken);
      return Result<string>.Ok(id);
    }
    catch (OperationCanceledException) {
      throw;
    }
    catch (Exception ex) {
      _logger.LogError(ex, ErrorMessage);
      return Result<string>.InternalServerError(default!, [ErrorMessage, .. ex.ExtractMessages()]);
    }
  }

  private static Result ValidateInstanceId(string instanceId) {
    if (string.IsNullOrWhiteSpace(instanceId))
      return Result.BadRequest("instanceId is required.");
    return Result.Ok();
  }
}
