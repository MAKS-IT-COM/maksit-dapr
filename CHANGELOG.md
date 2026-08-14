# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [2.2.1] - 2026-08-14

### Changed
- **Dependencies:** `MaksIT.Core` `1.6.9`, `MaksIT.Results` `2.0.4`; test SDK `Microsoft.NET.Test.Sdk` `18.9.0`.
- **RepoUtils:** synced Community Non-Helm utils — plugin success checks use exact `$true` (CLI stdout no longer masks failures), `Invoke-ExternalCommand` defaults to throw-on-error with soft callers opting out, plugin helper discovery by group directories.
- Package version **2.2.1**.

### Removed
- `Update-RepoUtils.bat` and `utils/tools/Update-RepoUtils/` (local-copy sync only).

## [2.2.0] - 2026-07-31

### Added
- **Actors facade:** `IDaprActorService` / `DaprActorService` — create typed/weak actor clients and invoke methods with `Result` outcomes; DI `RegisterActors(configure?)` and pipeline `RegisterActorsHandlers()`.
- **Workflows facade:** `IDaprWorkflowService` / `DaprWorkflowService` — schedule, get/wait state, raise event, terminate/suspend/resume/purge, list instance IDs, get history, rerun from event; DI `RegisterWorkflows(configure?)`.
- **Pub/Sub facade rename/expand:** `IDaprPubSubService` — publish with metadata, byte publish, bulk publish; DI `RegisterPubSub()`.
- **State expand:** bulk get/save/delete, `TryDeleteStateAsync`, `QueryStateAsync`, `ExecuteStateTransactionAsync`, optional metadata/consistency/options on existing methods.
- **Client facades:** invocation, binding, secrets, configuration, cryptography, sidecar (`IDapr*Service`) + `Register*` and `RegisterDaprClientFacades()`.
- **Lock facade:** `IDaprLockService` / `DaprLockService` — `LockAsync` / `UnlockAsync` with `Result` outcomes; DI `RegisterLock()` (included in `RegisterDaprClientFacades()`).
- **HA helpers:** `TryHoldAsync` / `DaprWorkLeaseHold` (auto-renew + `Generation` fencing), `LeasedBackgroundService`, `DaprWorkLeaseBootstrap.RunBootstrapUnderLeaseAsync`, `RegisterWorkLeases(storeName)` / `IDaprWorkLeaseOptions`.
- **Work leases rename:** `IDaprWorkLeaseStore` / `DaprWorkLeaseStore` → `IDaprWorkLeaseService` / `DaprWorkLeaseService`.

### Fixed
- **State get miss on `state.jetstream`:** `GetStateAndETagAsync` / `GetStateAsync` treat NATS/Dapr `key not found` (often gRPC `Internal`) as empty without logging Error — so work-lease first acquire works after release or on a fresh bucket.
- **Results hygiene:** rethrow `OperationCanceledException`; `BadRequest` for empty store/key or pubsub/topic; idempotent `DeleteStateAsync` when key is missing.
- **DI:** `AddDaprClient` is skipped per `IServiceCollection` when `DaprClient` is already registered (no process-wide static flag).
- **Docs:** README and CONTRIBUTING aligned with `net10.0`, RepoUtils Non-Helm bats, and current package version.

### Changed
- **Work leases layout:** all HA lease types live under `Services/WorkLease/` (`MaksIT.Dapr.Services.WorkLease`) — `DaprWorkLease`, `DaprWorkLeaseHold` / bootstrap / `LeasedBackgroundService`, `DaprWorkLeaseService`, `IDaprWorkLeaseOptions`, `IDaprRuntimeInstanceId`.
- **`GetStateAsync` miss:** returns `Ok(null)` instead of `NotFound` (aligned with ETag get and typical MaksIT optional-read pattern). Check `Value is null` for absence; `!IsSuccess` means infra failure.
- **CancellationToken:** optional `cancellationToken` on facade APIs; lease `ReleaseAsync` forwards it to delete.
- **Invocation:** `DaprInvocationService` uses `DaprClient.CreateInvokableHttpClient` (HTTP POST + JSON) instead of obsolete `InvokeMethodAsync(appId, method…)` helpers.
- **Workflows DI:** `RegisterWorkflows()` calls parameterless `AddDaprWorkflow()` when no configure delegate is passed (SDK 1.18 auto-discovers workflows/activities).
- **Dependencies:** Dapr packages bumped to `1.18.5`.
- **Docs:** README documents each facade (when to use / when not, DI, API), including lock vs work-lease guidance, and a short coordination-pattern chooser.
- **XML docs** on public APIs; `GenerateDocumentationFile` enabled.
- Removed obsolete `assets/badges` pack items (CoverageBadges uses shields.io).
- Package version **2.2.0**.

### Removed
- Custom pub/sub accept types and `IDaprPubSubWorkHandler` (`DaprPubSubAcceptOutcome`, `DaprPubSubAcceptResult`, `DaprPubSubAck`). Topic controllers should return `MaksIT.Results.Result` via `ToActionResult()` directly (`Ok` ACK, `ServiceUnavailable` retry, `BadRequest` drop).
- `IDaprPublisherService` / `RegisterPublisher` (use `IDaprPubSubService` / `RegisterPubSub`).
- Leftover `DaprWorkLeaseStore` and `PubSub/DaprPubSubWork` types (no obsolete shims).
- `DaprFacadeGuard` helper (inline try/catch + validation in each service).

## [2.1.0] - 2026-07-30

### Added
- **HA work leases via Dapr state:** `IDaprWorkLeaseService` / `DaprWorkLeaseService` (acquire, renew, release, get) using ETag concurrency — broker-agnostic (NATS KV / Postgres / other state Component).
- **State ETag API:** `GetStateAndETagAsync` and `TrySaveStateAsync` on `IDaprStateStoreService`.
- **Runtime instance id:** `IDaprRuntimeInstanceId` / `DaprRuntimeInstanceIdProvider` (`POD_NAME` in Kubernetes).
- **Pub/sub worker helpers:** `IDaprPubSubWorkHandler<T>`, `DaprPubSubAcceptOutcome` / `DaprPubSubAcceptResult`, `DaprPubSubAck` (HTTP ACK/NAK mapping).
- DI: `RegisterWorkLeases()` registers state store + work-lease service + instance id.

### Changed
- Package version **2.1.0**.

## [2.0.1] - 2026-06-28

### Changed
- Restored repository automation under `utils/` (aligned with maksit-core and maksit-repoutils). Configure release and test flows in `utils/engines/*/scriptSettings.json`.
- Updated dependencies to Dapr `1.18.4`, `MaksIT.Core` `1.6.8`, and `MaksIT.Results` `2.0.3`.

## [2.0.0] - 2026-02-22

### Added
- Dedicated test project (`MaksIT.Dapr.Tests`) with coverage for publisher and state-store service behavior.
- Repository-level utility modules and scripts under `utils/` for release automation, coverage badge generation, and tagged-commit maintenance.

### Changed
- Upgraded target framework to `.NET 10` (`net10.0`).
- Updated core dependencies to Dapr `1.16.1`, `MaksIT.Core` `1.6.4`, and `MaksIT.Results` `2.0.0`.
- Migrated solution definition from `MaksIT.Dapr.sln` to `MaksIT.Dapr.slnx`, including test project wiring.
- NuGet packaging now includes `CHANGELOG.md` and coverage badge assets.

### Removed
- Legacy root-level release scripts (`Release-NuGetPackage.*`) in favor of the `utils/Release-NuGetPackage/` flow.
