using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Dapr.Client;
using Moq;
using MaksIT.Results;
using MaksIT.Dapr.Services;
using MaksIT.Dapr.Services.WorkLease;


namespace MaksIT.Dapr.Tests;

public class DaprWorkLeaseServiceTests {
  private static Mock<IDaprStateStoreService> CreateStateMock() => new();

  [Fact]
  public async Task TryAcquireAsync_Succeeds_WhenKeyMissing() {
    var state = CreateStateMock();
    state
      .Setup(s => s.GetStateAndETagAsync<DaprWorkLease>(
        "store",
        "work",
        It.IsAny<ConsistencyMode?>(),
        It.IsAny<IReadOnlyDictionary<string, string>?>(),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync(Result<(DaprWorkLease? Value, string? ETag)>.Ok((null, null)));
    state
      .Setup(s => s.TrySaveStateAsync(
        "store",
        "work",
        It.IsAny<DaprWorkLease>(),
        null,
        It.IsAny<StateOptions?>(),
        It.IsAny<IReadOnlyDictionary<string, string>?>(),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync(Result<bool>.Ok(true));

    var service = new DaprWorkLeaseService(state.Object);
    var result = await service.TryAcquireAsync("store", "work", "pod-a", TimeSpan.FromMinutes(1));

    Assert.True(result.IsSuccess);
    Assert.True(result.Value);
  }

  [Fact]
  public async Task TryAcquireAsync_Fails_WhenHeldByOtherAndNotExpired() {
    var lease = new DaprWorkLease("pod-b", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(5), 3);
    var state = CreateStateMock();
    state
      .Setup(s => s.GetStateAndETagAsync<DaprWorkLease>(
        "store",
        "work",
        It.IsAny<ConsistencyMode?>(),
        It.IsAny<IReadOnlyDictionary<string, string>?>(),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync(Result<(DaprWorkLease? Value, string? ETag)>.Ok((lease, "1")));

    var service = new DaprWorkLeaseService(state.Object);
    var result = await service.TryAcquireAsync("store", "work", "pod-a", TimeSpan.FromMinutes(1));

    Assert.True(result.IsSuccess);
    Assert.False(result.Value);
  }

  [Fact]
  public async Task TryAcquireAsync_TakesOver_WhenLeaseExpired_AndBumpsGeneration() {
    var lease = new DaprWorkLease("pod-b", DateTimeOffset.UtcNow.AddMinutes(-10), DateTimeOffset.UtcNow.AddMinutes(-5), 7);
    DaprWorkLease? saved = null;
    var state = CreateStateMock();
    state
      .Setup(s => s.GetStateAndETagAsync<DaprWorkLease>(
        "store",
        "work",
        It.IsAny<ConsistencyMode?>(),
        It.IsAny<IReadOnlyDictionary<string, string>?>(),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync(Result<(DaprWorkLease? Value, string? ETag)>.Ok((lease, "1")));
    state
      .Setup(s => s.TrySaveStateAsync(
        "store",
        "work",
        It.IsAny<DaprWorkLease>(),
        "1",
        It.IsAny<StateOptions?>(),
        It.IsAny<IReadOnlyDictionary<string, string>?>(),
        It.IsAny<CancellationToken>()))
      .Callback<string, string, DaprWorkLease, string?, StateOptions?, IReadOnlyDictionary<string, string>?, CancellationToken>(
        (_, _, value, _, _, _, _) => saved = value)
      .ReturnsAsync(Result<bool>.Ok(true));

    var service = new DaprWorkLeaseService(state.Object);
    var result = await service.TryAcquireAsync("store", "work", "pod-a", TimeSpan.FromMinutes(1));

    Assert.True(result.IsSuccess);
    Assert.True(result.Value);
    Assert.Equal(8, saved!.Generation);
  }

  [Fact]
  public async Task TryAcquireAsync_ReturnsBadRequest_WhenTtlNonPositive() {
    var service = new DaprWorkLeaseService(Mock.Of<IDaprStateStoreService>());
    var result = await service.TryAcquireAsync("store", "work", "pod-a", TimeSpan.Zero);

    Assert.False(result.IsSuccess);
  }

  [Fact]
  public async Task TryRenewAsync_Succeeds_WhenSameHolder() {
    var lease = new DaprWorkLease("pod-a", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(1), 2);
    var state = CreateStateMock();
    state
      .Setup(s => s.GetStateAndETagAsync<DaprWorkLease>(
        "store",
        "work",
        It.IsAny<ConsistencyMode?>(),
        It.IsAny<IReadOnlyDictionary<string, string>?>(),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync(Result<(DaprWorkLease? Value, string? ETag)>.Ok((lease, "2")));
    state
      .Setup(s => s.TrySaveStateAsync(
        "store",
        "work",
        It.IsAny<DaprWorkLease>(),
        "2",
        It.IsAny<StateOptions?>(),
        It.IsAny<IReadOnlyDictionary<string, string>?>(),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync(Result<bool>.Ok(true));

    var service = new DaprWorkLeaseService(state.Object);
    var result = await service.TryRenewAsync("store", "work", "pod-a", TimeSpan.FromMinutes(5));

    Assert.True(result.IsSuccess);
    Assert.True(result.Value);
  }

  [Fact]
  public async Task TryRenewAsync_Fails_WhenDifferentHolder() {
    var lease = new DaprWorkLease("pod-b", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(1));
    var state = CreateStateMock();
    state
      .Setup(s => s.GetStateAndETagAsync<DaprWorkLease>(
        "store",
        "work",
        It.IsAny<ConsistencyMode?>(),
        It.IsAny<IReadOnlyDictionary<string, string>?>(),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync(Result<(DaprWorkLease? Value, string? ETag)>.Ok((lease, "2")));

    var service = new DaprWorkLeaseService(state.Object);
    var result = await service.TryRenewAsync("store", "work", "pod-a", TimeSpan.FromMinutes(5));

    Assert.True(result.IsSuccess);
    Assert.False(result.Value);
  }

  [Fact]
  public async Task ReleaseAsync_Deletes_WhenSameHolder() {
    var lease = new DaprWorkLease("pod-a", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(1));
    var state = CreateStateMock();
    state
      .Setup(s => s.GetStateAndETagAsync<DaprWorkLease>(
        "store",
        "work",
        It.IsAny<ConsistencyMode?>(),
        It.IsAny<IReadOnlyDictionary<string, string>?>(),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync(Result<(DaprWorkLease? Value, string? ETag)>.Ok((lease, "3")));
    state
      .Setup(s => s.DeleteStateAsync(
        "store",
        "work",
        It.IsAny<StateOptions?>(),
        It.IsAny<IReadOnlyDictionary<string, string>?>(),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync(Result.Ok());

    var service = new DaprWorkLeaseService(state.Object);
    var result = await service.ReleaseAsync("store", "work", "pod-a");

    Assert.True(result.IsSuccess);
    state.Verify(s => s.DeleteStateAsync(
      "store",
      "work",
      It.IsAny<StateOptions?>(),
      It.IsAny<IReadOnlyDictionary<string, string>?>(),
      It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task ReleaseAsync_ReturnsConflict_WhenDifferentHolder() {
    var lease = new DaprWorkLease("pod-b", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(1));
    var state = CreateStateMock();
    state
      .Setup(s => s.GetStateAndETagAsync<DaprWorkLease>(
        "store",
        "work",
        It.IsAny<ConsistencyMode?>(),
        It.IsAny<IReadOnlyDictionary<string, string>?>(),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync(Result<(DaprWorkLease? Value, string? ETag)>.Ok((lease, "3")));

    var service = new DaprWorkLeaseService(state.Object);
    var result = await service.ReleaseAsync("store", "work", "pod-a");

    Assert.False(result.IsSuccess);
  }

  [Fact]
  public async Task ReleaseAsync_ReturnsOk_WhenKeyMissing() {
    var state = CreateStateMock();
    state
      .Setup(s => s.GetStateAndETagAsync<DaprWorkLease>(
        "store",
        "work",
        It.IsAny<ConsistencyMode?>(),
        It.IsAny<IReadOnlyDictionary<string, string>?>(),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync(Result<(DaprWorkLease? Value, string? ETag)>.Ok((null, null)));

    var service = new DaprWorkLeaseService(state.Object);
    var result = await service.ReleaseAsync("store", "work", "pod-a");

    Assert.True(result.IsSuccess);
  }

  [Fact]
  public async Task GetAsync_ReturnsLease_WhenPresent() {
    var lease = new DaprWorkLease("pod-a", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(1), 4);
    var state = CreateStateMock();
    state
      .Setup(s => s.GetStateAndETagAsync<DaprWorkLease>(
        "store",
        "work",
        It.IsAny<ConsistencyMode?>(),
        It.IsAny<IReadOnlyDictionary<string, string>?>(),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync(Result<(DaprWorkLease? Value, string? ETag)>.Ok((lease, "1")));

    var service = new DaprWorkLeaseService(state.Object);
    var result = await service.GetAsync("store", "work");

    Assert.True(result.IsSuccess);
    Assert.Equal("pod-a", result.Value!.HolderId);
    Assert.Equal(4, result.Value.Generation);
  }

  [Fact]
  public async Task TryHoldAsync_ReturnsHold_WhenAcquired() {
    var state = CreateStateMock();
    DaprWorkLease? saved = null;
    state
      .Setup(s => s.GetStateAndETagAsync<DaprWorkLease>(
        "store",
        "work",
        It.IsAny<ConsistencyMode?>(),
        It.IsAny<IReadOnlyDictionary<string, string>?>(),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync(() => Result<(DaprWorkLease? Value, string? ETag)>.Ok((saved, saved is null ? null : "1")));
    state
      .Setup(s => s.TrySaveStateAsync(
        "store",
        "work",
        It.IsAny<DaprWorkLease>(),
        It.IsAny<string?>(),
        It.IsAny<StateOptions?>(),
        It.IsAny<IReadOnlyDictionary<string, string>?>(),
        It.IsAny<CancellationToken>()))
      .Callback<string, string, DaprWorkLease, string?, StateOptions?, IReadOnlyDictionary<string, string>?, CancellationToken>(
        (_, _, value, _, _, _, _) => saved = value)
      .ReturnsAsync(Result<bool>.Ok(true));
    state
      .Setup(s => s.DeleteStateAsync(
        "store",
        "work",
        It.IsAny<StateOptions?>(),
        It.IsAny<IReadOnlyDictionary<string, string>?>(),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync(Result.Ok());

    var service = new DaprWorkLeaseService(state.Object);
    var holdResult = await service.TryHoldAsync("store", "work", "pod-a", TimeSpan.FromMinutes(1), autoRenew: false);

    Assert.True(holdResult.IsSuccess);
    Assert.NotNull(holdResult.Value);
    Assert.Equal(1, holdResult.Value!.Generation);

    await holdResult.Value.DisposeAsync();
    state.Verify(s => s.DeleteStateAsync(
      "store",
      "work",
      It.IsAny<StateOptions?>(),
      It.IsAny<IReadOnlyDictionary<string, string>?>(),
      It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task TryHoldAsync_ReturnsNull_WhenBusy() {
    var lease = new DaprWorkLease("pod-b", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(5), 1);
    var state = CreateStateMock();
    state
      .Setup(s => s.GetStateAndETagAsync<DaprWorkLease>(
        "store",
        "work",
        It.IsAny<ConsistencyMode?>(),
        It.IsAny<IReadOnlyDictionary<string, string>?>(),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync(Result<(DaprWorkLease? Value, string? ETag)>.Ok((lease, "1")));

    var service = new DaprWorkLeaseService(state.Object);
    var holdResult = await service.TryHoldAsync("store", "work", "pod-a", TimeSpan.FromMinutes(1), autoRenew: false);

    Assert.True(holdResult.IsSuccess);
    Assert.Null(holdResult.Value);
  }
}

public class DaprWorkLeaseBootstrapTests {
  [Fact]
  public async Task RunBootstrapUnderLeaseAsync_RunsBootstrap_WhenLeaseAcquired() {
    var leases = new Mock<IDaprWorkLeaseService>();
    var hold = new DaprWorkLeaseHold(leases.Object, "store", "boot", "pod-a", TimeSpan.FromMinutes(1), 1, autoRenew: false);
    leases
      .Setup(l => l.TryHoldAsync("store", "boot", "pod-a", It.IsAny<TimeSpan>(), true, It.IsAny<CancellationToken>()))
      .ReturnsAsync(Result<DaprWorkLeaseHold?>.Ok(hold));
    leases
      .Setup(l => l.ReleaseAsync("store", "boot", "pod-a", It.IsAny<CancellationToken>()))
      .ReturnsAsync(Result.Ok());

    var ran = false;
    var result = await DaprWorkLeaseBootstrap.RunBootstrapUnderLeaseAsync(
      leases.Object,
      "store",
      "boot",
      "pod-a",
      TimeSpan.FromMinutes(1),
      _ => {
        ran = true;
        return Task.FromResult(Result.Ok());
      },
      _ => Task.FromResult(Result<bool>.Ok(true)));

    Assert.True(result.IsSuccess);
    Assert.True(ran);
  }

  [Fact]
  public async Task RunBootstrapUnderLeaseAsync_WaitsForReady_WhenFollower() {
    var leases = new Mock<IDaprWorkLeaseService>();
    leases
      .Setup(l => l.TryHoldAsync("store", "boot", "pod-b", It.IsAny<TimeSpan>(), true, It.IsAny<CancellationToken>()))
      .ReturnsAsync(Result<DaprWorkLeaseHold?>.Ok(null));

    var polls = 0;
    var result = await DaprWorkLeaseBootstrap.RunBootstrapUnderLeaseAsync(
      leases.Object,
      "store",
      "boot",
      "pod-b",
      TimeSpan.FromMinutes(1),
      _ => Task.FromResult(Result.Ok()),
      _ => {
        polls++;
        return Task.FromResult(Result<bool>.Ok(polls >= 2));
      },
      followerPollInterval: TimeSpan.FromMilliseconds(1));

    Assert.True(result.IsSuccess);
    Assert.True(polls >= 2);
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

public class DaprInvocationServiceTests {
  [Fact]
  public async Task InvokeAsync_ReturnsBadRequest_WhenAppIdEmpty() {
    var service = new DaprInvocationService(Mock.Of<ILogger<DaprInvocationService>>(), Mock.Of<DaprClient>());
    var result = await service.InvokeAsync(" ", "method");
    Assert.False(result.IsSuccess);
  }

  [Fact]
  public async Task InvokeAsync_PostsViaInvokableHttpClient_AndReturnsOk() {
    var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK));
    using var http = new HttpClient(handler) { BaseAddress = new Uri("http://orders/") };

    var client = new Mock<DaprClient>();
    client.Setup(c => c.CreateInvokableHttpClient("orders")).Returns(http);
    client.SetupGet(c => c.JsonSerializerOptions).Returns(new JsonSerializerOptions(JsonSerializerDefaults.Web));

    using var service = new DaprInvocationService(Mock.Of<ILogger<DaprInvocationService>>(), client.Object);
    var result = await service.InvokeAsync("orders", "create", new { Id = 1 });

    Assert.True(result.IsSuccess);
    Assert.Equal(HttpMethod.Post, handler.LastMethod);
    Assert.Equal(new Uri("http://orders/create"), handler.LastUri);
  }

  [Fact]
  public async Task InvokeAsync_DeserializesJsonResponse() {
    var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK) {
      Content = new StringContent("""{"total":42}""", Encoding.UTF8, "application/json")
    });
    using var http = new HttpClient(handler) { BaseAddress = new Uri("http://orders/") };

    var client = new Mock<DaprClient>();
    client.Setup(c => c.CreateInvokableHttpClient("orders")).Returns(http);
    client.SetupGet(c => c.JsonSerializerOptions).Returns(new JsonSerializerOptions(JsonSerializerDefaults.Web));

    using var service = new DaprInvocationService(Mock.Of<ILogger<DaprInvocationService>>(), client.Object);
    var result = await service.InvokeAsync<OrderTotal>("orders", "total");

    Assert.True(result.IsSuccess);
    Assert.Equal(42, result.Value?.Total);
  }

  private sealed class OrderTotal {
    public int Total { get; set; }
  }

  private sealed class RecordingHandler(HttpResponseMessage response) : HttpMessageHandler {
    public HttpMethod? LastMethod { get; private set; }
    public Uri? LastUri { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
      LastMethod = request.Method;
      LastUri = request.RequestUri;
      return Task.FromResult(response);
    }
  }
}

public class DaprSidecarServiceTests {
  [Fact]
  public async Task CheckHealthAsync_ReturnsOk_WhenHealthy() {
    var client = new Mock<DaprClient>();
    client.Setup(c => c.CheckHealthAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

    var service = new DaprSidecarService(Mock.Of<ILogger<DaprSidecarService>>(), client.Object);
    var result = await service.CheckHealthAsync();

    Assert.True(result.IsSuccess);
    Assert.True(result.Value);
  }
}

public class DaprSecretServiceTests {
  [Fact]
  public async Task GetAsync_ReturnsBadRequest_WhenStoreEmpty() {
    var service = new DaprSecretService(Mock.Of<ILogger<DaprSecretService>>(), Mock.Of<DaprClient>());
    var result = await service.GetAsync(" ", "key");
    Assert.False(result.IsSuccess);
  }
}

public class DaprBindingServiceTests {
  [Fact]
  public async Task InvokeAsync_ReturnsBadRequest_WhenBindingEmpty() {
    var service = new DaprBindingService(Mock.Of<ILogger<DaprBindingService>>(), Mock.Of<DaprClient>());
    var result = await service.InvokeAsync(" ", "create", new { });
    Assert.False(result.IsSuccess);
  }
}
