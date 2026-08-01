namespace MaksIT.Dapr.Services.WorkLease;


/// <summary>
/// Lease document stored in Dapr state for HA work coordination.
/// </summary>
/// <param name="HolderId">Runtime instance currently holding the lease.</param>
/// <param name="AcquiredAtUtc">When this generation was acquired.</param>
/// <param name="ExpiresAtUtc">When the lease expires if not renewed.</param>
/// <param name="Generation">Monotonic fencing token; bumped on steal / re-acquire by another holder path.</param>
public sealed record DaprWorkLease(
  string HolderId,
  DateTimeOffset AcquiredAtUtc,
  DateTimeOffset ExpiresAtUtc,
  long Generation = 0
);
