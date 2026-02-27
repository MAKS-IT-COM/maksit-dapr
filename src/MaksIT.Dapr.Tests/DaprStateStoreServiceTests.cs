using Dapr.Client;
using MaksIT.Dapr.Services;
using Microsoft.Extensions.Logging;
using Moq;

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
  public async Task GetStateAsync_ReturnsNotFound_WhenStateIsNull() {
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

    Assert.False(result.IsSuccess);
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
}
