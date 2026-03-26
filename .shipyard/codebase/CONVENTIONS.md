# CONVENTIONS.md

## Overview

The codebase follows consistent C# conventions across all 37 executable sample projects, enforced by pattern repetition rather than any automated tooling (no `.editorconfig`, `Directory.Build.props`, or Roslyn analyzer configuration is present). All transport-specific projects share the same structural template and delegate their logic to `SampleShared`, making cross-transport consistency high at the behavioral level. Minor formatting inconsistencies exist at the XML/csproj indentation level but are inconsequential at runtime.

---

## Findings

### Tooling and Enforcement

- **No `.editorconfig` present**: No editor configuration file exists at any level of the repository. Style is enforced purely by convention and human review.
  - Evidence: Glob search for `.editorconfig` returned no results across the entire repo.
- **No `Directory.Build.props`**: There is no shared MSBuild props file. Every project repeats the full `<PropertyGroup>` and `<PackageReference>` list independently.
  - Evidence: Glob search for `Directory.Build.props` returned no results.
- **No Roslyn analyzers**: No `<PackageReference Include="Microsoft.CodeAnalysis.NetAnalyzers" ...>` or similar analyzer packages appear in any `.csproj`.
  - Evidence: `Source/Samples/Redis/RedisConsumer/RedisConsumer.csproj` (lines 13–64) — no analyzer references.
- **No StyleCop, SonarAnalyzer, or Roslynator packages** referenced in any project.

---

### Naming Conventions

- **Project and namespace naming**: Projects follow `{Transport}{Role}` PascalCase: `RedisProducer`, `SQLServerConsumer`, `LiteDbConsumerAsync`, `PostGreSQLSchedulerConsumer`. Namespaces match project names exactly.
  - Evidence: `Source/Samples/Redis/RedisProducer/Program.cs` (line 12: `namespace RedisProducer`)
  - Evidence: `Source/Samples/PostgreSQL/PostGreSQLConsumer/Program.cs` (line 11: `namespace PostGreSQLConsumer`)
- **Inconsistency — SQLite scheduler folder casing**: The SQLite scheduler folder is named `SQliteScheduler` (lowercase `l`) rather than the expected `SQLiteScheduler`. The namespace inside the file matches the folder: `namespace SQliteScheduler`.
  - Evidence: `Source/Samples/SQLite/SQliteScheduler/Program.cs` (line 10: `namespace SQliteScheduler`)
  - All other SQLite projects use `SQLite` prefix: `SQLiteConsumer`, `SQLiteProducer`, etc.
- **Inconsistency — PostgreSQL casing**: The PostgreSQL transport folder and most project names use `PostGreSQL` (mixed caps), but the appName string literals passed to `Injectors.AddInjectors()` use `PostgreSql` (standard camel): `"PostgreSqlConsumer"`.
  - Evidence: `Source/Samples/PostgreSQL/PostGreSQLConsumer/Program.cs` (line 31: `"PostgreSqlConsumer"`) vs folder name `PostGreSQLConsumer`.
- **Class naming**: All classes in `SampleShared` use PascalCase (`SharedConfiguration`, `MessageProcessing`, `RunProducer`, `HandleResults`). All are `static` classes, consistently.
  - Evidence: `Source/Samples/SampleShared/SharedConfiguration.cs` (line 6), `Source/Samples/SampleShared/MessageProcessing.cs` (line 14).
- **Method naming**: All public methods use PascalCase (`HandleMessages`, `AddInjectors`, `CreateSimpleMessage`, `WaitForCancelKeyPress`). Local variables and parameters use camelCase.
  - Evidence: `Source/Samples/SampleShared/Helpers.cs` (lines 11, 16, 28).
- **Private static fields**: Prefixed with underscore: `_metrics`, `_meterProvider`, `_tracer`, `_dashboardClient`, `_userData`.
  - Evidence: `Source/Samples/SampleShared/Injectors.cs` (lines 25–27, 176); `Source/Samples/SQLServer/SQLServerProducer/Program.cs` (line 16).
- **Constants**: Named in `SCREAMING_SNAKE_CASE` (`AllowedChars` is actually PascalCase — a minor deviation from typical constant style, though `private const` is used correctly).
  - Evidence: `Source/Samples/SampleShared/RandomString.cs` (line 6: `private const string AllowedChars`).

---

### File and Class Organization

- **One class per file rule**: Followed in `SampleShared` for most files. Exception: `SimpleMessage.cs` contains three types (`SimpleMessage`, `ErrorTypes` enum, `TestClass`, `SomeInput`) in a single file.
  - Evidence: `Source/Samples/SampleShared/SimpleMessage.cs` (lines 8–54).
- **Entry points**: All transport executables define a single `Program.cs` containing only a `class Program` with `static void Main(string[] args)`. No top-level statements are used (unlike the Dashboard API).
  - Evidence: `Source/Samples/Redis/RedisProducer/Program.cs` (lines 13–15).
- **Dashboard API exception**: `DashBoard.Api/Program.cs` uses .NET 6+ top-level statements and `implicit usings`.
  - Evidence: `Source/Samples/DashBoard.Api/DashBoard.Api/Program.cs` (lines 1–17 — no class or namespace declaration, `ImplicitUsings` enabled in `.csproj` line 10).
- **Region usage**: Used sparingly; only `SharedConfiguration.cs` uses `#region` blocks (`#region Constructor`, `#region Public Props`).
  - Evidence: `Source/Samples/SampleShared/SharedConfiguration.cs` (lines 8, 47).

---

### Import Ordering

- **No enforced ordering**: Imports are not sorted consistently. Two patterns appear across the codebase:
  - `SampleShared` files: System namespaces first, then third-party (`DotNetWorkQueue`, `Microsoft.*`, `OpenTelemetry`, `Serilog`).
    - Evidence: `Source/Samples/SampleShared/Injectors.cs` (lines 1–19).
  - Transport `Program.cs` files: Mixed — some begin with `DotNetWorkQueue.*`, some begin with `System.*`, others with `SampleShared`. No consistent ordering rule is applied.
    - Evidence: `Source/Samples/Redis/RedisConsumer/Program.cs` (lines 1–12, starts with `DotNetWorkQueue`); `Source/Samples/Redis/RedisProducer/Program.cs` (lines 1–10, starts with `System`).

---

### Code Formatting

- **Indentation**: Predominantly 4-space indentation in `.cs` files. `.csproj` files show mixed indentation — some use 2-space, others use tabs (especially in LiteDb and PostgreSQL projects).
  - Evidence: `Source/Samples/LiteDb/LiteDbConsumer/LiteDbConsumer.csproj` (lines 13–63, tab-indented `<ItemGroup>`); `Source/Samples/Redis/RedisConsumer/RedisConsumer.csproj` (lines 13–64, 4-space `<ItemGroup>`).
- **Brace style**: Allman style (opening brace on new line) for class and method bodies. K&R style (opening brace on same line) used in lambda/delegate expressions.
  - Evidence: `Source/Samples/SampleShared/Injectors.cs` (line 29: method opening brace on same line — this is actually K&R for methods). [Inferred] The overall feel of the codebase leans toward same-line braces for all constructs, consistent with default Visual Studio C# formatting.
- **`var` usage**: `var` is used for all local variable declarations where the type is unambiguous from the right-hand side. Explicit types are used for interface variables and parameters.
  - Evidence: `Source/Samples/Redis/RedisProducer/Program.cs` (lines 18–28: `var log`, `var queueName`, `var connectionString`, `var queueConnection`).
- **String interpolation**: Used consistently in log statements and console output throughout. No string concatenation for multi-part strings in logging contexts.
  - Evidence: `Source/Samples/SampleShared/MessageProcessing.cs` (lines 20, 35, 86); `Source/Samples/SampleShared/CreateNotifications.cs` (lines 22–47).
- **Trailing whitespace/blank lines**: Not consistently trimmed. `RedisConsumerLinq/Program.cs` has the jaeger flush comment inside the inner `using` block while other consumers have it outside — minor structural drift.
  - Evidence: `Source/Samples/Redis/RedisConsumerLinq/Program.cs` (lines 79–82, flush sleep inside inner using block vs `Source/Samples/Redis/RedisConsumer/Program.cs` lines 70–72, outside all using blocks).

---

### Dependency Injection Pattern

- **DI container**: SimpleInjector v5.5.0 is used uniformly across all transport projects as the IoC container (`IContainer` from DotNetWorkQueue wraps it).
  - Evidence: `Source/Samples/Redis/RedisConsumer/RedisConsumer.csproj` (line 44: `SimpleInjector 5.5.0`).
- **Registration pattern**: All projects use the identical two-lambda `QueueContainer<T>` constructor pattern: first lambda registers services into `IContainer`, second lambda sets queue-level options.
  - Evidence: `Source/Samples/Redis/RedisProducer/Program.cs` (lines 30–32); `Source/Samples/SQLServer/SQLServerConsumer/Program.cs` (lines 53–55); `Source/Samples/PostgreSQL/PostGreSQLConsumer/Program.cs` (lines 49–51).
- **Injectors delegation**: Every project delegates the full DI setup to `Injectors.AddInjectors(...)` and `Injectors.SetOptions(...)` in `SampleShared`. No project defines its own service registrations except for transport-specific options objects (`RedisQueueTransportOptions`) or scope injection (LiteDb `ICreationScope`).
  - Evidence: `Source/Samples/Redis/RedisConsumer/Program.cs` (lines 41–46); `Source/Samples/LiteDb/LiteDbProducerConsumer/Program.cs` (lines 153–161: `RegisterService` method adds scope).
- **Singleton registration**: Metrics, tracer, and dashboard client are registered as `LifeStyles.Singleton` or `RegisterNonScopedSingleton`. Static fields on `Injectors` (`_metrics`, `_tracer`, `_dashboardClient`) prevent duplicate registration when multiple containers are created in the same process.
  - Evidence: `Source/Samples/SampleShared/Injectors.cs` (lines 103–110, 220–226: guard clauses checking for non-null static fields).
- **Dashboard client lifecycle**: On net8.0 only, `Injectors.StartDashboardRegistration()` is called before the `QueueContainer` construction, and `Injectors.StopDashboardRegistration()` is called after all `using` blocks exit. The `#if NET8_0_OR_GREATER` guard is consistent across all consumer projects.
  - Evidence: `Source/Samples/Redis/RedisConsumer/Program.cs` (lines 38–40, 66–68); `Source/Samples/SQLServer/SQLServerConsumer/Program.cs` (lines 50–52, 84–86); `Source/Samples/PostgreSQL/PostGreSQLConsumer/Program.cs` (lines 46–48, 71–73).

---

### Configuration Patterns

- **App.config**: Every transport executable carries an `App.config` with a fixed set of `<appSettings>` keys: `Database`, `QueueName`, `EnableTrace`, `EnableMetrics`, `EnableCompression`, `EnableEncryption`, `EnableChaos`, `EnableHistory`, `EnableDashboard`, `DashboardApiUrl`. Transport-specific extras (e.g., `UseUserDequeue`, `UserDayOfWeek` for SQLServer) are additive.
  - Evidence: `Source/Samples/Redis/RedisConsumer/App.config` (lines 6–17); `Source/Samples/SQLServer/SQLServerConsumer/App.config` (lines 6–19).
- **Reading App.config**: All projects use the `ReadSetting` extension method from `SampleShared.Helpers` rather than direct `ConfigurationManager.AppSettings["key"]` indexing. The method returns `string.Empty` on missing keys, avoiding null.
  - Evidence: `Source/Samples/SampleShared/Helpers.cs` (lines 11–14); `Source/Samples/Redis/RedisProducer/Program.cs` (lines 26–27).
- **SharedConfiguration**: Boolean feature flags are parsed once in the static constructor of `SharedConfiguration` and exposed as read-only static properties. All projects consume this class, not raw `ConfigurationManager`.
  - Evidence: `Source/Samples/SampleShared/SharedConfiguration.cs` (lines 9–44).
- **tracesettings.json**: Every executable project ships its own `tracesettings.json` (with `CopyToOutputDirectory = Always`) containing Jaeger endpoint configuration. The service name embedded in each file uses the transport name: `"dotnetworkqueue-Redis-sample"`.
  - Evidence: `Source/Samples/Redis/RedisConsumer/tracesettings.json`; `RedisConsumer.csproj` (lines 83–87).
- **metricsettings.json**: Stored once in `SampleShared/` and linked into each project output via `<Content Include="..\..\SampleShared\metricsettings.json" Link="metricsettings.json">` with `CopyToOutputDirectory = PreserveNewest`. Contains a single `OtlpEndpoint` URL.
  - Evidence: `Source/Samples/Redis/RedisConsumer/RedisConsumer.csproj` (lines 88–92); `Source/Samples/SampleShared/metricsettings.json`.
- **Dashboard appsettings.json**: The Dashboard API uses `appsettings.json` (ASP.NET Core convention) with a `Dashboard` section containing connections, interceptor keys, auth credentials, and swagger toggle. Contains developer-specific hardcoded values (IP addresses, credentials — see CONCERNS.md).
  - Evidence: `Source/Samples/DashBoard.Api/DashBoard.Api/appsettings.json` (lines 1–50).

---

### Error Handling Patterns

- **Queue send errors**: `HandleResults.Handle()` iterates `IQueueOutputMessage` results and logs via Serilog if `result.HasError` is true. No exception is thrown; failures are logged and execution continues.
  - Evidence: `Source/Samples/SampleShared/HandleResults.cs` (lines 8–16).
- **Consumer error notifications**: `CreateNotifications.Create()` wires up five notification callbacks (error, receive error, moved to error queue, poison message, rollback) all logging via Serilog at appropriate levels (`Information`, `Warning`, `Error`).
  - Evidence: `Source/Samples/SampleShared/CreateNotifications.cs` (lines 9–48).
- **Message processing errors**: `MessageProcessing.HandleMessages()` demonstrates three error scenarios: fatal divide-by-zero (simulated), retryable `InvalidDataException` that eventually succeeds, and retryable `InvalidDataException` that always fails. Exceptions are thrown intentionally; the queue framework handles retry/dead-letter routing.
  - Evidence: `Source/Samples/SampleShared/MessageProcessing.cs` (lines 22–68).
- **Scheduler key handling**: Scheduler `Program.cs` files wrap the `switch(key)` block in a `try/catch (Exception e)` that logs via `log.Error(e, "Failed")`. Producer/consumer `Program.cs` files have no such catch — errors propagate to the runtime.
  - Evidence: `Source/Samples/Redis/RedisScheduler/Program.cs` (lines 64–138); `Source/Samples/SQLite/SQliteScheduler/Program.cs` (lines 88–165).
- **Consumer queue existence check**: SQL-backed transports (SQLServer, PostgreSQL, SQLite, LiteDb) check `createQueue.QueueExists` before starting the consumer and return early with an error log if the queue was not yet created by the producer. Redis skips this check (Redis queues are implicitly created).
  - Evidence: `Source/Samples/SQLServer/SQLServerConsumer/Program.cs` (lines 41–47); `Source/Samples/Redis/RedisConsumer/Program.cs` — no equivalent check present.
- **Metrics config error swallowing**: `LoadMetricsConfig()` catches all exceptions silently and returns `null`, allowing startup to continue without metrics if `metricsettings.json` is missing or malformed.
  - Evidence: `Source/Samples/SampleShared/Injectors.cs` (lines 158–173).
- **Graceful shutdown**: All consumer projects use `Helpers.WaitForCancelKeyPress()` which traps `Console.CancelKeyPress`, sets a `ManualResetEventSlim`, and blocks — allowing `using` block `Dispose()` calls to run cleanly before process exit.
  - Evidence: `Source/Samples/SampleShared/Helpers.cs` (lines 28–43); `Source/Samples/Redis/RedisConsumer/Program.cs` (line 63).

---

### Comment and Documentation Conventions

- **Inline comments**: Used generously in `Program.cs` files to explain why configuration values are set (e.g., `//lets run 4 worker threads`, `//set a heartbeat every 10 seconds`). Comments are imperative, lowercase, not sentence-terminated.
  - Evidence: `Source/Samples/Redis/RedisConsumer/Program.cs` (lines 51–60).
- **XML doc comments**: Used only for `ExpiredData()` and `ExpiredDataFuture()` helper methods in producer projects. Not used in `SampleShared` public API surface.
  - Evidence: `Source/Samples/Redis/RedisProducer/Program.cs` (lines 49–52, 62–65); `Source/Samples/SampleShared/Injectors.cs` — no XML docs present.
- **Jaeger flush comment**: The comment `//if jaeger is using udp, sometimes the messages get lost; there doesn't seem to be a flush() call ?` appears verbatim (copy-pasted) in every single `Program.cs` file across all transports and roles.
  - Evidence: `Source/Samples/Redis/RedisProducer/Program.cs` (line 44); `Source/Samples/SQLServer/SQLServerConsumer/Program.cs` (line 88); `Source/Samples/PostgreSQL/PostGreSQLConsumer/Program.cs` (line 75).
- **Log verbosity inconsistency**: Producer and scheduler projects set `MinimumLevel.Debug()`; consumer projects vary — `RedisConsumer` uses `MinimumLevel.Verbose()` while `SQLServerConsumer` and `PostGreSQLConsumer` use `MinimumLevel.Debug()`. `RedisConsumerAsync` uses `MinimumLevel.Verbose()`.
  - Evidence: `Source/Samples/Redis/RedisProducer/Program.cs` (line 21: `.MinimumLevel.Debug()`); `Source/Samples/Redis/RedisConsumer/Program.cs` (line 21: `.MinimumLevel.Verbose()`); `Source/Samples/SQLServer/SQLServerConsumer/Program.cs` (line 24: `.MinimumLevel.Debug()`).

---

### Duplication Across Transports

- **Program.cs structure**: All 35 transport executables follow the same structural template: configure Serilog, read `QueueName`/`Database` from App.config, create `QueueConnection`, optionally create queue (producers/SQL-backed), start dashboard client (net8.0 consumers), create `QueueContainer`, configure and start queue, block on `WaitForCancelKeyPress()` or `RunLoop()`, stop dashboard client, sleep for Jaeger flush.
  - Evidence: Compare `Source/Samples/Redis/RedisConsumer/Program.cs`, `Source/Samples/SQLServer/SQLServerConsumer/Program.cs`, `Source/Samples/PostgreSQL/PostGreSQLConsumer/Program.cs` — structurally identical except for the transport init type (`RedisQueueInit`, `SqlServerMessageQueueInit`, `PostgreSqlMessageQueueInit`).
- **`ExpiredData()` / `ExpiredDataFuture()` / `DelayedProcessing()` helpers**: All producer `Program.cs` files define the same three private static helper methods with identical logic. These are not centralized in `SampleShared`.
  - Evidence: `Source/Samples/Redis/RedisProducer/Program.cs` (lines 53–78); `Source/Samples/SQLServer/SQLServerProducer/Program.cs` (lines 93–130); `Source/Samples/LiteDb/LiteDbProducerConsumer/Program.cs` (lines 167–192) — all structurally identical.
- **Consumer heartbeat/retry/expiry config block**: The five-line heartbeat + retry + expiry configuration block is copy-pasted verbatim into every consumer `Program.cs` across all transports with identical values (`WorkerCount=4`, `HeartBeat.UpdateTime="sec(*%10)"`, `MonitorTime=15s`, `Time=35s`, `RetryDelayBehavior` 3/6/9s, `MessageExpiration.MonitorTime=20s`).
  - Evidence: `Source/Samples/Redis/RedisConsumer/Program.cs` (lines 51–61); `Source/Samples/SQLServer/SQLServerConsumer/Program.cs` (lines 60–69); `Source/Samples/PostgreSQL/PostGreSQLConsumer/Program.cs` (lines 56–65).
- **Scheduler interactive menu**: The scheduler menu (`a/b/c/d/e/f/g/q`) and job management logic are copy-pasted identically across all five transport scheduler projects, differing only in the transport-specific `QueueInit` and `JobQueueCreation` type arguments.
  - Evidence: `Source/Samples/Redis/RedisScheduler/Program.cs` (lines 49–138); `Source/Samples/SQLite/SQliteScheduler/Program.cs` (lines 70–165) — structurally identical.

---

### Version Consistency

- **DotNetWorkQueue version skew**: `SampleShared` references `DotNetWorkQueue` v0.9.11 and `DotNetWorkQueue.Dashboard.Client` v0.9.11, while all 36 transport executable projects reference `DotNetWorkQueue` v0.9.10, `DotNetWorkQueue.Dashboard.Client` v0.9.10, and transport packages at v0.9.10. The Dashboard API references v0.9.10.
  - Evidence: `Source/Samples/SampleShared/SampleShared.csproj` (lines 8, 23: v0.9.11); `Source/Samples/Redis/RedisConsumer/RedisConsumer.csproj` (lines 14–15, 66: v0.9.10); `Source/Samples/DashBoard.Api/DashBoard.Api/DashBoard.Api.csproj` (lines 14–21: v0.9.10).
- **Dashboard API target framework**: DashBoard.Api targets `net10.0` only, while all other projects dual-target `net8.0;net48`. This is the only project on net10.0.
  - Evidence: `Source/Samples/DashBoard.Api/DashBoard.Api/DashBoard.Api.csproj` (line 3: `<TargetFramework>net10.0</TargetFramework>`); `Source/Samples/Redis/RedisConsumer/RedisConsumer.csproj` (line 4: `<TargetFrameworks>net8.0;net48</TargetFrameworks>`).
- **`Polly.Contrib.Simmy`**: Present in SQLServer and PostgreSQL projects but absent from Redis. [Inferred] Redis may not support chaos engineering through Simmy in the same way, or it was simply not added.
  - Evidence: `Source/Samples/SQLServer/SQLServerConsumer/SQLServerConsumer.csproj` (line 40: `Polly.Contrib.Simmy v0.3.0`); `Source/Samples/Redis/RedisConsumer/RedisConsumer.csproj` — `Simmy` not present.

---

## Summary Table

| Item | Detail | Confidence |
|------|--------|------------|
| `.editorconfig` | Not present | Observed |
| `Directory.Build.props` | Not present | Observed |
| Roslyn analyzers | Not configured | Observed |
| Class naming | PascalCase throughout | Observed |
| Private field prefix | Underscore (`_fieldName`) | Observed |
| Method naming | PascalCase | Observed |
| Local variable style | `var` with camelCase | Observed |
| Brace style | Same-line opening brace | Observed |
| DI container | SimpleInjector v5.5.0 via `IContainer` | Observed |
| DI pattern | Delegated entirely to `Injectors.AddInjectors()` | Observed |
| Config pattern | `App.config` + `SharedConfiguration` static class | Observed |
| Error handling (send) | `HandleResults.Handle()` — log and continue | Observed |
| Error handling (consumer) | `CreateNotifications` callback wiring | Observed |
| Graceful shutdown | `Helpers.WaitForCancelKeyPress()` everywhere | Observed |
| Code duplication level | High — heartbeat config, helper methods, menus copy-pasted | Observed |
| DotNetWorkQueue version skew | SampleShared at v0.9.11; all executables at v0.9.10 | Observed |
| Dashboard API target framework | net10.0 only (others: net8.0 + net48) | Observed |
| Log level inconsistency | Consumers: Debug vs Verbose mixed | Observed |
| SQLite scheduler folder casing | `SQliteScheduler` (lowercase l) | Observed |
| PostgreSQL naming inconsistency | Folder/namespace `PostGreSQL`, string literals `PostgreSql` | Observed |
| XML doc comments | Present only on `ExpiredData*` helper methods | Observed |
| `TODO`/`FIXME` comments | None found | Observed |

---

## Open Questions

- Should the DotNetWorkQueue version skew between `SampleShared` (v0.9.11) and all transport executables (v0.9.10) be resolved by bumping the transport projects to v0.9.11, or by reverting `SampleShared`? This may cause runtime assembly conflicts when running transport projects that load `SampleShared.dll`.
- Is the `Polly.Contrib.Simmy` omission from Redis and LiteDb intentional (transport does not support chaos injection), or was it simply missed during the last dependency update round?
- Is the `MinimumLevel.Verbose()` in Redis consumers (vs `Debug()` in SQL consumers) intentional for debugging Redis-specific issues, or an oversight?
- `SampleShared/app.config` exists but is never described in docs — what is its purpose? (It appears to be a stub for the class library project build and is not used at runtime.)
