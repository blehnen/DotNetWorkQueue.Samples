# Changelog

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
