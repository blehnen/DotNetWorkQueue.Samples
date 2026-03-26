# Upgrade DotNetWorkQueue Samples to 0.9.11

## Description

Upgrade all transport sample projects from DotNetWorkQueue 0.9.10 to 0.9.11. Version 0.9.11 removes `IHistoryConfiguration` — history tracking is now configured exclusively through transport options at queue creation time (via `IBaseTransportOptions.EnableHistory`), not as a runtime consumer toggle. This requires code changes in the shared library, all consumer samples, and Redis producer samples, plus a NuGet version bump across all 36 transport projects.

SampleShared is already referencing 0.9.11 but its code has not been updated for the breaking changes.

## Goals

1. Update all DotNetWorkQueue package references from 0.9.10 to 0.9.11 across all 36 transport projects
2. Remove all usage of the removed `IHistoryConfiguration` interface
3. Ensure Redis producers configure history via the new `RedisBaseTransportOptions.EnableHistory`
4. Verify all solutions build cleanly against 0.9.11

## Non-Goals

- No changes to Dashboard.Api (separate package lifecycle)
- No changes to App.config or JSON configuration files
- No new features or additional refactoring beyond what the breaking changes require
- Not addressing other concerns identified during codebase mapping (deprecated SqlClient, Polly version split, etc.)

## Requirements

### R1: NuGet Package Version Bump
- Update `DotNetWorkQueue` from 0.9.10 to 0.9.11 in all 36 transport .csproj files
- Update transport-specific packages (`DotNetWorkQueue.Transport.Redis`, `.SqlServer`, `.PostgreSQL`, `.SQLite`, `.LiteDB`) from 0.9.10 to 0.9.11

### R2: Remove IHistoryConfiguration from SampleShared
- Remove `container.GetInstance<IHistoryConfiguration>()` and `history.Enabled = ...` from `Injectors.SetOptions()` (Injectors.cs lines 64-65)

### R3: Remove IHistoryConfiguration from Consumer Samples
- Delete `queue.Configuration.History.Enabled = SharedConfiguration.EnableHistory;` from all ~18 consumer samples across all transports (Redis, SQL Server, PostgreSQL, SQLite, LiteDB)

### R4: Add History to Redis Producers
- In RedisProducer and RedisProducerLinq, resolve `RedisBaseTransportOptions` from the container and set `EnableHistory = SharedConfiguration.EnableHistory` before producing

## Non-Functional Requirements

- All 7 solutions (5 transports + SampleShared + Dashboard.Api) must build in Debug configuration
- No new warnings introduced by the upgrade

## Success Criteria

1. `dotnet build` succeeds for SampleShared and all 5 transport solutions
2. No references to `IHistoryConfiguration` remain in the codebase
3. All .csproj files reference DotNetWorkQueue 0.9.11 (except Dashboard.Api which is independent)
4. Redis producers set `EnableHistory` via `RedisBaseTransportOptions`

## Constraints

- SampleShared must be built first (other projects depend on its compiled DLL via HintPath)
- Build order: SampleShared → Transport solutions (any order) → Dashboard.Api (optional)
- Git strategy: manual (user controls commits)
