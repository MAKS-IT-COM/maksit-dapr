using Microsoft.Extensions.DependencyInjection;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;
using Dapr.Client;
using Dapr.Workflow;
using MaksIT.Dapr.Services;
using MaksIT.Dapr.Services.WorkLease;


namespace MaksIT.Dapr.Extensions;

/// <summary>
/// DI registration helpers for MaksIT.Dapr services.
/// </summary>
public static class ServiceCollectionExtensions {
  private static void AddDaprClientOnce(this IServiceCollection services) {
    if (services.Any(d => d.ServiceType == typeof(DaprClient)))
      return;

    services.AddDaprClient();
  }

  /// <summary>
  /// Registers <see cref="IDaprPubSubService"/> and a <see cref="DaprClient"/> when missing.
  /// </summary>
  public static void RegisterPubSub(this IServiceCollection services) {
    services.AddDaprClientOnce();
    services.AddSingleton<IDaprPubSubService, DaprPubSubService>();
  }

  /// <summary>
  /// Registers <see cref="IDaprStateStoreService"/> and a <see cref="DaprClient"/> when missing.
  /// </summary>
  public static void RegisterStateStore(this IServiceCollection services) {
    services.AddDaprClientOnce();
    services.AddSingleton<IDaprStateStoreService, DaprStateStoreService>();
  }

  /// <summary>
  /// Registers <see cref="IDaprInvocationService"/>.
  /// </summary>
  public static void RegisterInvocation(this IServiceCollection services) {
    services.AddDaprClientOnce();
    services.AddSingleton<IDaprInvocationService, DaprInvocationService>();
  }

  /// <summary>
  /// Registers <see cref="IDaprBindingService"/>.
  /// </summary>
  public static void RegisterBinding(this IServiceCollection services) {
    services.AddDaprClientOnce();
    services.AddSingleton<IDaprBindingService, DaprBindingService>();
  }

  /// <summary>
  /// Registers <see cref="IDaprSecretService"/>.
  /// </summary>
  public static void RegisterSecrets(this IServiceCollection services) {
    services.AddDaprClientOnce();
    services.AddSingleton<IDaprSecretService, DaprSecretService>();
  }

  /// <summary>
  /// Registers <see cref="IDaprConfigurationService"/>.
  /// </summary>
  public static void RegisterConfiguration(this IServiceCollection services) {
    services.AddDaprClientOnce();
    services.AddSingleton<IDaprConfigurationService, DaprConfigurationService>();
  }

  /// <summary>
  /// Registers <see cref="IDaprCryptographyService"/>.
  /// </summary>
  public static void RegisterCryptography(this IServiceCollection services) {
    services.AddDaprClientOnce();
    services.AddSingleton<IDaprCryptographyService, DaprCryptographyService>();
  }

  /// <summary>
  /// Registers <see cref="IDaprSidecarService"/>.
  /// </summary>
  public static void RegisterSidecar(this IServiceCollection services) {
    services.AddDaprClientOnce();
    services.AddSingleton<IDaprSidecarService, DaprSidecarService>();
  }

  /// <summary>
  /// Registers <see cref="IDaprLockService"/>.
  /// </summary>
  public static void RegisterLock(this IServiceCollection services) {
    services.AddDaprClientOnce();
    services.AddSingleton<IDaprLockService, DaprLockService>();
  }

  /// <summary>
  /// Registers all <see cref="DaprClient"/>-backed facades (pub/sub, state, invocation, binding, secrets,
  /// configuration, cryptography, sidecar, lock). Actors, workflows, and work leases stay separate.
  /// </summary>
  public static void RegisterDaprClientFacades(this IServiceCollection services) {
    services.RegisterPubSub();
    services.RegisterStateStore();
    services.RegisterInvocation();
    services.RegisterBinding();
    services.RegisterSecrets();
    services.RegisterConfiguration();
    services.RegisterCryptography();
    services.RegisterSidecar();
    services.RegisterLock();
  }

  /// <summary>
  /// Registers Dapr state store plus HA work-lease service and runtime instance id.
  /// </summary>
  public static void RegisterWorkLeases(this IServiceCollection services) {
    services.RegisterStateStore();
    services.AddSingleton<IDaprRuntimeInstanceId, DaprRuntimeInstanceIdProvider>();
    services.AddSingleton<IDaprWorkLeaseService, DaprWorkLeaseService>();
  }

  /// <summary>
  /// Registers work leases and binds the default Dapr state Component name for <see cref="LeasedBackgroundService"/>.
  /// </summary>
  public static void RegisterWorkLeases(this IServiceCollection services, string storeName) {
    if (string.IsNullOrWhiteSpace(storeName))
      throw new ArgumentException("storeName is required.", nameof(storeName));

    services.RegisterWorkLeases();
    services.AddSingleton<IDaprWorkLeaseOptions>(new DaprWorkLeaseOptions { StoreName = storeName });
  }

  /// <summary>
  /// Registers the Dapr actor runtime and <see cref="IDaprActorService"/>.
  /// Use <paramref name="configure"/> to <c>options.Actors.RegisterActor&lt;T&gt;()</c> and tune idle/drain settings.
  /// Pair with <see cref="WebApplicationExtensions.RegisterActorsHandlers"/>.
  /// </summary>
  public static void RegisterActors(this IServiceCollection services, Action<ActorRuntimeOptions>? configure = null) {
    if (!services.Any(d => d.ServiceType == typeof(IActorProxyFactory)))
      services.AddActors(options => configure?.Invoke(options));

    services.AddSingleton<IDaprActorService, DaprActorService>();
  }

  /// <summary>
  /// Registers Dapr workflow runtime/client and <see cref="IDaprWorkflowService"/>.
  /// From Dapr SDK 1.18+, workflows/activities in the entry assembly are auto-discovered by the source generator;
  /// pass <paramref name="configure"/> only for explicit <c>RegisterWorkflow</c> / <c>RegisterActivity</c> or other
  /// <see cref="WorkflowRuntimeOptions"/> tuning. Types in referenced assemblies need
  /// <c>DaprWorkflowVersioningScanReferences</c> in the host <c>.csproj</c>.
  /// </summary>
  public static void RegisterWorkflows(this IServiceCollection services, Action<WorkflowRuntimeOptions>? configure = null) {
    if (!services.Any(d => d.ServiceType == typeof(DaprWorkflowClient) || d.ServiceType == typeof(IDaprWorkflowClient))) {
      if (configure is null)
        services.AddDaprWorkflow();
      else
        services.AddDaprWorkflow(configure);
    }

    services.AddSingleton<IDaprWorkflowService, DaprWorkflowService>();
  }
}
