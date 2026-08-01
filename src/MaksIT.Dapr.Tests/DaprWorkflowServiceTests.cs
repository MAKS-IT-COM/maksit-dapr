using Microsoft.Extensions.Logging;
using Dapr.Workflow;
using Moq;
using MaksIT.Dapr.Services;


namespace MaksIT.Dapr.Tests;

public class DaprWorkflowServiceTests {
  [Fact]
  public async Task ScheduleAsync_ReturnsBadRequest_WhenWorkflowNameEmpty() {
    var service = new DaprWorkflowService(
      Mock.Of<ILogger<DaprWorkflowService>>(),
      Mock.Of<IDaprWorkflowClient>());

    var result = await service.ScheduleAsync(" ");

    Assert.False(result.IsSuccess);
  }

  [Fact]
  public async Task GetStateAsync_ReturnsBadRequest_WhenInstanceIdEmpty() {
    var service = new DaprWorkflowService(
      Mock.Of<ILogger<DaprWorkflowService>>(),
      Mock.Of<IDaprWorkflowClient>());

    var result = await service.GetStateAsync(" ");

    Assert.False(result.IsSuccess);
  }

  [Fact]
  public async Task RaiseEventAsync_ReturnsBadRequest_WhenEventNameEmpty() {
    var service = new DaprWorkflowService(
      Mock.Of<ILogger<DaprWorkflowService>>(),
      Mock.Of<IDaprWorkflowClient>());

    var result = await service.RaiseEventAsync("instance-1", " ");

    Assert.False(result.IsSuccess);
  }

  [Fact]
  public async Task TerminateAsync_ReturnsBadRequest_WhenInstanceIdEmpty() {
    var service = new DaprWorkflowService(
      Mock.Of<ILogger<DaprWorkflowService>>(),
      Mock.Of<IDaprWorkflowClient>());

    var result = await service.TerminateAsync(" ");

    Assert.False(result.IsSuccess);
  }

  [Fact]
  public async Task ScheduleAsync_ReturnsOk_WhenClientSucceeds() {
    var client = new Mock<IDaprWorkflowClient>();
    client
      .Setup(c => c.ScheduleNewWorkflowAsync("OrderFlow", null, null, null, It.IsAny<CancellationToken>()))
      .ReturnsAsync("instance-1");

    var service = new DaprWorkflowService(
      Mock.Of<ILogger<DaprWorkflowService>>(),
      client.Object);

    var result = await service.ScheduleAsync("OrderFlow");

    Assert.True(result.IsSuccess);
    Assert.Equal("instance-1", result.Value);
  }
}
