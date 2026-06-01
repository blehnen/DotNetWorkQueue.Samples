# Changelog

### 2026-05-31 — DotNetWorkQueue 0.9.37 upgrade + outbox & inbox samples

- Bump every `DotNetWorkQueue.*` package (core, transports, Dashboard.Api/Ui/Client) from **0.9.35** to **0.9.37** across 39 csproj files (124 `PackageReference` attributes).
- Align explicit transitive pins with what the 0.9.37 dependency tree requires (NU1605 avoidance):
  - `Microsoft.Extensions.*` 10.0.7 → **10.0.8** (17 packages)
  - `Npgsql` 10.0.2 → **10.0.3**
  - `StackExchange.Redis` 2.12.14 → **2.13.17**
  - `SimpleInjector` 5.5.1 → **5.5.2**
  - `MudBlazor` 9.3.0 → **9.5.0** (Dashboard.Api only)
- Fix GitHub Actions CI: `.github/workflows/ci.yml` `dotnet-version` updated from `8.0.x` to `10.0.x` to match the net10.0 sample target.
- Add `SQLServerProducerOutbox` and `PostgreSQLProducerOutbox` sample projects: open a business connection, begin a transaction, insert an `Orders` row, capability-cast the producer to `IRelationalProducerQueue<OrderCreatedEvent>`, call `Send(msg, tx)`, then commit or rollback — demonstrating that a rollback leaves both the business row and the queued message absent. Add `SqlServerOutboxTests` and `PostgreSqlOutboxTests` (LocalOnly) to `IntegrationTests` covering both commit and rollback paths.
- Add `SQLServerConsumerInbox` and `PostgreSQLConsumerInbox` sample projects: configure the consumer with `EnableHoldTransactionUntilMessageCommitted = true`, then delegate to `InboxMessageProcessing.HandleMessages`, which casts `IWorkerNotification` to `IRelationalWorkerNotification` and writes to `OrdersProjection` on the library-owned transaction atomically. Add `SqlServerInboxTests` and `PostgreSqlInboxTests` (LocalOnly) to `IntegrationTests` covering commit (projection row written) and rollback (`ForceRollback=true` causes handler throw → queue and projection both rolled back).
- Add `DotNetWorkQueue.Transport.RelationalDatabase` @0.9.37 to `SampleShared.csproj`; add `OrderCreatedEvent.cs` POCO and `InboxMessageProcessing.cs` shared handler (used by both inbox sample projects).
- Verified with clean restore + build across SampleShared, all 5 transport solutions, Dashboard.Api, and IntegrationTests (0 warnings, 0 errors). CI-category integration tests pass on SQLite + LiteDb; LocalOnly outbox/inbox tests pass on developer SQL Server + PostgreSQL instances.

### 2026-04-23 — DotNetWorkQueue 0.9.35 upgrade

- Bump every `DotNetWorkQueue.*` package (core, transports, Dashboard.Api/Ui/Client) from **0.9.31** to **0.9.35** across 39 csproj files.
- Align explicit transitive pins with what the 0.9.35 dependency tree requires (NU1605 avoidance):
  - `Microsoft.Extensions.*` 10.0.1 → **10.0.7** (17 packages)
  - `OpenTelemetry.*` 1.14.0 → **1.15.3** (4 packages)
  - `StackExchange.Redis` 2.10.1 → **2.12.14**
  - `Npgsql` 8.0.8 → **10.0.2**
  - `MudBlazor` 9.1.0 → **9.3.0**
  - `SimpleInjector` 5.5.0 → **5.5.1**
- Verified with clean restore + build across SampleShared, all 5 transport solutions, Dashboard.Api, and IntegrationTests (0 warnings, 0 errors). CI-category integration tests pass on SQLite + LiteDb.

### 2026-04-10 — DotNetWorkQueue 0.9.31 upgrade

- Upgrade every sample project from DotNetWorkQueue 0.9.14 (core and transports) and 0.9.16 (Dashboard) to **0.9.31**.
- Drop `net48` targeting from all samples; every sample executable, `SampleShared`, `DashBoard.Api`, and `IntegrationTests` now targets **net10.0** only. Required by DotNetWorkQueue 0.9.19, which dropped net48 and netstandard2.0.
- Remove the `#if net48` dynamic-LINQ helpers (`RunDynamic`, `RunDynamicAsync`) and their `RunLoop` dispatch cases from `SampleShared/RunProducer.cs`. Dynamic LINQ (JpLabs.DynamicCode) was removed from the library in 0.9.19 — it was net48-only. Static method LINQ samples are unaffected.
- Convert every Schyntax schedule string to 6-field cron (Cronos), as required by 0.9.30:
  - `sec(*%10)` → `*/10 * * * * *` (heartbeats across 22 files)
  - `sec(0,5,10,15,20,25,30,35,40,45,50,55)` → `*/5 * * * * *`
  - `min(*)` → `0 * * * * *`
  - `sec(30)` → `30 * * * * *`
- Migrate `DashBoard.Api` to the 0.9.31 multi-source dashboard config shape:
  - `Program.cs` rewritten to mirror the canonical `DotNetWorkQueue.Dashboard.Ui/Program.cs`, using `DashboardConfigParser.ValidateNoLegacyConfig`, `SourceRegistry`, `MultiSourceDashboardApiClient`, `SourceHealthMonitor`, per-source `HttpClient`, and `LocalSourceHostedService` for self-contained mode.
  - `appsettings.json` / `appsettings.example.json` now use top-level `DashboardApi:Sources[]` and `DashboardAuth`. The former nested `Dashboard:Auth` section is gone.

### 2026-04-03
- Update all DotNetWorkQueue.* packages to 0.9.14
- Simplify Dashboard.Api to use new `IConfiguration` overload for transport registration (replaces manual `AddConnectionByTransport` switch)
- Remove unused SampleShared reference from Dashboard.Api

### 2026-03-30
- Update all DotNetWorkQueue.* packages to 0.9.13
- Add MSTest integration test project verifying produce-consume round-trips for all 5 transports
- SQLite and LiteDb tests run in CI; Redis, SQL Server, PostgreSQL tests are local-only

### 2026-03-27
- Update all DotNetWorkQueue.* packages to 0.9.11

### 2026-03-20
- Update all DotNetWorkQueue.* packages to 0.9.10
- Add message history support: `EnableHistory` setting in App.config, queue creation options, and consumer/producer configuration
- Add per-message cancellation support: `MessageCancellation.Token` replaces `WorkerStopping.StopWorkToken` in message handler. Note: dashboard-initiated cancel requires in-process queues (consumer and dashboard in the same process); the samples run consumers as separate processes, so the dashboard cancel button will not work out of the box
- Wire `IConsumerMetricsNotification` via `Injectors.SetOptions()` so consumer metric counters (processed, errors, rollbacks, poison messages) flow automatically to the dashboard
- Add `DotNetWorkQueue.Dashboard.Client` to all producer and scheduler projects
- Move dashboard client creation before `QueueContainer` in all consumer samples for correct DI registration
- Revert `CreateNotifications.cs` to simple form — metrics are now handled by the core library pipeline
