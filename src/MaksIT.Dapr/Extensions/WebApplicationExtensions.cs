using Microsoft.AspNetCore.Builder;


namespace MaksIT.Dapr.Extensions;

/// <summary>
/// ASP.NET Core pipeline helpers for Dapr pub/sub and actors.
/// </summary>
public static class WebApplicationExtensions {

  /// <summary>
  /// This code should be placed after the middleware that configures authentication
  /// and authorization but before any other middleware that needs to process incoming HTTP requests.
  /// This ensures that the UseCloudEvents and MapSubscribeHandler middleware has access to the raw HTTP request data
  /// before it is processed by other middleware.
  ///
  /// <para>If you need to set controller as subscriber, use [Topic("pubsubName", "name")] attribute
  /// where:
  /// <list type="table">
  /// <item>
  /// <term>pubsubName</term>
  /// <term>The name of the pubsub component to use.</term>
  /// </item>
  /// <item>
  /// <term>name</term>
  /// <term>The topic name.</term>
  /// </item>
  /// </list>
  /// </para>
  /// </summary>
  /// <param name="app">The application builder.</param>
  public static void RegisterSubscriber(this WebApplication app) {
    app.UseCloudEvents();
    app.MapSubscribeHandler();
  }

  /// <summary>
  /// Maps Dapr actor HTTP handlers. Call after <see cref="ServiceCollectionExtensions.RegisterActors"/>
  /// (typically near endpoint mapping; avoid HTTPS redirection before actors in development sidecars).
  /// </summary>
  /// <param name="app">The application builder.</param>
  public static void RegisterActorsHandlers(this WebApplication app) =>
    app.MapActorsHandlers();
}
