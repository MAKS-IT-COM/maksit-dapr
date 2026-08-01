using Microsoft.Extensions.DependencyInjection;
using Dapr.Actors.Client;
using Dapr.Client;
using MaksIT.Dapr.Extensions;
using MaksIT.Dapr.Services;
using MaksIT.Dapr.Services.WorkLease;


namespace MaksIT.Dapr.Tests;

public class ServiceCollectionExtensionsTests {
  [Fact]
  public void RegisterPubSub_AndStateStore_RegisterSingleDaprClient() {
    var services = new ServiceCollection();
    services.AddLogging();

    services.RegisterPubSub();
    services.RegisterStateStore();

    Assert.Equal(1, services.Count(d => d.ServiceType == typeof(DaprClient)));
    Assert.Contains(services, d => d.ServiceType == typeof(IDaprPubSubService));
    Assert.Contains(services, d => d.ServiceType == typeof(IDaprStateStoreService));
  }

  [Fact]
  public void RegisterDaprClientFacades_RegistersClientBackedServices() {
    var services = new ServiceCollection();
    services.AddLogging();

    services.RegisterDaprClientFacades();

    Assert.Equal(1, services.Count(d => d.ServiceType == typeof(DaprClient)));
    Assert.Contains(services, d => d.ServiceType == typeof(IDaprPubSubService));
    Assert.Contains(services, d => d.ServiceType == typeof(IDaprStateStoreService));
    Assert.Contains(services, d => d.ServiceType == typeof(IDaprInvocationService));
    Assert.Contains(services, d => d.ServiceType == typeof(IDaprBindingService));
    Assert.Contains(services, d => d.ServiceType == typeof(IDaprSecretService));
    Assert.Contains(services, d => d.ServiceType == typeof(IDaprConfigurationService));
    Assert.Contains(services, d => d.ServiceType == typeof(IDaprCryptographyService));
    Assert.Contains(services, d => d.ServiceType == typeof(IDaprSidecarService));
    Assert.Contains(services, d => d.ServiceType == typeof(IDaprLockService));
  }

  [Fact]
  public void RegisterLock_RegistersLockService() {
    var services = new ServiceCollection();
    services.AddLogging();

    services.RegisterLock();

    Assert.Contains(services, d => d.ServiceType == typeof(IDaprLockService));
    Assert.Equal(1, services.Count(d => d.ServiceType == typeof(DaprClient)));
  }

  [Fact]
  public void RegisterWorkLeases_RegistersLeaseAndInstanceId() {
    var services = new ServiceCollection();
    services.AddLogging();

    services.RegisterWorkLeases();

    Assert.Contains(services, d => d.ServiceType == typeof(IDaprWorkLeaseService));
    Assert.Contains(services, d => d.ServiceType == typeof(IDaprRuntimeInstanceId));
    Assert.Contains(services, d => d.ServiceType == typeof(IDaprStateStoreService));
  }

  [Fact]
  public void RegisterWorkLeases_WithStoreName_RegistersOptions() {
    var services = new ServiceCollection();
    services.AddLogging();

    services.RegisterWorkLeases("my-state");

    var provider = services.BuildServiceProvider();
    var options = provider.GetRequiredService<IDaprWorkLeaseOptions>();
    Assert.Equal("my-state", options.StoreName);
  }

  [Fact]
  public void AddDaprClientOnce_IsPerCollection_NotProcessWide() {
    var first = new ServiceCollection();
    first.AddLogging();
    first.RegisterStateStore();

    var second = new ServiceCollection();
    second.AddLogging();
    second.RegisterStateStore();

    Assert.Equal(1, first.Count(d => d.ServiceType == typeof(DaprClient)));
    Assert.Equal(1, second.Count(d => d.ServiceType == typeof(DaprClient)));
  }

  [Fact]
  public void RegisterActors_RegistersActorService() {
    var services = new ServiceCollection();
    services.AddLogging();

    services.RegisterActors();

    Assert.Contains(services, d => d.ServiceType == typeof(IDaprActorService));
    Assert.Contains(services, d => d.ServiceType == typeof(IActorProxyFactory));
  }

  [Fact]
  public void RegisterWorkflows_RegistersWorkflowService() {
    var services = new ServiceCollection();
    services.AddLogging();

    services.RegisterWorkflows();

    Assert.Contains(services, d => d.ServiceType == typeof(IDaprWorkflowService));
  }
}
