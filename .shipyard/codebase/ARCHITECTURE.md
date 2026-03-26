# ARCHITECTURE.md

## Overview

This repository is a **multi-transport samples monorepo** for the DotNetWorkQueue distributed work queue library. It is not a production application; its purpose is to demonstrate five transport backends (Redis, SQL Server, PostgreSQL, SQLite, LiteDB) each exercising the same seven messaging patterns. A shared library (`SampleShared`) provides all cross-cutting logic—message types, DI wiring, metrics, tracing, and producer/consumer handlers—so that each per-transport executable is a thin shell of ~80–130 lines. A standalone ASP.NET Core application (`DashBoard.Api`) provides a Blazor-based monitoring UI over all transports simultaneously.

---

## Findings

### Architectural Pattern

- **Pattern**: Multi-project samples monorepo with a central shared library and a separate monitoring API. Not a production service topology — every executable runs independently and connects directly to its own queue.
  - Evidence: `Source/Samples/SampleShared/SampleShared.csproj` — single class library with no executable entry point
  - Evidence: `Source/Samples/Redis/Samples.sln` — seven executable projects, none referencing each other
  - Evidence: `Source/Samples/DashBoard.Api/DashBoard.Api/DashBoard.Api.csproj` — standalone `Microsoft.NET.Sdk.Web` project with no dependency on any transport sample executable

- **Topology**: Each sample executable is a standalone console application. A producer and consumer for the same transport share the same queue name and connection string (configured in `App.config`), but are run as separate processes.
  - Evidence: `Source/Samples/Redis/RedisProducer/App.config` (key `QueueName = sampleQueue`) and `Source/Samples/Redis/RedisConsumer/App.config` (key `QueueName = sampleQueue`)

---

### Layer Boundaries and Data Flow

#### Producer Flow

1. `Main()` reads `QueueName` and `Database` from `App.config` via `ConfigurationManager.AppSettings`.
2. A `QueueConnection(queueName, connectionString)` value object is constructed.
3. For SQL-backed transports (SQL Server, PostgreSQL, SQLite, LiteDB), the producer first creates the queue schema if absent using a `QueueCreationContainer<TInit>` and transport-specific `QueueCreation` type. Redis skips this step — no schema creation phase.
4. A `QueueContainer<TInit>` is instantiated with a DI registration lambda (cross-cutting concerns wired via `Injectors.AddInjectors`) and an options lambda (`Injectors.SetOptions`).
5. `queueContainer.CreateProducer<SimpleMessage>(queueConnection)` returns an `IProducerQueue<SimpleMessage>`.
6. `RunProducer.RunLoop(queue, ...)` drives an interactive console menu that calls `queue.Send(...)` or `queue.SendAsync(...)` with optional `IAdditionalMessageData` (expiration, delay, user columns).
  - Evidence: `Source/Samples/Redis/RedisProducer/Program.cs` (lines 30–46)
  - Evidence: `Source/Samples/SQLServer/SQLServerProducer/Program.cs` (lines 33–82) — queue-creation phase visible here

#### Consumer Flow

1. Same `QueueConnection` construction from `App.config`.
2. SQL-backed transports verify the queue exists (created by the producer); Redis consumers skip this check.
3. `Injectors.StartDashboardRegistration(queueName, friendlyName)` is called first (net8.0 only) so the `DashboardConsumerClient` is available for DI registration.
4. A `QueueContainer<TInit>` is constructed with the same `Injectors.AddInjectors` + `Injectors.SetOptions` pattern.
5. `queueContainer.CreateConsumer(queueConnection)` returns an `IConsumerQueue`.
6. Queue options are set inline: worker count, heartbeat intervals, retry delay behaviour, message expiration.
7. `queue.Start<SimpleMessage>(MessageProcessing.HandleMessages, CreateNotifications.Create(log))` begins polling.
8. `Helpers.WaitForCancelKeyPress()` blocks until Ctrl+C; the `using` block then disposes the queue gracefully.
9. `Injectors.StopDashboardRegistration(dashboardClient)` tears down the dashboard client (net8.0 only).
  - Evidence: `Source/Samples/Redis/RedisConsumer/Program.cs` (lines 38–73)
  - Evidence: `Source/Samples/SQLServer/SQLServerConsumer/Program.cs` (lines 34–91)
  - Evidence: `Source/Samples/LiteDb/LiteDbConsumer/Program.cs` (lines 31–79)

#### Async Consumer Flow (ConsumerAsync)

Differs from Consumer in container type. Uses a two-container nesting: an outer `SchedulerContainer` creates an `ITaskScheduler` and `ITaskFactory`, the inner `QueueContainer<TInit>` uses `CreateConsumerQueueScheduler(queueConnection, factory)`. The scheduler thread pool (`MaximumThreads = 8`) does the work; only one thread polls the queue (`WorkerCount = 1`).
  - Evidence: `Source/Samples/Redis/RedisConsumerAsync/Program.cs` (lines 32–77)

#### Linq Producer Flow (ProducerLinq)

Same as Producer except `queueContainer.CreateMethodProducer(queueConnection)` returns an `IProducerMethodQueue`. The `RunProducer.RunLoop(queue, ...)` overload sends lambda expressions (static or, on net48 only, dynamic string-compiled) rather than typed message objects.
  - Evidence: `Source/Samples/Redis/RedisProducerLinq/Program.cs` (lines 34–46)
  - Evidence: `Source/Samples/SampleShared/RunProducer.cs` (lines 42–97) — `RunStatic`, `RunDynamic`, `RunStaticAsync`, `RunDynamicAsync`

#### Linq Consumer Flow (ConsumerLinq / SchedulerConsumer)

Uses `CreateConsumerMethodQueueScheduler(queueConnection, factory)`. The message handler is not passed to `Start()` — the work to execute is embedded in the message itself (the lambda sent by ProducerLinq). `queue.Start(CreateNotifications.Create(log))` takes only a notifications object.
  - Evidence: `Source/Samples/Redis/RedisConsumerLinq/Program.cs` (line 76)
  - Evidence: `Source/Samples/Redis/RedisSchedulerConsumer/Program.cs` (line 72)

#### Scheduler Flow

Uses `JobSchedulerContainer` (not `QueueContainer`) to create a `IJobScheduler`. Jobs are added interactively via `scheduler.AddUpdateJob<TQueueInit, TJobQueueCreation>(name, queueConnection, cronExpression, lambda)`. The scheduler fires the lambda on the cron schedule and enqueues it to the named queue. A separate SchedulerConsumer process dequeues and executes.
  - Evidence: `Source/Samples/Redis/RedisScheduler/Program.cs` (lines 31–141)

---

### DI and Container Pattern

- **DI framework in executables**: SimpleInjector v5.5.0, accessed through DotNetWorkQueue's `IContainer` abstraction.
  - Evidence: `Source/Samples/Redis/RedisProducer/RedisProducer.csproj` — `<PackageReference Include="SimpleInjector" Version="5.5.0" />`

- **Registration pattern**: All cross-cutting services are registered through the `Injectors.AddInjectors(logFactory, addTrace, addMetrics, enableGzip, enableEncryption, appName, container)` static method in `SampleShared`. This is invoked as a lambda during `QueueContainer<TInit>` or `SchedulerContainer` construction and must not be called after container creation.
  - Evidence: `Source/Samples/SampleShared/Injectors.cs` (lines 29–57)

- **Service registrations performed by `AddInjectors`**:
  - `ILoggerFactory` — always registered as Singleton
  - `IMetrics` (MetricsNet) — registered if `addMetrics = true`; static field prevents duplicate registration when multiple containers are created in the same process
  - `ActivitySource` (OpenTelemetry tracer) — registered if `addTrace = true`; same static-field deduplication
  - `IMessageInterceptor` collection (GZip, TripleDES) — registered if compression/encryption enabled
  - `IConsumerMetricsNotification` — registered if a `DashboardConsumerClient` is already started (net8.0 only)
  - Evidence: `Source/Samples/SampleShared/Injectors.cs` (lines 29–256)

- **Post-construction options**: `Injectors.SetOptions(container, enableChaos)` is called via the options lambda to set `IPolicies.EnableChaos` and `IHistoryConfiguration.Enabled`. These must be set before `queue.Start()`.
  - Evidence: `Source/Samples/SampleShared/Injectors.cs` (lines 59–66)

- **Dashboard client lifecycle** (net8.0 only): `StartDashboardRegistration` must be called *before* constructing `QueueContainer`, because `AddInjectors` checks a static `_dashboardClient` field and conditionally registers `IConsumerMetricsNotification` into the container.
  - Evidence: `Source/Samples/SampleShared/Injectors.cs` (lines 176–218)
  - Evidence: `Source/Samples/Redis/RedisConsumer/Program.cs` (lines 38–42) — `StartDashboardRegistration` precedes `new QueueContainer<...>`

---

### Configuration Pattern

- **Runtime flags** (`App.config` `<appSettings>`): Every executable reads the same fixed set of keys via the static `SharedConfiguration` class constructor.

| Key | Type | Purpose |
|-----|------|---------|
| `Database` | string | Transport connection string |
| `QueueName` | string | Queue name for this sample |
| `EnableTrace` | bool | OpenTelemetry/Jaeger tracing on/off |
| `EnableMetrics` | bool | OTLP metrics export on/off |
| `EnableCompression` | bool | GZip message interceptor on/off |
| `EnableEncryption` | bool | TripleDES message interceptor on/off |
| `EnableChaos` | bool | Polly chaos policy on/off |
| `EnableDashboard` | bool | Dashboard client registration on/off |
| `EnableHistory` | bool | Message history tracking on/off |
| `DashboardApiUrl` | string | URL of the Dashboard.Api process (default `https://localhost:32906`) |

  - Evidence: `Source/Samples/SampleShared/SharedConfiguration.cs` (lines 9–44)
  - Evidence: `Source/Samples/Redis/RedisConsumer/App.config`

- **Trace configuration** (`tracesettings.json`, copied to output directory): Contains Jaeger endpoint (`JAEGER_AGENT_HOST`, `JAEGER_AGENT_PORT`, `JAEGER_SERVICE_NAME`). Read by `Injectors.AddTrace()` using `Microsoft.Extensions.Configuration`.
  - Evidence: `Source/Samples/Redis/RedisConsumer/tracesettings.json`
  - Evidence: `Source/Samples/SampleShared/Injectors.cs` (lines 220–256)

- **Metrics configuration** (`metricsettings.json`, linked from SampleShared output): Contains `Metrics.OtlpEndpoint`. If absent or empty, metrics fall back to console output.
  - Evidence: `Source/Samples/SampleShared/metricsettings.json` — `"OtlpEndpoint": "http://192.168.0.2:9090/api/v1/otlp/v1/metrics"`
  - Evidence: `Source/Samples/SampleShared/Injectors.cs` (lines 116–155)
  - Evidence: `Source/Samples/Redis/RedisProducer/RedisProducer.csproj` (lines 93–96) — metricsettings.json linked via `Content Include` pointing to `SampleShared`

- **SQL Server transport-specific key**: `UseUserDequeue` (bool) and `UserDayOfWeek` (int) — SQL Server consumer and producer only, for demonstrating user-column filtering.
  - Evidence: `Source/Samples/SQLServer/SQLServerConsumer/App.config` (lines 17–18)
  - Evidence: `Source/Samples/SQLServer/SQLServerConsumer/Program.cs` (lines 71–77)

---

### Message Types and the Linq Pattern

Two distinct message styles are demonstrated:

- **Typed messages** (`SimpleMessage`): A POCO with `Message` (random string payload), `ProcessingTime` (milliseconds to sleep), and `Error` (enum driving error simulation). Used by Producer/Consumer pairs.
  - Evidence: `Source/Samples/SampleShared/SimpleMessage.cs` (lines 8–22)

- **Linq method messages**: Lambda expressions serialised as delegates (static) or as string expressions compiled at runtime (dynamic, net48 only). The payload is the work itself — the consumer needs no `HandleMessages` callback. Used by ProducerLinq/ConsumerLinq/SchedulerConsumer.
  - Evidence: `Source/Samples/SampleShared/RunProducer.cs` (lines 42–97) — `RunStatic` sends `(message, workerNotification) => new TestClass().RunMe(...)` as delegate; `RunDynamic` sends a string expression
  - Evidence: `Source/Samples/SampleShared/SimpleMessage.cs` (lines 24–54) — `TestClass.RunMe` is the target method for Linq messages

---

### Message Processing (Consumer Side)

- **Handler**: `MessageProcessing.HandleMessages(IReceivedMessage<SimpleMessage>, IWorkerNotification)` is the single shared handler for all typed consumers across all transports. It simulates normal completion, hard error (divide-by-zero), retryable error (throws `InvalidDataException`), and retryable-then-succeed.
  - Evidence: `Source/Samples/SampleShared/MessageProcessing.cs`

- **Cancellation**: Uses `arg2.MessageCancellation.Token.WaitHandle.WaitOne(processingTime)` when `TransportSupportsRollback` is true, allowing graceful cancellation via Ctrl+C or dashboard cancel without data loss. Falls back to `Thread.Sleep` for non-rollback transports.
  - Evidence: `Source/Samples/SampleShared/MessageProcessing.cs` (lines 72–84)

- **Retry configuration**: Set identically across all consumers — `InvalidDataException` retried 3 times with delays of 3s, 6s, 9s.
  - Evidence: `Source/Samples/Redis/RedisConsumer/Program.cs` (line 57); cross-verified in `Source/Samples/LiteDb/LiteDbConsumer/Program.cs` (line 63) and `Source/Samples/SQLServer/SQLServerSchedulerConsumer/Program.cs` (lines 88–92)

- **Notifications**: `CreateNotifications.Create(log)` builds a `ConsumerQueueNotifications` instance with callbacks for: error, receive error, message-moved-to-error-queue, poison message, rollback, and completion. All callbacks log via Serilog.
  - Evidence: `Source/Samples/SampleShared/CreateNotifications.cs`

---

### Dashboard.Api Architecture

The dashboard is a fully separate ASP.NET Core application (no dependency on any sample executable). It runs two co-hosted components in a single process:

- **REST API** (`DotNetWorkQueue.Dashboard.Api`): Registered via `builder.Services.AddDotNetWorkQueueDashboard(options => ...)`. Exposes queue inspection endpoints. API key authentication via `X-Api-Key` header.
- **Blazor Server UI** (`DotNetWorkQueue.Dashboard.Ui`): Registered via `builder.Services.AddRazorComponents()`. Uses MudBlazor v9.1.0 for components. The UI calls its own REST API via a named `HttpClient<IDashboardApiClient>` pointed at `ASPNETCORE_URLS` (default `https://localhost:32906`).
- **Authentication**: Cookie-based (`CookieAuthenticationDefaults`). Username and SHA-256 password hash stored in `appsettings.json`. Login at `/auth/login`, logout at `/auth/logout`.
- **Transport registration**: Each transport is registered at startup by reading the `Dashboard:Connections` array from `appsettings.json` and calling the appropriate `options.AddConnection<TInit>(...)`. All five transports are supported (SqlServer, PostgreSql, SQLite, LiteDb, Redis).
  - Evidence: `Source/Samples/DashBoard.Api/DashBoard.Api/Program.cs` (lines 17–180)
  - Evidence: `Source/Samples/DashBoard.Api/DashBoard.Api/appsettings.example.json`

- **Consumer-to-dashboard telemetry**: Sample consumers (net8.0) register a `DashboardConsumerClient` that pushes processed/errored/rolled-back/poison-message counters to the dashboard API via HTTP. The client is wired into DotNetWorkQueue's `IConsumerMetricsNotification` interface.
  - Evidence: `Source/Samples/SampleShared/Injectors.cs` (lines 175–217)

- **Target framework note**: The `DashBoard.Api.csproj` targets `net10.0` (not `net8.0` as stated in CLAUDE.md). It also has a `HintPath` reference to `SampleShared` compiled for `net8.0`, which is a framework mismatch if the project is built with .NET 10 SDK.
  - Evidence: `Source/Samples/DashBoard.Api/DashBoard.Api/DashBoard.Api.csproj` (line 4: `<TargetFramework>net10.0</TargetFramework>`, line 30: `net8.0\SampleShared.dll`)

---

### Observability Architecture

Three observability channels, all optional and controlled by `App.config` flags:

| Channel | Mechanism | Sink | Config file |
|---------|-----------|------|-------------|
| Tracing | OpenTelemetry `ActivitySource` + OTLP exporter | Jaeger (or any OTLP collector) | `tracesettings.json` per project |
| Metrics | OpenTelemetry `MeterProvider` (`DotNetWorkQueue` meter) + OTLP exporter | Prometheus (OTLP push) or console fallback | `metricsettings.json` in SampleShared (linked) |
| Logging | Serilog, bridged to `Microsoft.Extensions.Logging` | Console | Inline in `Main()` |

The `_tracer` and `_metrics` / `_meterProvider` instances are stored as static fields in `Injectors`, preventing duplicate registration when the same process creates more than one container (e.g., `SchedulerContainer` + `QueueContainer` in ConsumerAsync).
  - Evidence: `Source/Samples/SampleShared/Injectors.cs` (lines 25–27, 103–155, 220–256)

---

### Transport Behavioural Differences

| Transport | Queue creation | Schema owner | Rollback support | User columns |
|-----------|---------------|-------------|-----------------|--------------|
| Redis | None (schema-less) | N/A | [Inferred] Yes (key-based TTL) | No |
| SQL Server | Producer creates tables | Producer | Yes | Yes (configurable) |
| PostgreSQL | Producer creates tables | Producer | Yes | [Inferred] possible |
| SQLite | Producer creates tables | Producer | Yes | [Inferred] possible |
| LiteDB | Producer creates tables | Producer | Yes | [Inferred] possible |

For Redis, the consumer constructs a `RedisQueueTransportOptions` object (with `SntpTimeConfiguration`) before creating the container — the only transport where transport-level options are explicitly constructed in `Main()`.
  - Evidence: `Source/Samples/Redis/RedisConsumer/Program.cs` (lines 32–36)

---

## Summary Table

| Item | Detail | Confidence |
|------|--------|------------|
| Architectural pattern | Multi-transport samples monorepo, shared library + standalone API | Observed |
| Shared library role | DI wiring, message types, handlers, observability, producer run loops | Observed |
| DI framework | SimpleInjector v5.5.0 via DotNetWorkQueue `IContainer` abstraction | Observed |
| Container strategy | QueueContainer / SchedulerContainer / JobSchedulerContainer per pattern | Observed |
| Message types | Typed (`SimpleMessage`) and Linq (delegate / string expression) | Observed |
| Configuration source | `App.config` `<appSettings>` for all runtime flags | Observed |
| Trace config | `tracesettings.json` per project, OTLP to Jaeger | Observed |
| Metrics config | `metricsettings.json` in SampleShared (linked), OTLP to Prometheus | Observed |
| Dashboard transport | HTTP push from consumer via `DashboardConsumerClient` (net8.0+) | Observed |
| Dashboard UI/API | Co-hosted Blazor Server + REST API, cookie auth, MudBlazor | Observed |
| Dashboard target framework | `net10.0` in csproj (CLAUDE.md says net8.0) | Observed |
| SampleShared HintPath in Dashboard | Points to `net8.0` build despite project targeting `net10.0` | Observed |
| Dynamic Linq messages | Net48 only (string-compiled expressions) | Observed |
| Redis queue creation | No schema creation step (Redis is schema-less) | Observed |
| SQL Server user columns | Optional additional dequeue filter column on metadata table | Observed |
| Retry config | Identical across all consumers: InvalidDataException, 3s/6s/9s | Observed |
| Heartbeat config | Identical across all consumers: update 10s, monitor 15s, dead after 35s | Observed |
| CI system | GitHub Actions (`.github/workflows/ci.yml`), windows-latest, .NET 8 SDK | Observed |

---

## Open Questions

- The `DashBoard.Api.csproj` targets `net10.0` but the CI workflow installs only the .NET 8 SDK (`dotnet-version: '8.0.x'`). It is unclear whether the CI build of `DashBoard.Api.sln` succeeds under .NET 8 when the project requests `net10.0`, or whether the CI workflow has drifted from the actual project file.
- The `SampleShared` HintPath in `DashBoard.Api.csproj` points to `net8.0\SampleShared.dll`. If the Dashboard project is run under .NET 10 and SampleShared was only built for net8.0/net48, this may work via compatibility but has not been verified.
- `RandomString.cs` was listed in the SampleShared directory but not read. Its contents are [Inferred] to generate random alphanumeric strings used for message payloads in `Messages.cs`.
- The `DotNetWorkQueue.Metrics.Net` package (imported in `Injectors.cs`) provides the `MetricsNet` class; whether this is the App.Metrics-based implementation mentioned in CLAUDE.md or the newer System.Diagnostics.Metrics wrapper is not confirmed from the package reference alone. The comment in `Injectors.cs` (line 111) indicates the transition to `System.Diagnostics.Metrics` has occurred.
- No LiteDB or PostgreSQL Scheduler `Program.cs` was read. Behaviour is [Inferred] identical to the Redis and SQL Server equivalents based on the pattern observed across all other transports.
