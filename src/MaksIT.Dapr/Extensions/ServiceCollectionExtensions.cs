using MaksIT.Dapr.Services;
using Microsoft.Extensions.DependencyInjection;


namespace MaksIT.Dapr.Extensions;

public static class ServiceCollectionExtensions {
  private static bool _isDaprClientRegistered = false;

  private static void AddDaprClientOnce(this IServiceCollection services) {
    if (!_isDaprClientRegistered) {
      services.AddDaprClient();
      _isDaprClientRegistered = true;
    }
  }

  public static void RegisterPublisher(this IServiceCollection services) {
    services.AddDaprClientOnce();
    services.AddSingleton<IDaprPublisherService, DaprPublisherService>();
  }

  public static void RegisterStateStore(this IServiceCollection services) {
    services.AddDaprClientOnce();
    services.AddSingleton<IDaprStateStoreService, DaprStateStoreService>();
  }
}