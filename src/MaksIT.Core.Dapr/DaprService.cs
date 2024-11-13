using Microsoft.Extensions.Logging;

using Dapr.Client;
using MaksIT.Results;

namespace MaksIT.Core.Dapr;
public interface IDaprService {
  Task<Result> PublishEventAsync(string pubSubName, string topicName, string payload);
}

public class DaprService : IDaprService {
  private const string _errorMessage = "MaksIT.Core.Dapr - Data provider error";

  private readonly DaprClient _client;
  private readonly ILogger<DaprService> _logger;

  public DaprService(
    ILogger<DaprService> logger,
    DaprClient client
  ) {
    _logger = logger;
    _client = client;
  }

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
}