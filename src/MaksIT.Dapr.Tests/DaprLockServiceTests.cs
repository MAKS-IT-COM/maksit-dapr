using Microsoft.Extensions.Logging;
using Dapr.Client;
using Moq;
using MaksIT.Dapr.Services;


namespace MaksIT.Dapr.Tests;

#pragma warning disable DAPR_DISTRIBUTEDLOCK

public class DaprLockServiceTests {
  [Fact]
  public async Task LockAsync_ReturnsOk_WhenClientSucceeds() {
    var response = new TryLockResponse();
    var clientMock = new Mock<DaprClient>();
    clientMock
      .Setup(x => x.Lock("lock-store", "resource", "owner", 30, It.IsAny<CancellationToken>()))
      .ReturnsAsync(response);

    var service = new DaprLockService(Mock.Of<ILogger<DaprLockService>>(), clientMock.Object);

    var result = await service.LockAsync("lock-store", "resource", "owner", 30);

    Assert.True(result.IsSuccess);
    Assert.Same(response, result.Value);
  }

  [Fact]
  public async Task LockAsync_ReturnsBadRequest_WhenArgsInvalid() {
    var service = new DaprLockService(Mock.Of<ILogger<DaprLockService>>(), Mock.Of<DaprClient>());

    var empty = await service.LockAsync(" ", "resource", "owner", 30);
    var badExpiry = await service.LockAsync("lock-store", "resource", "owner", 0);

    Assert.False(empty.IsSuccess);
    Assert.False(badExpiry.IsSuccess);
  }

  [Fact]
  public async Task LockAsync_ReturnsInternalServerError_WhenClientFails() {
    var clientMock = new Mock<DaprClient>();
    clientMock
      .Setup(x => x.Lock(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new InvalidOperationException("lock failed"));

    var service = new DaprLockService(Mock.Of<ILogger<DaprLockService>>(), clientMock.Object);

    var result = await service.LockAsync("lock-store", "resource", "owner", 30);

    Assert.False(result.IsSuccess);
  }

  [Fact]
  public async Task LockAsync_Rethrows_WhenCanceled() {
    var clientMock = new Mock<DaprClient>();
    clientMock
      .Setup(x => x.Lock(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new OperationCanceledException());

    var service = new DaprLockService(Mock.Of<ILogger<DaprLockService>>(), clientMock.Object);

    await Assert.ThrowsAsync<OperationCanceledException>(() =>
      service.LockAsync("lock-store", "resource", "owner", 30));
  }

  [Fact]
  public async Task UnlockAsync_ReturnsOk_WhenClientSucceeds() {
    var response = new UnlockResponse(LockStatus.Success);
    var clientMock = new Mock<DaprClient>();
    clientMock
      .Setup(x => x.Unlock("lock-store", "resource", "owner", It.IsAny<CancellationToken>()))
      .ReturnsAsync(response);

    var service = new DaprLockService(Mock.Of<ILogger<DaprLockService>>(), clientMock.Object);

    var result = await service.UnlockAsync("lock-store", "resource", "owner");

    Assert.True(result.IsSuccess);
    Assert.Same(response, result.Value);
  }

  [Fact]
  public async Task UnlockAsync_ReturnsBadRequest_WhenArgsEmpty() {
    var service = new DaprLockService(Mock.Of<ILogger<DaprLockService>>(), Mock.Of<DaprClient>());

    var result = await service.UnlockAsync("lock-store", " ", "owner");

    Assert.False(result.IsSuccess);
  }
}

#pragma warning restore DAPR_DISTRIBUTEDLOCK
