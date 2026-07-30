# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.1.0] - 2026-07-30

### Added
- **HA work leases via Dapr state:** `IDaprWorkLeaseStore` / `DaprWorkLeaseStore` (acquire, renew, release, get) using ETag concurrency — broker-agnostic (NATS KV / Postgres / other state Component).
- **State ETag API:** `GetStateAndETagAsync` and `TrySaveStateAsync` on `IDaprStateStoreService`.
- **Runtime instance id:** `IDaprRuntimeInstanceId` / `DaprRuntimeInstanceIdProvider` (`POD_NAME` in Kubernetes).
- **Pub/sub worker helpers:** `IDaprPubSubWorkHandler<T>`, `DaprPubSubAcceptOutcome` / `DaprPubSubAcceptResult`, `DaprPubSubAck` (HTTP ACK/NAK mapping).
- DI: `RegisterWorkLeases()` registers state store + lease store + instance id.

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
