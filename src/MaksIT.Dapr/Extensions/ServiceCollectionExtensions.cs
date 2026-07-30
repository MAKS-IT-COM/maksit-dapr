using MaksIT.Dapr.Services;
using Microsoft.Extensions.DependencyInjection;


namespace MaksIT.Dapr.Extensions;

public static class ServiceCollectionExtensions {
  private static bool _isDaprClientRegistered;

  private static void AddDaprClientOnce(this IServiceCollection services) {
    if (_isDaprClientRegistered)
      return;

    services.AddDaprClient();
    _isDaprClientRegistered = true;
  }

  public static void RegisterPublisher(this IServiceCollection services) {
    services.AddDaprClientOnce();
    services.AddSingleton<IDaprPublisherService, DaprPublisherService>();
  }

  public static void RegisterStateStore(this IServiceCollection services) {
    services.AddDaprClientOnce();
    services.AddSingleton<IDaprStateStoreService, DaprStateStoreService>();
  }

  /// <summary>
  /// Registers Dapr state store plus HA work-lease coordination and runtime instance id.
  /// </summary>
  public static void RegisterWorkLeases(this IServiceCollection services) {
    services.RegisterStateStore();
    services.AddSingleton<IDaprRuntimeInstanceId, DaprRuntimeInstanceIdProvider>();
    services.AddSingleton<IDaprWorkLeaseStore, DaprWorkLeaseStore>();
  }
}
