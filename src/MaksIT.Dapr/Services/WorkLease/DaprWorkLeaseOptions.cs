namespace MaksIT.Dapr.Services.WorkLease;


/// <summary>
/// Default Dapr state component name for work leases.
/// </summary>
public interface IDaprWorkLeaseOptions {
  /// <summary>
  /// Dapr state store Component name used when callers omit <c>storeName</c>.
  /// </summary>
  string StoreName { get; }
}

/// <summary>
/// Default <see cref="IDaprWorkLeaseOptions"/>.
/// </summary>
public sealed class DaprWorkLeaseOptions : IDaprWorkLeaseOptions {
  /// <inheritdoc />
  public required string StoreName { get; init; }
}
