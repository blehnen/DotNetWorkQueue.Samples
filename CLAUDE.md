# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Sample applications demonstrating the [DotNetWorkQueue](https://github.com/blehnen/DotNetWorkQueue) distributed work queue library (v0.9.13) across multiple transport backends: Redis, SQL Server, PostgreSQL, SQLite, and LiteDB. Each transport has the same set of sample patterns (Producer, ProducerLinq, Consumer, ConsumerAsync, ConsumerLinq, Scheduler, SchedulerConsumer).

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

There are no tests in this repository — it is a samples-only project.

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

- **DotNetWorkQueue** v0.9.13 + transport-specific packages (including `DotNetWorkQueue.Dashboard.Api`)
- **OpenTelemetry** v1.14.0 (tracing via Jaeger)
- **App.Metrics** v4.3.0 (metrics via InfluxDB)
- **Serilog** v4.3.0 (logging)
- **Polly** v8.6.5 (chaos engineering)
- **SimpleInjector** v5.5.0 (DI in executable projects)

## CI

AppVeyor is used for CI (`appveyor.yml`). It restores and builds all 7 solutions (including DashBoard.Api) in Debug configuration using Visual Studio 2022.
