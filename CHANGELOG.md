# Changelog

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
