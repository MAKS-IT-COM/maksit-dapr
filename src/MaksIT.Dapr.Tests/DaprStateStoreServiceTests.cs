using Microsoft.Extensions.Logging;
using Grpc.Core;
using Dapr.Client;
using Moq;
using MaksIT.Dapr.Services;


namespace MaksIT.Dapr.Tests;

public class DaprStateStoreServiceTests {
  [Fact]
  public async Task SetStateAsync_ReturnsOk_WhenSaveSucceeds() {
    var clientMock = new Mock<DaprClient>();
    clientMock
      .Setup(x => x.SaveStateAsync(
        It.IsAny<string>(),
        It.IsAny<string>(),
        It.IsAny<string>(),
        It.IsAny<StateOptions>(),
        It.IsAny<IReadOnlyDictionary<string, string>>(),
        It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    var service = new DaprStateStoreService(
      Mock.Of<ILogger<DaprStateStoreService>>(),
      clientMock.Object);

    var result = await service.SetStateAsync("store", "key", "value");

    Assert.True(result.IsSuccess);
  }

  [Fact]
  public async Task SetStateAsync_ReturnsBadRequest_WhenStoreOrKeyEmpty() {
    var service = new DaprStateStoreService(
      Mock.Of<ILogger<DaprStateStoreService>>(),
      Mock.Of<DaprClient>());

    var result = await service.SetStateAsync(" ", "key", "value");

    Assert.False(result.IsSuccess);
  }

  [Fact]
  public async Task GetStateAsync_ReturnsOk_WhenStateExists() {
    var clientMock = new Mock<DaprClient>();
    clientMock
      .Setup(x => x.GetStateAsync<string?>(
        It.IsAny<string>(),
        It.IsAny<string>(),
        It.IsAny<ConsistencyMode?>(),
        It.IsAny<IReadOnlyDictionary<string, string>>(),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync("value");

    var service = new DaprStateStoreService(
      Mock.Of<ILogger<DaprStateStoreService>>(),
      clientMock.Object);

    var result = await service.GetStateAsync<string>("store", "key");

    Assert.True(result.IsSuccess);
    Assert.Equal("value", result.Value);
  }

  [Fact]
  public async Task GetStateAsync_ReturnsOkNull_WhenStateIsNull() {
    var clientMock = new Mock<DaprClient>();
    clientMock
      .Setup(x => x.GetStateAsync<string?>(
        It.IsAny<string>(),
        It.IsAny<string>(),
        It.IsAny<ConsistencyMode?>(),
        It.IsAny<IReadOnlyDictionary<string, string>>(),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync((string?)null);

    var service = new DaprStateStoreService(
      Mock.Of<ILogger<DaprStateStoreService>>(),
      clientMock.Object);

    var result = await service.GetStateAsync<string>("store", "key");

    Assert.True(result.IsSuccess);
    Assert.Null(result.Value);
  }

  [Fact]
  public async Task DeleteStateAsync_ReturnsInternalServerError_WhenDeleteFails() {
    var clientMock = new Mock<DaprClient>();
    clientMock
      .Setup(x => x.DeleteStateAsync(
        It.IsAny<string>(),
        It.IsAny<string>(),
        It.IsAny<StateOptions>(),
        It.IsAny<IReadOnlyDictionary<string, string>>(),
        It.IsAny<CancellationToken>()))
      .ThrowsAsync(new InvalidOperationException("delete failed"));

    var service = new DaprStateStoreService(
      Mock.Of<ILogger<DaprStateStoreService>>(),
      clientMock.Object);

    var result = await service.DeleteStateAsync("store", "key");

    Assert.False(result.IsSuccess);
  }

  [Fact]
  public async Task DeleteStateAsync_ReturnsOk_WhenJetStreamKeyNotFound() {
    var clientMock = new Mock<DaprClient>();
    clientMock
      .Setup(x => x.DeleteStateAsync(
        It.IsAny<string>(),
        It.IsAny<string>(),
        It.IsAny<StateOptions>(),
        It.IsAny<IReadOnlyDictionary<string, string>>(),
        It.IsAny<CancellationToken>()))
      .ThrowsAsync(CreateJetStreamKeyNotFoundException("missing", "store"));

    var logger = new Mock<ILogger<DaprStateStoreService>>();
    var service = new DaprStateStoreService(logger.Object, clientMock.Object);

    var result = await service.DeleteStateAsync("store", "key");

    Assert.True(result.IsSuccess);
    logger.Verify(
      x => x.Log(
        LogLevel.Error,
        It.IsAny<EventId>(),
        It.IsAny<It.IsAnyType>(),
        It.IsAny<Exception?>(),
        It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
      Times.Never);
  }

  [Fact]
  public async Task GetStateAndETagAsync_ReturnsEmpty_WhenJetStreamKeyNotFound() {
    var clientMock = new Mock<DaprClient>();
    clientMock
      .Setup(x => x.GetStateAndETagAsync<string?>(
        It.IsAny<string>(),
        It.IsAny<string>(),
        It.IsAny<ConsistencyMode?>(),
        It.IsAny<IReadOnlyDictionary<string, string>>(),
        It.IsAny<CancellationToken>()))
      .ThrowsAsync(CreateJetStreamKeyNotFoundException("identity-hub-otc-cleanup", "maksit-identity-hub-state"));

    var logger = new Mock<ILogger<DaprStateStoreService>>();
    var service = new DaprStateStoreService(logger.Object, clientMock.Object);

    var result = await service.GetStateAndETagAsync<string>("store", "key");

    Assert.True(result.IsSuccess);
    Assert.Null(result.Value.Value);
    Assert.Null(result.Value.ETag);
    logger.Verify(
      x => x.Log(
        LogLevel.Error,
        It.IsAny<EventId>(),
        It.IsAny<It.IsAnyType>(),
        It.IsAny<Exception?>(),
        It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
      Times.Never);
  }

  [Fact]
  public async Task GetStateAsync_ReturnsOkNull_WhenJetStreamKeyNotFound() {
    var clientMock = new Mock<DaprClient>();
    clientMock
      .Setup(x => x.GetStateAsync<string?>(
        It.IsAny<string>(),
        It.IsAny<string>(),
        It.IsAny<ConsistencyMode?>(),
        It.IsAny<IReadOnlyDictionary<string, string>>(),
        It.IsAny<CancellationToken>()))
      .ThrowsAsync(CreateJetStreamKeyNotFoundException("missing-key", "store"));

    var logger = new Mock<ILogger<DaprStateStoreService>>();
    var service = new DaprStateStoreService(logger.Object, clientMock.Object);

    var result = await service.GetStateAsync<string>("store", "missing-key");

    Assert.True(result.IsSuccess);
    Assert.Null(result.Value);
    logger.Verify(
      x => x.Log(
        LogLevel.Error,
        It.IsAny<EventId>(),
        It.IsAny<It.IsAnyType>(),
        It.IsAny<Exception?>(),
        It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
      Times.Never);
  }

  [Fact]
  public async Task GetStateAndETagAsync_ReturnsInternalServerError_WhenOtherFailure() {
    var clientMock = new Mock<DaprClient>();
    clientMock
      .Setup(x => x.GetStateAndETagAsync<string?>(
        It.IsAny<string>(),
        It.IsAny<string>(),
        It.IsAny<ConsistencyMode?>(),
        It.IsAny<IReadOnlyDictionary<string, string>>(),
        It.IsAny<CancellationToken>()))
      .ThrowsAsync(new InvalidOperationException("connection refused"));

    var service = new DaprStateStoreService(
      Mock.Of<ILogger<DaprStateStoreService>>(),
      clientMock.Object);

    var result = await service.GetStateAndETagAsync<string>("store", "key");

    Assert.False(result.IsSuccess);
  }

  [Fact]
  public async Task SetStateAsync_Rethrows_WhenCanceled() {
    var clientMock = new Mock<DaprClient>();
    clientMock
      .Setup(x => x.SaveStateAsync(
        It.IsAny<string>(),
        It.IsAny<string>(),
        It.IsAny<string>(),
        It.IsAny<StateOptions>(),
        It.IsAny<IReadOnlyDictionary<string, string>>(),
        It.IsAny<CancellationToken>()))
      .ThrowsAsync(new OperationCanceledException());

    var service = new DaprStateStoreService(
      Mock.Of<ILogger<DaprStateStoreService>>(),
      clientMock.Object);

    await Assert.ThrowsAsync<OperationCanceledException>(() =>
      service.SetStateAsync("store", "key", "value"));
  }

  private static RpcException CreateJetStreamKeyNotFoundException(string key, string storeName) {
    var detail = $"fail to get {key} from state store {storeName}: nats: key not found";
    return new RpcException(new Status(StatusCode.Internal, detail));
  }
}
