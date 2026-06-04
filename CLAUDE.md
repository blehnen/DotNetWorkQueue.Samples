# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Sample applications demonstrating the [DotNetWorkQueue](https://github.com/blehnen/DotNetWorkQueue) distributed work queue library (v0.9.38) across multiple transport backends: Redis, SQL Server, PostgreSQL, SQLite, and LiteDB. Each transport has the same set of sample patterns (Producer, ProducerLinq, Consumer, ConsumerAsync, ConsumerLinq, Scheduler, SchedulerConsumer). SQL Server and PostgreSQL additionally include ProducerOutbox and ConsumerInbox (transactional outbox/inbox patterns introduced in DotNetWorkQueue 0.9.36/0.9.37).

## Release discipline

**When bumping `DotNetWorkQueue.*` package versions (or any transitive pins that come along for the ride), always add a dated entry to `CHANGELOG.md` in the same commit/session.** Also update the `v0.9.xx` reference in the Project Overview above and the dependency list under Architecture → Key Dependencies.

## Build Commands

**SampleShared must be built first** — all other projects reference its compiled DLL via HintPath, not as a ProjectReference.

```bash
# 1. Build the shared library first (required)
dotnet restore "Source/Samples/SampleShared/SampleShared.sln"
dotnet build "Source/Samples/SampleShared/SampleShared.sln" -c Debug

# 2. Then build any transport solution
dotnet restore "Source/Samples/Redis/Samples.sln"
dotnet build "Source/Samples/Redis/Samples.sln" -c Debug

# Available transport solutions:
# Source/Samples/LiteDb/Samples.sln
# Source/Samples/PostgreSQL/Samples.sln
# Source/Samples/Redis/Samples.sln
# Source/Samples/SQLite/Samples.sln
# Source/Samples/SQLServer/Samples.sln

# 3. Build Dashboard.Api (ASP.NET Core, net10.0)
dotnet restore "Source/Samples/DashBoard.Api/DashBoard.Api.sln"
dotnet build "Source/Samples/DashBoard.Api/DashBoard.Api.sln" -c Debug
```

## Integration Tests

MSTest integration tests verify produce-consume round-trips for all 5 transports.

```bash
# Build SampleShared first, then run CI-safe tests (SQLite + LiteDb)
dotnet build "Source/Samples/SampleShared/SampleShared.sln" -c Debug
dotnet build "Source/Samples/IntegrationTests/IntegrationTests.sln" -c Debug
dotnet test "Source/Samples/IntegrationTests/IntegrationTests.sln" -c Debug --filter "TestCategory=CI"

# All transports (needs Redis, SQL Server, PostgreSQL)
dotnet test "Source/Samples/IntegrationTests/IntegrationTests.sln" -c Debug
```

## Architecture

### Target Frameworks

All projects target **net10.0** only. Starting with DotNetWorkQueue 0.9.19, the library dropped its net48 and netstandard2.0 targets, so samples were consolidated onto .NET 10. The SampleShared DLL is referenced via HintPath (`..\..\SampleShared\bin\Debug\net10.0\SampleShared.dll`), so you must build SampleShared in the same configuration before building any transport project.

### Project Structure

- **`Source/Samples/SampleShared/`** — Shared library containing common logic used by all samples: message factories, shared configuration reader, DI/metrics/tracing injectors, producer run loops, and message processing handlers.
- **`Source/Samples/{Transport}/`** — Each transport folder contains a `Samples.sln` and 7 executable projects following the same naming pattern (e.g., `RedisProducer`, `RedisConsumerAsync`, `RedisScheduler`). SQL Server and PostgreSQL each have 9 — the additional 2 are `ProducerOutbox` and `ConsumerInbox` (transactional outbox/inbox samples).
- **`Source/Samples/DashBoard.Api/`** — Standalone ASP.NET Core Dashboard API + UI host (net10.0) that demonstrates the 0.9.37 multi-source dashboard config shape. Reads `Dashboard:Connections` for self-contained API mode and `DashboardApi:Sources[]` for multi-source UI routing. Uses `appsettings.json` for configuration. No SampleShared dependency.

### Configuration

Each sample executable has:
- **`App.config`** — Connection strings, queue name, and feature toggles (`EnableTrace`, `EnableMetrics`, `EnableCompression`, `EnableEncryption`, `EnableChaos`)
- **`tracesettings.json`** — OpenTelemetry/Jaeger exporter configuration (copied to output)
- **`metricsettings.json`** — App.Metrics/InfluxDB reporter configuration (copied to output)

### Key Dependencies

- **DotNetWorkQueue** v0.9.38 + transport-specific packages (including `DotNetWorkQueue.Dashboard.Api`, `DotNetWorkQueue.Dashboard.Ui`)
- **OpenTelemetry** v1.15.3 (tracing via Jaeger)
- **App.Metrics** v4.3.0 (metrics via InfluxDB)
- **Serilog** v4.3.0 (logging)
- **Polly** v8.6.5 (chaos engineering)
- **SimpleInjector** v5.5.2 (DI in executable projects)

## CI

- **Jenkins** (`Jenkinsfile`) — Linux/Docker, net10.0. Builds all solutions, runs CI integration tests (SQLite + LiteDb), then runs LocalOnly tests in parallel (PostgreSQL, SQL Server, Redis) with injected credentials. Uses the same `docker` agent label and credential IDs as the core DotNetWorkQueue project.
- **GitHub Actions** (`.github/workflows/ci.yml`) — Windows, net10.0. Builds all solutions and runs CI-category integration tests. Serves as a .NET 10.0 / Windows compatibility check.
