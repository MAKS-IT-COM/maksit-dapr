using Dapr.Client;
using MaksIT.Dapr.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace MaksIT.Dapr.Tests;

public class DaprPublisherServiceTests {
  [Fact]
  public async Task PublishEventAsync_ReturnsOk_WhenPublishSucceeds() {
    var clientMock = new Mock<DaprClient>();
    clientMock
      .Setup(x => x.PublishEventAsync(
        It.IsAny<string>(),
        It.IsAny<string>(),
        It.IsAny<object>(),
        It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    var service = new DaprPublisherService(
      Mock.Of<ILogger<DaprPublisherService>>(),
      clientMock.Object);
    object payload = new { Name = "payload" };

    var result = await service.PublishEventAsync("pubsub", "topic", payload);

    Assert.True(result.IsSuccess);
  }

  [Fact]
  public async Task PublishEventAsync_ReturnsInternalServerError_WhenPublishFails() {
    var clientMock = new Mock<DaprClient>();
    clientMock
      .Setup(x => x.PublishEventAsync(
        It.IsAny<string>(),
        It.IsAny<string>(),
        It.IsAny<object>(),
        It.IsAny<CancellationToken>()))
      .ThrowsAsync(new InvalidOperationException("publish failed"));

    var service = new DaprPublisherService(
      Mock.Of<ILogger<DaprPublisherService>>(),
      clientMock.Object);
    object payload = new { Name = "payload" };

    var result = await service.PublishEventAsync("pubsub", "topic", payload);

    Assert.False(result.IsSuccess);
  }
}
