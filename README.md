# MaksIT.Dapr

![Line Coverage](https://img.shields.io/badge/Line%20Coverage-41.9%25-yellowgreen)
![Branch Coverage](https://img.shields.io/badge/Branch%20Coverage-40.7%25-yellowgreen)
![Method Coverage](https://img.shields.io/badge/Method%20Coverage-59.5%25-yellowgreen)

NuGet facade over [Dapr](https://dapr.io/) for ASP.NET Core: pub/sub, state, invocation, bindings, secrets, configuration, cryptography, sidecar, lock, HA work leases, actors, and workflows — all with `MaksIT.Results` outcomes.

## Table of Contents

- [Overview](#overview)
- [Result conventions](#result-conventions)
- [Getting Started](#getting-started)
- [Installation](#installation)
- [Registering services](#registering-services)
- [Services](#services)
  - [Pub/Sub — `IDaprPubSubService`](#pubsub--idaprpubsubservice)
  - [State store — `IDaprStateStoreService`](#state-store--idaprstatestoreservice)
  - [Service invocation — `IDaprInvocationService`](#service-invocation--idaprinvocationservice)
  - [Bindings — `IDaprBindingService`](#bindings--idaprbindingservice)
  - [Secrets — `IDaprSecretService`](#secrets--idaprsecretservice)
  - [Configuration — `IDaprConfigurationService`](#configuration--idaprconfigurationservice)
  - [Cryptography — `IDaprCryptographyService`](#cryptography--idaprcryptographyservice)
  - [Sidecar — `IDaprSidecarService`](#sidecar--idaprsidecarservice)
  - [Distributed lock — `IDaprLockService`](#distributed-lock--idaprlockservice)
  - [HA work leases — `IDaprWorkLeaseService`](#ha-work-leases--idaprworkleaseservice)
  - [Actors — `IDaprActorService`](#actors--idapractorservice)
  - [Workflows — `IDaprWorkflowService`](#workflows--idaprworkflowservice)
- [Choosing a coordination pattern](#choosing-a-coordination-pattern)
- [Contributing](#contributing)
- [Contact](#contact)
- [License](#license)

## Overview

`MaksIT.Dapr` wraps Dapr building blocks so application code depends on small `IDapr*Service` facades instead of raw `DaprClient` calls. Failures surface as `MaksIT.Results` outcomes that map cleanly to HTTP via `ToActionResult()`.

| Need | Register | Inject |
|------|----------|--------|
| Publish events | `RegisterPubSub()` | `IDaprPubSubService` |
| Key/value state | `RegisterStateStore()` | `IDaprStateStoreService` |
| Call another Dapr app | `RegisterInvocation()` | `IDaprInvocationService` |
| Trigger external systems (queues, cron, …) | `RegisterBinding()` | `IDaprBindingService` |
| Read secrets from a Component | `RegisterSecrets()` | `IDaprSecretService` |
| Dynamic config + subscribe | `RegisterConfiguration()` | `IDaprConfigurationService` |
| Encrypt/decrypt via Dapr crypto Component | `RegisterCryptography()` | `IDaprCryptographyService` |
| Wait for sidecar / health / metadata | `RegisterSidecar()` | `IDaprSidecarService` |
| Short-lived distributed mutex | `RegisterLock()` | `IDaprLockService` |
| Multi-replica exclusive work (leader/bootstrap) | `RegisterWorkLeases(storeName)` | `IDaprWorkLeaseService` |
| Virtual actors | `RegisterActors(...)` | `IDaprActorService` |
| Durable workflows | `RegisterWorkflows(...)` | `IDaprWorkflowService` |

`RegisterDaprClientFacades()` registers all `DaprClient`-backed rows above (not actors, workflows, or work leases).

## Result conventions

- **Success / failure:** check `result.IsSuccess`. Map to HTTP with `result.ToActionResult()`.
- **Cancellation:** facades rethrow `OperationCanceledException` (do not wrap as a failed `Result`).
- **State misses:** `GetStateAsync` / `GetStateAndETagAsync` return `Ok(null)` / `Ok((null, null))` when the key is absent (including JetStream `key not found` surfaced as gRPC `Internal`). `!IsSuccess` means infrastructure failure.
- **Topic handlers:** return `Result` via `ToActionResult()` — `Ok` ACK, `ServiceUnavailable` retry, `BadRequest` drop.

## Getting Started

Ensure that you have the following installed:

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/) (for local sidecar runs)
- [Docker](https://www.docker.com/get-started) (optional; used by RepoUtils Linux test validation)

## Installation

```powershell
dotnet add package MaksIT.Dapr
```

Or in your `.csproj`:

```xml
<PackageReference Include="MaksIT.Dapr" Version="2.2.1" />
```

## Registering services

```csharp
using MaksIT.Dapr.Extensions;

var builder = WebApplication.CreateBuilder(args);

// All DaprClient-backed facades (pub/sub, state, invocation, binding, secrets, …)
builder.Services.RegisterDaprClientFacades();
// Or register individually: RegisterPubSub(), RegisterStateStore(), …

// HA coordination (leases via Dapr state Component — e.g. persistent NATS KV)
builder.Services.RegisterWorkLeases("maksit-cicd-state"); // storeName for LeasedBackgroundService

builder.Services.RegisterActors(options => {
  options.Actors.RegisterActor<MyActor>();
});
builder.Services.RegisterWorkflows();

var app = builder.Build();

// After authentication/authorization middleware
app.RegisterSubscriber();
app.RegisterActorsHandlers();
```

## Services

### Pub/Sub — `IDaprPubSubService`

**When to use:** fire-and-forget or at-least-once messaging between services (commands, domain events, fan-out). Prefer this over direct broker SDKs so the app stays Component-agnostic (Redis, NATS, Kafka, …).

**When not to:** request/response RPC → [service invocation](#service-invocation--idaprinvocationservice); long-running orchestrations → [workflows](#workflows--idaprworkflowservice); exclusive multi-replica jobs → [work leases](#ha-work-leases--idaprworkleaseservice).

| | |
|--|--|
| **DI** | `RegisterPubSub()` (also in `RegisterDaprClientFacades()`) |
| **Pipeline** | `app.RegisterSubscriber()` + `[Topic("pubsubName", "topic")]` on controllers |
| **API** | `PublishEventAsync`, `PublishByteEventAsync`, `BulkPublishEventAsync` |

```csharp
var result = await pubSub.PublishEventAsync("my-pubsub", "my-topic", command, cancellationToken: ct);
if (!result.IsSuccess) { /* handle */ }

[Topic("my-pubsub", "my-topic")]
[HttpPost("/my-endpoint")]
public IActionResult Receive([FromBody] MyCommand payload) =>
  Result.Ok().ToActionResult(); // ACK; ServiceUnavailable → retry; BadRequest → drop
```

### State store — `IDaprStateStoreService`

**When to use:** shared key/value state (caches, documents, coordination metadata) through a Dapr state Component. Also the foundation for [work leases](#ha-work-leases--idaprworkleaseservice).

**When not to:** primary transactional business data that belongs in your product database; use the app’s own data layer for that.

| | |
|--|--|
| **DI** | `RegisterStateStore()` |
| **API** | `Set` / `Get` / `Delete`, ETag OCC (`GetStateAndETagAsync`, `TrySaveStateAsync`, `TryDeleteStateAsync`), bulk, query, transactions |

```csharp
var save = await stateStore.SetStateAsync("my-store", "my-key", "my-value", cancellationToken: ct);
var get = await stateStore.GetStateAsync<string>("my-store", "my-key", cancellationToken: ct);
if (!get.IsSuccess) { /* infra failure */ }
var value = get.Value; // null when key is missing
```

### Service invocation — `IDaprInvocationService`

**When to use:** synchronous HTTP-style calls to another Dapr app id (mTLS, retries, and discovery handled by the sidecars). Good for request/response between microservices without hard-coding URLs.

**When not to:** broadcast or decoupled events → [pub/sub](#pubsub--idaprpubsubservice); calling non-Dapr external APIs → [bindings](#bindings--idaprbindingservice) or a normal `HttpClient`.

| | |
|--|--|
| **DI** | `RegisterInvocation()` |
| **API** | `InvokeAsync` overloads (void / request / response / both) — HTTP **POST** via `CreateInvokableHttpClient` |

```csharp
var result = await invocation.InvokeAsync<OrderRequest, OrderResponse>(
  appId: "orders",
  methodName: "create",
  data: request,
  cancellationToken: ct);
```

### Bindings — `IDaprBindingService`

**When to use:** invoke an **output** binding Component (send to a queue, call Twilio, write to blob storage, cron-triggered input handlers on the app side, etc.) without embedding vendor SDKs.

**When not to:** app-to-app messaging → [pub/sub](#pubsub--idaprpubsubservice) or [invocation](#service-invocation--idaprinvocationservice).

| | |
|--|--|
| **DI** | `RegisterBinding()` |
| **API** | `InvokeAsync` (fire-and-forget or typed response) |

```csharp
var result = await bindings.InvokeAsync("my-smtp", "create", emailPayload, cancellationToken: ct);
```

### Secrets — `IDaprSecretService`

**When to use:** load secrets at runtime from a Dapr secret store Component (Kubernetes secrets, Azure Key Vault, local files, …) when the host should not bind every secret into `IConfiguration` up front.

**When not to:** replace standard ASP.NET `appsettings` / `appsecrets.json` for ordinary host config — MaksIT hosts still prefer configuration binding for app settings; use this facade for Component-backed secret reads from application code.

| | |
|--|--|
| **DI** | `RegisterSecrets()` |
| **API** | `GetAsync`, `GetBulkAsync` |

```csharp
var secret = await secrets.GetAsync("my-secret-store", "connection-string", cancellationToken: ct);
```

### Configuration — `IDaprConfigurationService`

**When to use:** read or subscribe to keys from a Dapr configuration Component (feature flags, dynamic settings that change without redeploy).

**When not to:** static startup configuration already covered by `IConfiguration` / Options — keep using the host configuration stack for that.

| | |
|--|--|
| **DI** | `RegisterConfiguration()` |
| **API** | `GetAsync`, `SubscribeAsync`, `UnsubscribeAsync` |

### Cryptography — `IDaprCryptographyService`

**When to use:** encrypt/decrypt payloads with keys managed by a Dapr cryptography Component (keys stay in the sidecar/Component, not in app memory as raw key material).

**When not to:** simple password hashing or app-local crypto libraries when you do not need Dapr-managed keys.

| | |
|--|--|
| **DI** | `RegisterCryptography()` |
| **API** | `EncryptAsync`, `DecryptAsync` |

### Sidecar — `IDaprSidecarService`

**When to use:** gate startup or background work until the sidecar is ready; health/outbound checks; inspect metadata; request graceful sidecar shutdown.

**Typical scenario:** call `WaitForSidecarAsync` before the first [work-lease](#ha-work-leases--idaprworkleaseservice) race or state call on cold start so replicas do not fail while the sidecar is still connecting.

| | |
|--|--|
| **DI** | `RegisterSidecar()` |
| **API** | `CheckHealthAsync`, `CheckOutboundHealthAsync`, `WaitForSidecarAsync`, `GetMetadataAsync`, `ShutdownAsync` |

```csharp
var ready = await sidecar.WaitForSidecarAsync(ct);
if (!ready.IsSuccess) { /* abort startup path */ }
```

### Distributed lock — `IDaprLockService`

**When to use:** a short-lived distributed mutex on a Dapr **lock** Component — e.g. protect a critical section across processes for seconds, then unlock. Check `TryLockResponse.Success`; dispose the response (or call `UnlockAsync`) when finished. Expiry is in seconds via the Dapr API.

**When not to:** multi-replica leader election, bootstrap, or long-running exclusive sweeps → prefer [HA work leases](#ha-work-leases--idaprworkleaseservice) (state-backed TTL, auto-renew, generation fencing, `LeasedBackgroundService` / bootstrap helpers). Per-entity serialized logic → [actors](#actors--idapractorservice).

| | |
|--|--|
| **DI** | `RegisterLock()` (also in `RegisterDaprClientFacades()`) |
| **API** | `LockAsync` → `TryLockResponse`, `UnlockAsync` → `UnlockResponse` |
| **Component** | Dapr lock store (separate from the state Component used by work leases) |

```csharp
var locked = await locks.LockAsync("my-lock-store", "invoice-42", runtimeInstance.InstanceId, expiryInSeconds: 15, ct);
if (!locked.IsSuccess)
  return; // infra failure

await using var handle = locked.Value; // DisposeAsync unlocks
if (!handle.Success)
  return; // another owner holds the lock

/* critical section */
```

Or unlock explicitly with `UnlockAsync` when you are not disposing the `TryLockResponse`.

### HA work leases — `IDaprWorkLeaseService`

**When to use:** only one replica among many should run a piece of work (DB migrate/bootstrap, periodic sweep, leader-style job). Leases live in a Dapr **state** Component (e.g. persistent NATS JetStream KV) — broker-agnostic, ETag concurrency, optional auto-renew and generation fencing.

**When not to:** short critical sections with an explicit unlock → [distributed lock](#distributed-lock--idaprlockservice); per-entity serialized state machines → [actors](#actors--idapractorservice); multi-step durable processes → [workflows](#workflows--idaprworkflowservice).

| | |
|--|--|
| **Namespace** | `MaksIT.Dapr.Services.WorkLease` |
| **DI** | `RegisterWorkLeases()` or `RegisterWorkLeases("state-component-name")` |
| **Also registered** | `IDaprStateStoreService`, `IDaprRuntimeInstanceId` (`POD_NAME` in Kubernetes) |
| **Helpers** | `TryHoldAsync` → `DaprWorkLeaseHold`, `DaprWorkLeaseBootstrap.RunBootstrapUnderLeaseAsync`, `LeasedBackgroundService` |

```csharp
using MaksIT.Dapr.Services.WorkLease;

builder.Services.RegisterWorkLeases("maksit-cicd-state");

await using var hold = (await leases.TryHoldAsync(
  storeName: "maksit-cicd-state",
  workKey: "bootstrap",
  holderId: runtimeInstance.InstanceId,
  ttl: TimeSpan.FromMinutes(5),
  autoRenew: true,
  cancellationToken: ct)).Value;

if (hold is null)
  return; // another replica holds the lease

var stillHeld = await hold.EnsureStillHeldAsync(ct);
if (stillHeld is { IsSuccess: true, Value: true }) {
  /* exclusive work; hold.Generation is a fencing token */
}

// Bootstrap: leader runs under lease; followers poll until ready
await DaprWorkLeaseBootstrap.RunBootstrapUnderLeaseAsync(
  leases,
  "maksit-cicd-state",
  "bootstrap",
  runtimeInstance.InstanceId,
  TimeSpan.FromMinutes(5),
  bootstrap: async ct => { /* migrate / seed */ return Result.Ok(); },
  isReady: async ct => Result<bool>.Ok(await db.IsMigratedAsync(ct)),
  cancellationToken: ct);
```

For recurring exclusive sweeps, subclass `LeasedBackgroundService` and implement `ExecuteLeasedAsync` (uses `IDaprWorkLeaseOptions.StoreName` from `RegisterWorkLeases(storeName)`). Before lease races on cold start, call `IDaprSidecarService.WaitForSidecarAsync`.

### Actors — `IDaprActorService`

**When to use:** virtual-actor patterns — single-threaded access to an entity id (cart, device, session), turn-based concurrency, reminders/timers. Define actor interfaces and implementations in the product; this facade creates proxies and invokes methods with `Result` outcomes.

**When not to:** cluster-wide singleton jobs → [work leases](#ha-work-leases--idaprworkleaseservice); multi-step saga across many services → [workflows](#workflows--idaprworkflowservice).

| | |
|--|--|
| **DI** | `RegisterActors(options => options.Actors.RegisterActor<T>())` |
| **Pipeline** | `app.RegisterActorsHandlers()` |
| **API** | `Create` / `Create<TActor>`, `InvokeAsync` overloads |

```csharp
builder.Services.RegisterActors(o => o.Actors.RegisterActor<CounterActor>());
app.RegisterActorsHandlers();

var created = actors.Create<ICounterActor>("cart-42", nameof(CounterActor));
if (!created.IsSuccess)
  return created.ToResult().ToActionResult();

var count = await created.Value.IncrementAsync();
```

### Workflows — `IDaprWorkflowService`

**When to use:** durable, multi-step business processes (order pipeline, approval flow) with history, wait-for-external-event, suspend/resume, and replay. Author `Workflow` / `WorkflowActivity` types in the product (auto-discovered on Dapr SDK 1.18+).

**When not to:** simple fire-and-forget messages → [pub/sub](#pubsub--idaprpubsubservice); single exclusive background job → [work leases](#ha-work-leases--idaprworkleaseservice).

| | |
|--|--|
| **DI** | `RegisterWorkflows(configure?)` |
| **API** | `ScheduleAsync`, get/wait state, `RaiseEventAsync`, terminate/suspend/resume/purge, list IDs, history, `RerunFromEventAsync` |

```csharp
builder.Services.RegisterWorkflows();

var scheduled = await workflows.ScheduleAsync(
  workflowName: nameof(OrderProcessingWorkflow),
  input: order,
  instanceId: order.Id,
  cancellationToken: ct);

if (!scheduled.IsSuccess)
  return scheduled.ToResult().ToActionResult();

var completed = await workflows.WaitForCompletionAsync(scheduled.Value, cancellationToken: ct);
```

## Choosing a coordination pattern

| Scenario | Prefer |
|----------|--------|
| One replica runs migrate/bootstrap/sweep | **Work leases** |
| Short critical section across processes | **Distributed lock** |
| Per-entity single-threaded logic | **Actors** |
| Long-running multi-step process with history | **Workflows** |
| Decoupled async events | **Pub/sub** |
| Sync call to another Dapr app | **Invocation** |

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for build/test (RepoUtils), commit format, and release scripts.

## Contact

- **Email**: [maksym.sadovnychyy@gmail.com](mailto:maksym.sadovnychyy@gmail.com)

## License

See `LICENSE.md`.
