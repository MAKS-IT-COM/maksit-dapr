using Microsoft.Extensions.Logging;
using Dapr.Client;
using Moq;
using MaksIT.Dapr.Services;


namespace MaksIT.Dapr.Tests;

public class DaprPubSubServiceTests {
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

    var service = new DaprPubSubService(
      Mock.Of<ILogger<DaprPubSubService>>(),
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

    var service = new DaprPubSubService(
      Mock.Of<ILogger<DaprPubSubService>>(),
      clientMock.Object);
    object payload = new { Name = "payload" };

    var result = await service.PublishEventAsync("pubsub", "topic", payload);

    Assert.False(result.IsSuccess);
  }

  [Fact]
  public async Task PublishEventAsync_ReturnsBadRequest_WhenPubsubOrTopicEmpty() {
    var service = new DaprPubSubService(
      Mock.Of<ILogger<DaprPubSubService>>(),
      Mock.Of<DaprClient>());

    var result = await service.PublishEventAsync(" ", "topic", new { });

    Assert.False(result.IsSuccess);
  }

  [Fact]
  public async Task PublishEventAsync_Rethrows_WhenCanceled() {
    var clientMock = new Mock<DaprClient>();
    clientMock
      .Setup(x => x.PublishEventAsync(
        It.IsAny<string>(),
        It.IsAny<string>(),
        It.IsAny<object>(),
        It.IsAny<CancellationToken>()))
      .ThrowsAsync(new OperationCanceledException());

    var service = new DaprPubSubService(
      Mock.Of<ILogger<DaprPubSubService>>(),
      clientMock.Object);

    await Assert.ThrowsAsync<OperationCanceledException>(() =>
      service.PublishEventAsync("pubsub", "topic", new { }));
  }
}
