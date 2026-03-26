# STRUCTURE.md

## Overview

The repository has a single source root at `Source/Samples/`. It contains one shared class library, five transport-grouped solution directories each with seven executable projects, and one standalone dashboard API. All solutions are independent — only the SampleShared DLL is shared, via HintPath references rather than ProjectReference. There are no subdirectories beyond the per-transport groups; the flat-per-pattern layout is applied uniformly across all transports.

---

## Findings

### Top-Level Repository Layout

```
DotNetWorkQueue.Samples/
├── .github/workflows/ci.yml          # GitHub Actions CI — builds all 7 solutions
├── .shipyard/                         # Shipyard metadata and codebase docs
├── Source/
│   └── Samples/                       # All sample projects live here
│       ├── SampleShared/              # Shared class library (build first)
│       ├── Redis/                     # Redis transport samples
│       ├── SQLServer/                 # SQL Server transport samples
│       ├── PostgreSQL/                # PostgreSQL transport samples
│       ├── SQLite/                    # SQLite transport samples
│       ├── LiteDb/                    # LiteDB transport samples
│       └── DashBoard.Api/             # Standalone monitoring dashboard
├── grafana-dashboard.json             # Sample Grafana dashboard for Prometheus metrics
├── prometheus.yml                     # Sample Prometheus scrape config
├── CHANGELOG.md
├── readme.md
└── CLAUDE.md                          # AI assistant instructions for this repo
```

Evidence: root directory listing; `Source/Samples/` directory listing.

---

### SampleShared — Shared Library

**Path**: `Source/Samples/SampleShared/`

This is the only class library in the repository. It must be built before any transport solution. All transport executables reference its compiled DLL via framework-conditional HintPath.

```
SampleShared/
├── SampleShared.sln
├── SampleShared.csproj              # Targets net8.0;net48; no OutputType (class library)
├── SimpleMessage.cs                 # Message POCO, ErrorTypes enum, TestClass, SomeInput
├── Messages.cs                      # Message factory methods (CreateSimpleMessage, etc.)
├── RunProducer.cs                   # All producer send patterns (sync, async, batch, loop)
├── MessageProcessing.cs             # Shared consumer message handler (HandleMessages)
├── CreateNotifications.cs           # ConsumerQueueNotifications factory
├── HandleResults.cs                 # Result logging helper for send operations
├── SharedConfiguration.cs           # Static config reader (App.config AppSettings)
├── Injectors.cs                     # DI wiring: metrics, tracing, interceptors, dashboard
├── Helpers.cs                       # Serilog factory, WaitForCancelKeyPress, ReadSetting
├── RandomString.cs                  # Random string generator for message payloads
├── app.config                       # Net48 startup config (not used at runtime by executables)
└── metricsettings.json              # Metrics OTLP endpoint; linked into all producer projects
```

Evidence: `Source/Samples/SampleShared/` directory listing; `Source/Samples/SampleShared/SampleShared.csproj`.

**HintPath convention** (used by every transport executable):
```xml
<!-- net48 -->
<HintPath>..\..\SampleShared\bin\Debug\net48\SampleShared.dll</HintPath>
<!-- net8.0 -->
<HintPath>..\..\SampleShared\bin\Debug\net8.0\SampleShared.dll</HintPath>
```
Evidence: `Source/Samples/Redis/RedisProducer/RedisProducer.csproj` (lines 70–83).

**metricsettings.json link convention** (used by every transport executable):
```xml
<Content Include="..\..\SampleShared\metricsettings.json" Link="metricsettings.json">
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</Content>
```
Evidence: `Source/Samples/Redis/RedisProducer/RedisProducer.csproj` (lines 93–96).

---

### Transport Solution Layout (applies to all five transports)

Each transport directory contains exactly one `Samples.sln` and seven project subdirectories:

```
{Transport}/
├── Samples.sln
├── {Transport}Producer/
├── {Transport}ProducerLinq/
├── {Transport}Consumer/
├── {Transport}ConsumerAsync/
├── {Transport}ConsumerLinq/
├── {Transport}Scheduler/
└── {Transport}SchedulerConsumer/
```

Evidence: `Source/Samples/Redis/` directory listing; `Source/Samples/Redis/Samples.sln` (all seven projects listed).

**Naming pattern**: `{Transport}{Pattern}` where Transport is `Redis`, `SQLServer`, `PostGreSQL` (note capitalisation inconsistency — see below), `SQLite`, or `LiteDb`, and Pattern is one of the seven above.

**Capitalisation inconsistency**: The PostgreSQL directory is named `PostgreSQL` (all-caps SQL) but the project namespace uses `PostGreSQL` (mixed case). This is the only transport with this inconsistency.
  - Evidence: `Source/Samples/PostgreSQL/` directory name vs. namespace `PostGreSQLConsumer` visible in project folder names under `Source/Samples/PostgreSQL/`.

---

### Per-Project File Layout

Every transport executable project has the same file structure:

```
{TransportPattern}/
├── {TransportPattern}.csproj        # OutputType=Exe; targets net8.0;net48
├── Program.cs                       # Single file: Main(), transport-specific init, config read
├── App.config                       # Runtime config (QueueName, Database, feature flags)
├── tracesettings.json               # Jaeger OTLP endpoint (copied to output)
└── [metricsettings.json]            # Linked from SampleShared (not a file here, linked content)
```

Evidence: `Source/Samples/Redis/RedisProducer/` directory listing; `Source/Samples/Redis/RedisConsumer/` directory listing.

No project has more than one source file (beyond `Program.cs`). All logic is in SampleShared.

---

### Solution Files

Each transport has a flat solution containing only the seven executable projects — no shared project or folder grouping in the solution file. SampleShared has its own separate solution.

| Solution | Projects |
|----------|----------|
| `Source/Samples/SampleShared/SampleShared.sln` | 1 (SampleShared) |
| `Source/Samples/Redis/Samples.sln` | 7 |
| `Source/Samples/SQLServer/Samples.sln` | 7 |
| `Source/Samples/PostgreSQL/Samples.sln` | 7 |
| `Source/Samples/SQLite/Samples.sln` | 7 |
| `Source/Samples/LiteDb/Samples.sln` | 7 |
| `Source/Samples/DashBoard.Api/DashBoard.Api.sln` | 1 |

Evidence: `Source/Samples/Redis/Samples.sln` (all 7 projects enumerated); solution directory listing.

---

### Dashboard.Api Project Layout

```
DashBoard.Api/
├── DashBoard.Api.sln
└── DashBoard.Api/
    ├── DashBoard.Api.csproj         # net10.0; Microsoft.NET.Sdk.Web; no SampleShared dep in packages
    ├── Program.cs                   # Top-level statements; all config, DI, middleware here
    ├── Properties/                  # [Inferred] launch settings
    ├── appsettings.json             # Live config (gitignored values; has local edits per git status)
    └── appsettings.example.json     # Reference config showing all supported keys
```

Evidence: `Source/Samples/DashBoard.Api/DashBoard.Api/` directory listing; `Source/Samples/DashBoard.Api/DashBoard.Api/DashBoard.Api.csproj`.

Note: `DashBoard.Api.csproj` does include a HintPath reference to `SampleShared` (net8.0 build), but SampleShared types are not used in `Program.cs`. The reference may be a leftover.
  - Evidence: `Source/Samples/DashBoard.Api/DashBoard.Api/DashBoard.Api.csproj` (lines 29–32)

---

### Configuration File Hierarchy

| File | Scope | Format | Location |
|------|-------|--------|----------|
| `App.config` | Per executable | XML | `Source/Samples/{Transport}/{Pattern}/App.config` |
| `tracesettings.json` | Per executable | JSON | `Source/Samples/{Transport}/{Pattern}/tracesettings.json` (copied to output) |
| `metricsettings.json` | Shared (one copy) | JSON | `Source/Samples/SampleShared/metricsettings.json` (linked to output of each project) |
| `appsettings.json` | Dashboard only | JSON | `Source/Samples/DashBoard.Api/DashBoard.Api/appsettings.json` |
| `appsettings.example.json` | Dashboard only | JSON | `Source/Samples/DashBoard.Api/DashBoard.Api/appsettings.example.json` |

Evidence: `Source/Samples/Redis/RedisProducer/RedisProducer.csproj` (lines 87–96); `Source/Samples/SampleShared/metricsettings.json`; `Source/Samples/DashBoard.Api/DashBoard.Api/appsettings.example.json`.

---

### Entry Points

| Executable | Entry point | Transport init type |
|------------|-------------|---------------------|
| `{Transport}Producer` | `Program.Main()` | `QueueContainer<{T}QueueInit>` |
| `{Transport}ProducerLinq` | `Program.Main()` | `QueueContainer<{T}QueueInit>` |
| `{Transport}Consumer` | `Program.Main()` | `QueueContainer<{T}QueueInit>` |
| `{Transport}ConsumerAsync` | `Program.Main()` | `SchedulerContainer` + `QueueContainer<{T}QueueInit>` |
| `{Transport}ConsumerLinq` | `Program.Main()` | `SchedulerContainer` + `QueueContainer<{T}QueueInit>` |
| `{Transport}Scheduler` | `Program.Main()` | `JobSchedulerContainer` |
| `{Transport}SchedulerConsumer` | `Program.Main()` | `SchedulerContainer` + `QueueContainer<{T}QueueInit>` |
| `DashBoard.Api` | Top-level statements (`Program.cs`) | ASP.NET Core `WebApplication` |

Transport init types by transport:
- Redis: `RedisQueueInit` / `RedisJobQueueCreation`
- SQL Server: `SqlServerMessageQueueInit` / `SqlServerMessageQueueCreation`
- PostgreSQL: `PostgreSqlMessageQueueInit` / `PostgreSqlMessageQueueCreation` [Inferred from naming pattern]
- SQLite: `SqLiteMessageQueueInit` / `SqLiteMessageQueueCreation` [Inferred; Dashboard.Api uses `SqLiteMessageQueueInit`]
- LiteDB: `LiteDbMessageQueueInit` / `LiteDbMessageQueueCreation`

Evidence: `Source/Samples/Redis/RedisProducer/Program.cs` (line 30); `Source/Samples/Redis/RedisConsumerAsync/Program.cs` (lines 32–49); `Source/Samples/Redis/RedisScheduler/Program.cs` (line 31); `Source/Samples/DashBoard.Api/DashBoard.Api/Program.cs` (lines 154, 160).

---

### Module Boundaries and Public Interfaces of SampleShared

SampleShared exposes only `public static` classes and one `public class`. There are no interfaces defined in SampleShared — it depends on DotNetWorkQueue interfaces (`IContainer`, `IProducerQueue<T>`, `IReceivedMessage<T>`, `IWorkerNotification`, etc.) but does not define any of its own.

| Type | Kind | Consumed by |
|------|------|-------------|
| `SharedConfiguration` | `public static class` | All `Program.cs` files |
| `Injectors` | `public static class` | All `Program.cs` files |
| `Helpers` | `public static class` | All `Program.cs` files |
| `Messages` | `public static class` | `RunProducer.RunLoop` |
| `RunProducer` | `public static class` | All producer `Program.cs` files |
| `MessageProcessing` | `public static class` | All consumer `Program.cs` files |
| `CreateNotifications` | `public static class` | All consumer `Program.cs` files |
| `HandleResults` | `public static class` | `RunProducer` internally |
| `SimpleMessage` | `public class` | Producer and consumer `Program.cs`, `MessageProcessing` |
| `TestClass` / `SomeInput` | `public class` | `RunProducer` (Linq send targets) |
| `ErrorTypes` | `public enum` | `SimpleMessage`, `MessageProcessing` |

Evidence: all files in `Source/Samples/SampleShared/`.

---

### CI and Build Order

The GitHub Actions workflow enforces the required build order explicitly: SampleShared is always built first, then each transport solution independently. The workflow runs on `windows-latest` with .NET 8 SDK only.

```
SampleShared → LiteDb → PostgreSQL → Redis → SQLite → SQLServer → DashBoard.Api
```

Evidence: `.github/workflows/ci.yml`.

---

## Summary Table

| Item | Detail | Confidence |
|------|--------|------------|
| Source root | `Source/Samples/` | Observed |
| Transport solutions | 5 (Redis, SQLServer, PostgreSQL, SQLite, LiteDb) | Observed |
| Projects per transport solution | 7 | Observed |
| Shared library | `Source/Samples/SampleShared/` — class library, build first | Observed |
| Inter-project dependency mechanism | HintPath to compiled DLL (not ProjectReference) | Observed |
| Per-project source files | One (`Program.cs`) per executable | Observed |
| Config files per project | `App.config` + `tracesettings.json` + linked `metricsettings.json` | Observed |
| Dashboard project | `Source/Samples/DashBoard.Api/DashBoard.Api/` | Observed |
| Dashboard target framework | `net10.0` | Observed |
| All other projects target frameworks | `net8.0;net48` | Observed |
| CI system | GitHub Actions, windows-latest, .NET 8 SDK | Observed |
| PostgreSQL naming inconsistency | Directory: `PostgreSQL`; project prefix: `PostGreSQL` | Observed |
| Dashboard SampleShared reference | HintPath to net8.0 DLL; may be unused in Program.cs | Observed |

---

## Open Questions

- The `DashBoard.Api.csproj` targets `net10.0` but CI installs only .NET 8 SDK. It is unclear whether the Dashboard build in CI passes by falling back to a compatible runtime or whether CI is currently broken for that step.
- `Source/Samples/SampleShared/RandomString.cs` was not read. Its role is [Inferred] from its use in `Messages.cs` (`RandomString.Create(messagePayloadLength)`).
- The `Properties/` subdirectory inside `DashBoard.Api/DashBoard.Api/` was not explored. It likely contains only `launchSettings.json` but this was not confirmed.
- No `Samples.sln` was inspected for PostgreSQL, SQLite, or LiteDB. Their project count and naming is [Inferred] to match the Redis and SQLServer solutions based on the directory listings showing the same seven folder names.
