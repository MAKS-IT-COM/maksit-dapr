# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## v2.0.0 - 2026-02-22

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

<!-- 
Template for new releases:

## v1.x.x

### Added
- New features

### Changed
- Changes in existing functionality

### Deprecated
- Soon-to-be removed features

### Removed
- Removed features

### Fixed
- Bug fixes

### Security
- Security improvements
-->
