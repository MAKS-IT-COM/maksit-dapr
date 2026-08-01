using MaksIT.Dapr.Services.WorkLease;


namespace MaksIT.Dapr.Tests;

public class DaprRuntimeInstanceIdProviderTests {
  [Fact]
  public void InstanceId_IsNonEmpty() {
    var provider = new DaprRuntimeInstanceIdProvider();

    Assert.False(string.IsNullOrWhiteSpace(provider.InstanceId));
  }
}
