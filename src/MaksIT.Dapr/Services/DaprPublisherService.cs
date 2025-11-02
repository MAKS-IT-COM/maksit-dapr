using Microsoft.Extensions.Logging;

using Dapr.Client;

using MaksIT.Results;
using MaksIT.Core.Extensions;

namespace MaksIT.Dapr.Services;
public interface IDaprPublisherService {
  Task<Result> PublishEventAsync(string pubsubName, string topicName, object payload);
}

public class DaprPublisherService : IDaprPublisherService {
  private const string _errorMessage = "MaksIT.Dapr - Event publishing error";

  private readonly DaprClient _client;
  private readonly ILogger<DaprPublisherService> _logger;

  public DaprPublisherService(
    ILogger<DaprPublisherService> logger,
    DaprClient client
  ) {
    _logger = logger;
    _client = client;
  }

  public async Task<Result> PublishEventAsync(string pubsubName, string topicName, object payload) {
    try {
      await _client.PublishEventAsync(pubsubName, topicName, payload);
      return Result.Ok();
    }
    catch (Exception ex) {
      _logger.LogError(ex, _errorMessage);
      return Result.InternalServerError([_errorMessage, .. ex.ExtractMessages()]);
    }
  }
}