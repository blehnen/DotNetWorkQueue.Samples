# Roadmap: Upgrade DotNetWorkQueue Samples to 0.9.11

## Overview

DotNetWorkQueue 0.9.11 removes the `IHistoryConfiguration` interface. History tracking is now configured exclusively through transport options at queue creation time (via `IBaseTransportOptions.EnableHistory`), not as a runtime consumer toggle. This upgrade requires:

1. Removing all code that references the deleted `IHistoryConfiguration` interface
2. Adding `RedisBaseTransportOptions.EnableHistory` configuration to Redis producers (the only transport where history is set at the producer/creation level)
3. Bumping NuGet package versions from 0.9.10 to 0.9.11 across all 36 transport project files

SampleShared.csproj already references 0.9.11, but its code has not been updated for the breaking change.

## Scope

- **In scope:** 36 transport .csproj files, 1 shared library source file (Injectors.cs), 21 consumer Program.cs files, 2 Redis producer Program.cs files
- **Out of scope:** DashBoard.Api (independent package lifecycle), App.config/JSON config files, any refactoring beyond what the breaking change requires

## Risk Assessment

The primary risk is **build-order coupling**: SampleShared must compile cleanly before any transport solution can build, because transport projects reference SampleShared via HintPath to its compiled DLL. If the SampleShared code fix is wrong, all downstream builds fail. This is mitigated by making SampleShared the first phase and verifying its build before proceeding.

Secondary risk is **missed occurrences**: the `History.Enabled` pattern appears in 21 files. A mechanical find-and-delete is reliable, but must be verified with a grep post-change.

There is no functional risk from the Redis producer change -- it is additive (new code, not modifying existing logic).

---

## Phase 1: Fix SampleShared (Foundation)

**Description:** Remove the `IHistoryConfiguration` usage from the shared library and verify it builds. This unblocks all downstream transport builds.

**Requirements covered:** R2

**Files affected:**
- `Source/Samples/SampleShared/Injectors.cs` (lines 64-65: remove `container.GetInstance<IHistoryConfiguration>()` and `history.Enabled = ...`)

**Success criteria:**
- `Injectors.SetOptions()` no longer references `IHistoryConfiguration`
- SampleShared builds cleanly for both net8.0 and net48

**Verification commands:**
```bash
# Confirm no IHistoryConfiguration references remain in SampleShared
grep -r "IHistoryConfiguration" Source/Samples/SampleShared/ && echo "FAIL: references remain" || echo "PASS"

# Build SampleShared
dotnet restore "Source/Samples/SampleShared/SampleShared.sln"
dotnet build "Source/Samples/SampleShared/SampleShared.sln" -c Debug
```

**Estimated scope:** ~5% of total work. One file, two lines removed.

---

## Phase 2: Update Transport Projects (Bulk Change)

**Description:** Across all 5 transport solutions (Redis, SQL Server, PostgreSQL, SQLite, LiteDB), perform three changes simultaneously per transport:

1. **NuGet version bump:** Update `DotNetWorkQueue` and transport-specific package references from 0.9.10 to 0.9.11 in all 36 .csproj files (excluding DashBoard.Api)
2. **Remove History.Enabled from consumers:** Delete `queue.Configuration.History.Enabled = SharedConfiguration.EnableHistory;` from all 21 consumer/scheduler-consumer/producer-consumer Program.cs files
3. **Add RedisBaseTransportOptions.EnableHistory to Redis producers:** In `RedisProducer/Program.cs` and `RedisProducerLinq/Program.cs`, resolve `RedisBaseTransportOptions` from the container and set `EnableHistory = SharedConfiguration.EnableHistory` before producing messages

**Requirements covered:** R1, R3, R4

**Depends on:** Phase 1 (SampleShared must be built first so transport projects can resolve the updated DLL)

**Files affected:**

*NuGet version bump (36 .csproj files):*
- `Source/Samples/Redis/RedisConsumer/RedisConsumer.csproj`
- `Source/Samples/Redis/RedisConsumerAsync/RedisConsumerAsync.csproj`
- `Source/Samples/Redis/RedisConsumerLinq/RedisConsumerLinq.csproj`
- `Source/Samples/Redis/RedisProducer/RedisProducer.csproj`
- `Source/Samples/Redis/RedisProducerLinq/RedisProducerLinq.csproj`
- `Source/Samples/Redis/RedisScheduler/RedisScheduler.csproj`
- `Source/Samples/Redis/RedisSchedulerConsumer/RedisSchedulerConsumer.csproj`
- `Source/Samples/SQLServer/SQLServerConsumer/SQLServerConsumer.csproj`
- `Source/Samples/SQLServer/SQLServerConsumerAsync/SQLServerConsumerAsync.csproj`
- `Source/Samples/SQLServer/SQLServerConsumerLinq/SQLServerConsumerLinq.csproj`
- `Source/Samples/SQLServer/SQLServerProducer/SQLServerProducer.csproj`
- `Source/Samples/SQLServer/SQLServerProducerLinq/SQLServerProducerLinq.csproj`
- `Source/Samples/SQLServer/SQLServerScheduler/SQLServerScheduler.csproj`
- `Source/Samples/SQLServer/SQLServerSchedulerConsumer/SQLServerSchedulerConsumer.csproj`
- `Source/Samples/PostgreSQL/PostGreSQLConsumer/PostGreSQLConsumer.csproj`
- `Source/Samples/PostgreSQL/PostGreSQLConsumerAsync/PostGreSQLConsumerAsync.csproj`
- `Source/Samples/PostgreSQL/PostGreSQLConsumerLinq/PostGreSQLConsumerLinq.csproj`
- `Source/Samples/PostgreSQL/PostgreSQLProducer/PostgreSQLProducer.csproj`
- `Source/Samples/PostgreSQL/PostgreSQLProducerLinq/PostgreSQLProducerLinq.csproj`
- `Source/Samples/PostgreSQL/PostGreSQLScheduler/PostGreSQLScheduler.csproj`
- `Source/Samples/PostgreSQL/PostGreSQLSchedulerConsumer/PostGreSQLSchedulerConsumer.csproj`
- `Source/Samples/SQLite/SQLiteConsumer/SQLiteConsumer.csproj`
- `Source/Samples/SQLite/SQLiteConsumerAsync/SQLiteConsumerAsync.csproj`
- `Source/Samples/SQLite/SQLiteConsumerLinq/SQLiteConsumerLinq.csproj`
- `Source/Samples/SQLite/SQLiteProducer/SQLiteProducer.csproj`
- `Source/Samples/SQLite/SQLiteProducerLinq/SQLiteProducerLinq.csproj`
- `Source/Samples/SQLite/SQliteScheduler/SQliteScheduler.csproj`
- `Source/Samples/SQLite/SQLiteSchedulerConsumer/SQLiteSchedulerConsumer.csproj`
- `Source/Samples/LiteDb/LiteDbConsumer/LiteDbConsumer.csproj`
- `Source/Samples/LiteDb/LiteDbConsumerAsync/LiteDbConsumerAsync.csproj`
- `Source/Samples/LiteDb/LiteDbConsumerLinq/LiteDbConsumerLinq.csproj`
- `Source/Samples/LiteDb/LiteDbProducer/LiteDbProducer.csproj`
- `Source/Samples/LiteDb/LiteDbProducerConsumer/LiteDbProducerConsumer.csproj`
- `Source/Samples/LiteDb/LiteDbProducerLinq/LiteDbProducerLinq.csproj`
- `Source/Samples/LiteDb/LiteDbScheduler/LiteDbScheduler.csproj`
- `Source/Samples/LiteDb/LiteDbSchedulerConsumer/LiteDbSchedulerConsumer.csproj`

*Remove History.Enabled (21 Program.cs files):*
- `Source/Samples/Redis/RedisConsumer/Program.cs` (line 61)
- `Source/Samples/Redis/RedisConsumerAsync/Program.cs` (line 71)
- `Source/Samples/Redis/RedisConsumerLinq/Program.cs` (line 75)
- `Source/Samples/Redis/RedisSchedulerConsumer/Program.cs` (line 71)
- `Source/Samples/SQLServer/SQLServerConsumer/Program.cs` (line 79)
- `Source/Samples/SQLServer/SQLServerConsumerAsync/Program.cs` (line 89)
- `Source/Samples/SQLServer/SQLServerConsumerLinq/Program.cs` (line 94)
- `Source/Samples/SQLServer/SQLServerSchedulerConsumer/Program.cs` (line 93)
- `Source/Samples/PostgreSQL/PostGreSQLConsumer/Program.cs` (line 66)
- `Source/Samples/PostgreSQL/PostGreSQLConsumerAsync/Program.cs` (line 89)
- `Source/Samples/PostgreSQL/PostGreSQLConsumerLinq/Program.cs` (line 94)
- `Source/Samples/PostgreSQL/PostGreSQLSchedulerConsumer/Program.cs` (line 90)
- `Source/Samples/SQLite/SQLiteConsumer/Program.cs` (line 68)
- `Source/Samples/SQLite/SQLiteConsumerAsync/Program.cs` (line 90)
- `Source/Samples/SQLite/SQLiteConsumerLinq/Program.cs` (line 96)
- `Source/Samples/SQLite/SQLiteSchedulerConsumer/Program.cs` (line 88)
- `Source/Samples/LiteDb/LiteDbConsumer/Program.cs` (line 67)
- `Source/Samples/LiteDb/LiteDbConsumerAsync/Program.cs` (line 89)
- `Source/Samples/LiteDb/LiteDbConsumerLinq/Program.cs` (line 95)
- `Source/Samples/LiteDb/LiteDbSchedulerConsumer/Program.cs` (line 88)
- `Source/Samples/LiteDb/LiteDbProducerConsumer/Program.cs` (line 127)

*Add RedisBaseTransportOptions.EnableHistory (2 Program.cs files):*
- `Source/Samples/Redis/RedisProducer/Program.cs`
- `Source/Samples/Redis/RedisProducerLinq/Program.cs`

**Success criteria:**
- All 36 transport .csproj files reference DotNetWorkQueue 0.9.11
- Zero occurrences of `History.Enabled` in the entire Source tree
- Zero occurrences of `IHistoryConfiguration` in the entire Source tree
- Redis producers configure `EnableHistory` via `RedisBaseTransportOptions`
- All 5 transport solutions build cleanly in Debug configuration

**Verification commands:**
```bash
# Confirm version bump is complete (should return 0 matches excluding DashBoard.Api)
grep -r "0\.9\.10" Source/Samples/*/[!D]*/*.csproj && echo "FAIL: old versions remain" || echo "PASS"

# Confirm no History.Enabled references remain
grep -r "History\.Enabled" Source/ && echo "FAIL: references remain" || echo "PASS"

# Confirm no IHistoryConfiguration references remain
grep -r "IHistoryConfiguration" Source/ && echo "FAIL: references remain" || echo "PASS"

# Confirm Redis producers have EnableHistory
grep -r "RedisBaseTransportOptions" Source/Samples/Redis/RedisProducer/Program.cs Source/Samples/Redis/RedisProducerLinq/Program.cs || echo "FAIL: EnableHistory not added"

# Build all transport solutions (SampleShared must already be built from Phase 1)
dotnet restore "Source/Samples/Redis/Samples.sln" && dotnet build "Source/Samples/Redis/Samples.sln" -c Debug
dotnet restore "Source/Samples/SQLServer/Samples.sln" && dotnet build "Source/Samples/SQLServer/Samples.sln" -c Debug
dotnet restore "Source/Samples/PostgreSQL/Samples.sln" && dotnet build "Source/Samples/PostgreSQL/Samples.sln" -c Debug
dotnet restore "Source/Samples/SQLite/Samples.sln" && dotnet build "Source/Samples/SQLite/Samples.sln" -c Debug
dotnet restore "Source/Samples/LiteDb/Samples.sln" && dotnet build "Source/Samples/LiteDb/Samples.sln" -c Debug
```

**Estimated scope:** ~95% of total work. High file count but mechanical changes.

---

## Phase Dependency Graph

```
Phase 1: Fix SampleShared ──────> Phase 2: Update Transport Projects
  (R2)                              (R1, R3, R4)
  ~5% effort                        ~95% effort
```

Phase 2 cannot begin until Phase 1's SampleShared build succeeds, because all transport projects link against the compiled SampleShared.dll via HintPath.

Within Phase 2, the five transport solutions are independent of each other and can be built/verified in any order or in parallel.

## Final Verification (Post-Phase 2)

After both phases are complete, the following must all pass:

```bash
# 1. No references to removed API
grep -r "IHistoryConfiguration" Source/ && exit 1
grep -r "History\.Enabled" Source/ && exit 1

# 2. All .csproj files at 0.9.11 (excluding DashBoard.Api)
grep -rl "0\.9\.10" Source/Samples/*/[!D]*/*.csproj && exit 1

# 3. Full build chain succeeds
dotnet build "Source/Samples/SampleShared/SampleShared.sln" -c Debug
dotnet build "Source/Samples/Redis/Samples.sln" -c Debug
dotnet build "Source/Samples/SQLServer/Samples.sln" -c Debug
dotnet build "Source/Samples/PostgreSQL/Samples.sln" -c Debug
dotnet build "Source/Samples/SQLite/Samples.sln" -c Debug
dotnet build "Source/Samples/LiteDb/Samples.sln" -c Debug
```

## Notes

- **DashBoard.Api is explicitly excluded.** It references `DotNetWorkQueue.Dashboard.Api` 0.9.10 and `DotNetWorkQueue.Transport.*` 0.9.10 but is on an independent upgrade lifecycle per project non-goals.
- **No SQLiteScheduler consumer History.Enabled removal needed** -- the `SQliteScheduler` project is a scheduler (producer-side), not a consumer, and does not reference `History.Enabled`.
- **The `LiteDbProducerConsumer` sample** is unique -- it is both a producer and consumer in one process. Its `History.Enabled` line (line 127) must be removed like any other consumer.
- **The `SharedConfiguration.EnableHistory` property itself is retained.** It is still read from App.config and will be used by the new `RedisBaseTransportOptions.EnableHistory` assignment. Only its consumption via the deleted `IHistoryConfiguration` interface is removed.
