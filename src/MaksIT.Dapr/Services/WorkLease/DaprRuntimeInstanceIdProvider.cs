namespace MaksIT.Dapr.Services.WorkLease;


/// <summary>
/// Stable id for this process/pod (lease holder).
/// </summary>
public interface IDaprRuntimeInstanceId {
  /// <summary>
  /// Identifier used as lease <c>HolderId</c>.
  /// </summary>
  string InstanceId { get; }
}

/// <summary>
/// Prefers <c>POD_NAME</c> in Kubernetes; otherwise host name + process id.
/// </summary>
public sealed class DaprRuntimeInstanceIdProvider : IDaprRuntimeInstanceId {
  /// <inheritdoc />
  public string InstanceId { get; } = Build();

  private static string Build() {
    var logicalHost =
      Environment.GetEnvironmentVariable("POD_NAME")
      ?? Environment.GetEnvironmentVariable("HOSTNAME")
      ?? Environment.GetEnvironmentVariable("COMPUTERNAME")
      ?? Environment.MachineName;

    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST")))
      return logicalHost;

    return $"{logicalHost}-{Environment.ProcessId}";
  }
}
