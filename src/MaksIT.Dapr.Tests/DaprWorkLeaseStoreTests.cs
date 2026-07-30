using Dapr.Client;
using MaksIT.Dapr.PubSub;
using MaksIT.Dapr.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;


namespace MaksIT.Dapr.Tests;

public class DaprWorkLeaseStoreTests {
  [Fact]
  public async Task TryAcquireAsync_Succeeds_WhenKeyMissing() {
    var state = new Mock<IDaprStateStoreService>();
    state
      .Setup(s => s.GetStateAndETagAsync<DaprWorkLease>("store", "work", It.IsAny<CancellationToken>()))
      .ReturnsAsync(MaksIT.Results.Result<(DaprWorkLease? Value, string? ETag)>.Ok((null, null)));
    state
      .Setup(s => s.TrySaveStateAsync("store", "work", It.IsAny<DaprWorkLease>(), null, It.IsAny<CancellationToken>()))
      .ReturnsAsync(MaksIT.Results.Result<bool>.Ok(true));

    var store = new DaprWorkLeaseStore(state.Object);
    var result = await store.TryAcquireAsync("store", "work", "pod-a", TimeSpan.FromMinutes(1));

    Assert.True(result.IsSuccess);
    Assert.True(result.Value);
  }

  [Fact]
  public async Task TryAcquireAsync_Fails_WhenHeldByOtherAndNotExpired() {
    var lease = new DaprWorkLease("pod-b", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(5));
    var state = new Mock<IDaprStateStoreService>();
    state
      .Setup(s => s.GetStateAndETagAsync<DaprWorkLease>("store", "work", It.IsAny<CancellationToken>()))
      .ReturnsAsync(MaksIT.Results.Result<(DaprWorkLease? Value, string? ETag)>.Ok((lease, "1")));

    var store = new DaprWorkLeaseStore(state.Object);
    var result = await store.TryAcquireAsync("store", "work", "pod-a", TimeSpan.FromMinutes(1));

    Assert.True(result.IsSuccess);
    Assert.False(result.Value);
  }
}

public class DaprPubSubAckTests {
  [Fact]
  public void ToActionResult_Busy_Returns503() {
    var result = DaprPubSubAck.ToActionResult(DaprPubSubAcceptResult.Busy("full"));
    var objectResult = Assert.IsType<ObjectResult>(result);
    Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
  }

  [Fact]
  public void ToActionResult_Accepted_Returns200() {
    var result = DaprPubSubAck.ToActionResult(DaprPubSubAcceptResult.Accepted());
    Assert.IsType<OkObjectResult>(result);
  }
}

public class DaprStateStoreETagTests {
  [Fact]
  public async Task TrySaveStateAsync_ReturnsOkFalse_WhenClientReturnsFalse() {
    var clientMock = new Mock<DaprClient>();
    clientMock
      .Setup(x => x.TrySaveStateAsync(
        It.IsAny<string>(),
        It.IsAny<string>(),
        It.IsAny<string>(),
        It.IsAny<string>(),
        It.IsAny<StateOptions>(),
        It.IsAny<IReadOnlyDictionary<string, string>>(),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync(false);

    var service = new DaprStateStoreService(Mock.Of<ILogger<DaprStateStoreService>>(), clientMock.Object);
    var result = await service.TrySaveStateAsync("store", "key", "value", "etag");

    Assert.True(result.IsSuccess);
    Assert.False(result.Value);
  }
}
