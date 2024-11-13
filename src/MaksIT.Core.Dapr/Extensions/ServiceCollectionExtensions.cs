using Microsoft.Extensions.DependencyInjection;

namespace MaksIT.Core.Dapr.Extensions;
public static class ServiceCollectionExtensions {
  public static void RegisterPublisher(this IServiceCollection services) {
    services.AddDaprClient();
    services.AddSingleton<IDaprService, DaprService>();
  }
}