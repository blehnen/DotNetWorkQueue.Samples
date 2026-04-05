# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Sample applications demonstrating the [DotNetWorkQueue](https://github.com/blehnen/DotNetWorkQueue) distributed work queue library (v0.9.14) across multiple transport backends: Redis, SQL Server, PostgreSQL, SQLite, and LiteDB. Each transport has the same set of sample patterns (Producer, ProducerLinq, Consumer, ConsumerAsync, ConsumerLinq, Scheduler, SchedulerConsumer).

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

# 3. Build Dashboard.Api (ASP.NET Core, net8.0 only)
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

All projects dual-target **net8.0** and **net48**. The SampleShared DLL is referenced via framework-conditional HintPath (e.g., `..\..\SampleShared\bin\Debug\net8.0\SampleShared.dll`), so you must build SampleShared in the same configuration before building transport projects.

### Project Structure

- **`Source/Samples/SampleShared/`** — Shared library containing common logic used by all samples: message factories, shared configuration reader, DI/metrics/tracing injectors, producer run loops, and message processing handlers.
- **`Source/Samples/{Transport}/`** — Each transport folder contains a `Samples.sln` and 7 executable projects following the same naming pattern (e.g., `RedisProducer`, `RedisConsumerAsync`, `RedisScheduler`).
- **`Source/Samples/DashBoard.Api/`** — Standalone ASP.NET Core dashboard API (net8.0 only) that monitors queues across all transports. Uses `appsettings.json` for configuration. No SampleShared dependency.

### Configuration

Each sample executable has:
- **`App.config`** — Connection strings, queue name, and feature toggles (`EnableTrace`, `EnableMetrics`, `EnableCompression`, `EnableEncryption`, `EnableChaos`)
- **`tracesettings.json`** — OpenTelemetry/Jaeger exporter configuration (copied to output)
- **`metricsettings.json`** — App.Metrics/InfluxDB reporter configuration (copied to output)

### Key Dependencies

- **DotNetWorkQueue** v0.9.14 + transport-specific packages (including `DotNetWorkQueue.Dashboard.Api`, `DotNetWorkQueue.Dashboard.Ui`)
- **OpenTelemetry** v1.14.0 (tracing via Jaeger)
- **App.Metrics** v4.3.0 (metrics via InfluxDB)
- **Serilog** v4.3.0 (logging)
- **Polly** v8.6.5 (chaos engineering)
- **SimpleInjector** v5.5.0 (DI in executable projects)

## CI

- **Jenkins** (`Jenkinsfile`) — Linux/Docker, net10.0. Builds all solutions, runs CI integration tests (SQLite + LiteDb), then runs LocalOnly tests in parallel (PostgreSQL, SQL Server, Redis) with injected credentials. Uses the same `docker` agent label and credential IDs as the core DotNetWorkQueue project.
- **GitHub Actions** (`.github/workflows/ci.yml`) — Windows, net8.0. Builds all solutions and runs CI-category integration tests. Serves as a .NET 8.0 / Windows compatibility check.
