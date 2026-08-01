using Microsoft.Extensions.Logging;
using Dapr.Actors;
using Dapr.Actors.Client;
using Moq;
using MaksIT.Dapr.Services;


namespace MaksIT.Dapr.Tests;

public class DaprActorServiceTests {
  [Fact]
  public void Create_ReturnsBadRequest_WhenActorIdEmpty() {
    var service = new DaprActorService(
      Mock.Of<ILogger<DaprActorService>>(),
      Mock.Of<IActorProxyFactory>());

    var result = service.Create(" ", "MyActor");

    Assert.False(result.IsSuccess);
  }

  [Fact]
  public void Create_ReturnsOk_WhenFactorySucceeds() {
    var actor = ActorProxy.Create(new ActorId("1"), "MyActor");
    var factory = new Mock<IActorProxyFactory>();
    factory
      .Setup(f => f.Create(It.IsAny<ActorId>(), "MyActor", null))
      .Returns(actor);

    var service = new DaprActorService(
      Mock.Of<ILogger<DaprActorService>>(),
      factory.Object);

    var result = service.Create("1", "MyActor");

    Assert.True(result.IsSuccess);
    Assert.NotNull(result.Value);
  }

  [Fact]
  public async Task InvokeAsync_ReturnsBadRequest_WhenMethodEmpty() {
    var service = new DaprActorService(
      Mock.Of<ILogger<DaprActorService>>(),
      Mock.Of<IActorProxyFactory>());

    var result = await service.InvokeAsync("1", "MyActor", " ");

    Assert.False(result.IsSuccess);
  }

  [Fact]
  public async Task InvokeAsync_ReturnsInternalServerError_WhenFactoryThrows() {
    var factory = new Mock<IActorProxyFactory>();
    factory
      .Setup(f => f.Create(It.IsAny<ActorId>(), "MyActor", null))
      .Throws(new InvalidOperationException("sidecar unavailable"));

    var service = new DaprActorService(
      Mock.Of<ILogger<DaprActorService>>(),
      factory.Object);

    var result = await service.InvokeAsync("1", "MyActor", "DoWork");

    Assert.False(result.IsSuccess);
  }
}
