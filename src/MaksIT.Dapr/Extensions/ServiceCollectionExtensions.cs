using Microsoft.Extensions.DependencyInjection;

namespace MaksIT.Dapr.Extensions;
public static class ServiceCollectionExtensions {
  public static void RegisterPublisher(this IServiceCollection services) {
    services.AddDaprClient();
    services.AddSingleton<IDaprPublisherService, DaprService>();
  }

  public static void RegisterStateStore(this IServiceCollection services) {
    services.AddDaprClient();
    services.AddSingleton<IDaprStateStoreService, DaprService>();
  }
}