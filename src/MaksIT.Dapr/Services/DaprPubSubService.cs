using Microsoft.Extensions.Logging;
using Dapr.Client;
using MaksIT.Results;
using MaksIT.Core.Extensions;


namespace MaksIT.Dapr.Services;

/// <summary>
/// Publishes events to a Dapr pub/sub component.
/// </summary>
public interface IDaprPubSubService {
  /// <summary>
  /// Publishes <paramref name="payload"/> to <paramref name="topicName"/> on <paramref name="pubsubName"/>.
  /// </summary>
  Task<Result> PublishEventAsync(
    string pubsubName,
    string topicName,
    object payload,
    IReadOnlyDictionary<string, string>? metadata = null,
    CancellationToken cancellationToken = default);

  /// <summary>
  /// Publishes a raw byte payload.
  /// </summary>
  Task<Result> PublishByteEventAsync(
    string pubsubName,
    string topicName,
    ReadOnlyMemory<byte> data,
    string contentType = "application/json",
    IReadOnlyDictionary<string, string>? metadata = null,
    CancellationToken cancellationToken = default);

  /// <summary>
  /// Publishes multiple events; returns the Dapr bulk response (including failed entries).
  /// </summary>
  Task<Result<BulkPublishResponse<T>>> BulkPublishEventAsync<T>(
    string pubsubName,
    string topicName,
    IReadOnlyList<T> events,
    Dictionary<string, string>? metadata = null,
    CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IDaprPubSubService"/> using <see cref="DaprClient"/>.
/// </summary>
public class DaprPubSubService : IDaprPubSubService {
  private const string ErrorMessage = "MaksIT.Dapr - Pub/sub error";

  private readonly DaprClient _client;
  private readonly ILogger<DaprPubSubService> _logger;

  /// <summary>
  /// Creates a pub/sub facade backed by <paramref name="client"/>.
  /// </summary>
  public DaprPubSubService(ILogger<DaprPubSubService> logger, DaprClient client) {
    _logger = logger;
    _client = client;
  }

  /// <inheritdoc />
  public async Task<Result> PublishEventAsync(
    string pubsubName,
    string topicName,
    object payload,
    IReadOnlyDictionary<string, string>? metadata = null,
    CancellationToken cancellationToken = default) {
    if (string.IsNullOrWhiteSpace(pubsubName) || string.IsNullOrWhiteSpace(topicName))
      return Result.BadRequest("pubsubName and topicName are required.");

    try {
      if (metadata is null)
        await _client.PublishEventAsync(pubsubName, topicName, payload, cancellationToken);
      else
        await _client.PublishEventAsync(
          pubsubName,
          topicName,
          payload,
          metadata as Dictionary<string, string> ?? metadata.ToDictionary(static kv => kv.Key, static kv => kv.Value),
          cancellationToken);
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
  public async Task<Result> PublishByteEventAsync(
    string pubsubName,
    string topicName,
    ReadOnlyMemory<byte> data,
    string contentType = "application/json",
    IReadOnlyDictionary<string, string>? metadata = null,
    CancellationToken cancellationToken = default) {
    if (string.IsNullOrWhiteSpace(pubsubName) || string.IsNullOrWhiteSpace(topicName))
      return Result.BadRequest("pubsubName and topicName are required.");

    try {
      await _client.PublishByteEventAsync(
        pubsubName,
        topicName,
        data,
        contentType,
        metadata as Dictionary<string, string> ?? metadata?.ToDictionary(static kv => kv.Key, static kv => kv.Value),
        cancellationToken);
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
  public async Task<Result<BulkPublishResponse<T>>> BulkPublishEventAsync<T>(
    string pubsubName,
    string topicName,
    IReadOnlyList<T> events,
    Dictionary<string, string>? metadata = null,
    CancellationToken cancellationToken = default) {
    if (string.IsNullOrWhiteSpace(pubsubName) || string.IsNullOrWhiteSpace(topicName))
      return Result<BulkPublishResponse<T>>.BadRequest(default!, "pubsubName and topicName are required.");

    try {
      var response = await _client.BulkPublishEventAsync(pubsubName, topicName, events, metadata, cancellationToken);
      return Result<BulkPublishResponse<T>>.Ok(response);
    }
    catch (OperationCanceledException) {
      throw;
    }
    catch (Exception ex) {
      _logger.LogError(ex, ErrorMessage);
      return Result<BulkPublishResponse<T>>.InternalServerError(default!, [ErrorMessage, .. ex.ExtractMessages()]);
    }
  }
}
