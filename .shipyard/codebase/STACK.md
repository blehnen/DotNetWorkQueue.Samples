# STACK.md

## Overview

DotNetWorkQueue.Samples is a pure C# .NET samples repository with no production application logic of its own. All 37 executable projects are demonstration harnesses for the DotNetWorkQueue library across five transport backends. Every executable dual-targets net8.0 and net48; the sole exception is the Dashboard.Api, which targets net10.0 only. There are no tests and no production deployment artifacts — the repo exists exclusively to show consumers of the library how to wire it up.

## Findings

### Language and Runtime

- **Language**: C# (all source files are `.cs`; no other languages present)
  - Evidence: `Source/Samples/SampleShared/Injectors.cs`, `Source/Samples/SampleShared/SharedConfiguration.cs`, and all project source files
- **Primary target frameworks**: `net8.0` and `net48` (dual-target on all transport sample executables and SampleShared)
  - Evidence: `Source/Samples/SampleShared/SampleShared.csproj` (line 4): `<TargetFrameworks>net8.0;net48</TargetFrameworks>`
  - Evidence: `Source/Samples/Redis/RedisProducer/RedisProducer.csproj` (line 4): same value confirmed
  - Evidence: same pattern confirmed across SQLServer, PostgreSQL, SQLite, and LiteDb producer projects
- **Dashboard.Api target framework**: `net10.0` only (ASP.NET Core web project)
  - Evidence: `Source/Samples/DashBoard.Api/DashBoard.Api/DashBoard.Api.csproj` (line 4): `<TargetFramework>net10.0</TargetFramework>`
  - Note: CLAUDE.md states net8.0, but the csproj shows net10.0 — the file is authoritative
- **net48 runtime declaration**: All `App.config` files declare `supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8"`
  - Evidence: `Source/Samples/Redis/RedisProducer/App.config` (line 4)

### Build System

- **Build tool**: `dotnet` CLI (SDK-style projects throughout)
  - Evidence: all `.csproj` files open with `<Project Sdk="Microsoft.NET.Sdk">` or `<Project Sdk="Microsoft.NET.Sdk.Web">`
- **Solution structure**: 7 separate `.sln` files — one per transport plus one for SampleShared and one for Dashboard.Api
  - Evidence: `Source/Samples/SampleShared/SampleShared.sln`, `Source/Samples/Redis/Samples.sln`, `Source/Samples/PostgreSQL/Samples.sln`, `Source/Samples/SQLServer/Samples.sln`, `Source/Samples/SQLite/Samples.sln`, `Source/Samples/LiteDb/Samples.sln`, `Source/Samples/DashBoard.Api/DashBoard.Api.sln`
- **Required build order**: SampleShared must be compiled before any transport solution. Transport executables reference SampleShared via framework-conditional `HintPath` to the compiled DLL, not as a `ProjectReference`.
  - Evidence: `Source/Samples/Redis/RedisProducer/RedisProducer.csproj` (lines 70-82): separate `<Reference>` blocks for `net48` and `net8.0` each pointing to `..\..\SampleShared\bin\Debug\{framework}\SampleShared.dll`
- **Configurations**: Debug and Release (AnyCPU platform); Debug uses `full` PDB, Release uses `pdbonly`
  - Evidence: `Source/Samples/Redis/RedisProducer/RedisProducer.csproj` (lines 11-16)
- **Binding redirects**: `<AutoGenerateBindingRedirects>true</AutoGenerateBindingRedirects>` on all executable projects (needed for net48 compatibility)
  - Evidence: `Source/Samples/Redis/RedisProducer/RedisProducer.csproj` (line 6)
- **No Directory.Build.props / Directory.Build.targets / global.json**: none found at any level of the repository
- **Visual Studio compatibility**: Solution files reference VS 2017 format (Version 16) for transport solutions, VS 2022 format (Version 17) for SampleShared
  - Evidence: `Source/Samples/Redis/Samples.sln` (line 3): `# Visual Studio Version 16`
  - Evidence: `Source/Samples/SampleShared/SampleShared.sln` (line 3): `# Visual Studio Version 17`

### CI

- **CI provider**: AppVeyor (referenced in CLAUDE.md as `appveyor.yml`)
- **appveyor.yml status**: File not present in the working tree at time of analysis — it may have been deleted or is gitignored
  - [Inferred] Based on CLAUDE.md description: builds all 7 solutions in Debug configuration using Visual Studio 2022

### Package Manager

- **NuGet** via SDK-style `<PackageReference>` in all `.csproj` files
- **dotnet local tool**: `ilspycmd` v9.1.0.7988 (ILSpy command-line decompiler)
  - Evidence: `dotnet-tools.json` (lines 6-9): `"ilspycmd": { "version": "9.1.0.7988" }`
  - [Inferred] Used for inspecting compiled DotNetWorkQueue assemblies; not part of any build step

### Core Queue Library

- **DotNetWorkQueue** — the library under demonstration
  - Version in SampleShared: `0.9.11`
    - Evidence: `Source/Samples/SampleShared/SampleShared.csproj` (line 8)
  - Version in all transport executables: `0.9.10`
    - Evidence: `Source/Samples/Redis/RedisProducer/RedisProducer.csproj` (line 18), `Source/Samples/SQLServer/SQLServerProducer/SQLServerProducer.csproj` (line 14), `Source/Samples/PostgreSQL/PostgreSQLProducer/PostgreSQLProducer.csproj` (line 14), `Source/Samples/SQLite/SQLiteProducer/SQLiteProducer.csproj` (line 17), `Source/Samples/LiteDb/LiteDbProducer/LiteDbProducer.csproj` (line 17)
  - **Version mismatch**: SampleShared references `0.9.11` while all transport executables reference `0.9.10`. This creates a potential assembly binding conflict at runtime.
- **DotNetWorkQueue.Dashboard.Client** `0.9.10` / `0.9.11` — net8.0-only; registers consumers with the dashboard API
  - Version in SampleShared: `0.9.11` (line 23 of SampleShared.csproj, net8.0 conditional)
  - Version in transport executables: `0.9.10` (net8.0 conditional in every transport csproj)
  - Evidence: `Source/Samples/Redis/RedisProducer/RedisProducer.csproj` (lines 84-86)

### Transport Packages

| Transport | NuGet Package | Version | Native Driver |
|-----------|--------------|---------|---------------|
| Redis | `DotNetWorkQueue.Transport.Redis` | 0.9.10 | `StackExchange.Redis` 2.10.1 |
| SQL Server | `DotNetWorkQueue.Transport.SqlServer` | 0.9.10 | `System.Data.SqlClient` 4.9.0 |
| PostgreSQL | `DotNetWorkQueue.Transport.PostgreSQL` | 0.9.10 | `Npgsql` 8.0.8 |
| SQLite | `DotNetWorkQueue.Transport.SQLite` | 0.9.10 | `System.Data.SQLite.Core` 1.0.119 + `Stub.System.Data.SQLite.Core.NetFramework` 1.0.119 |
| LiteDB | `DotNetWorkQueue.Transport.LiteDb` | 0.9.10 | `LiteDB` 5.0.21 |

Evidence: respective producer `.csproj` files for each transport.

### Observability Stack

- **OpenTelemetry** (tracing + metrics)
  - `OpenTelemetry` 1.14.0
  - `OpenTelemetry.Api` 1.14.0
  - `OpenTelemetry.Exporter.Console` 1.14.0
  - `OpenTelemetry.Exporter.OpenTelemetryProtocol` 1.14.0
  - Evidence: `Source/Samples/Redis/RedisProducer/RedisProducer.csproj` (lines 40-43), consistent across all transport projects
- **Serilog** (structured logging)
  - `Serilog` 4.3.0
  - `Serilog.Extensions.Logging` 10.0.0
  - `Serilog.Sinks.Console` 6.1.1
  - `Serilog.AspNetCore` 9.0.0 (Dashboard.Api only)
  - Evidence: `Source/Samples/Redis/RedisProducer/RedisProducer.csproj` (lines 45-47); `Source/Samples/DashBoard.Api/DashBoard.Api/DashBoard.Api.csproj` (line 23)

### Resilience and Chaos Engineering

- **Polly** (via `Polly.Caching.Memory` 3.0.2) — policy/caching support
  - Evidence: `Source/Samples/Redis/RedisProducer/RedisProducer.csproj` (line 44)
  - Note: `Polly` 8.6.5 is declared in SampleShared directly; transport executables use the older `Polly.Caching.Memory` 3.0.2
    - Evidence: `Source/Samples/SampleShared/SampleShared.csproj` (line 16)
- **Polly.Contrib.Simmy** 0.3.0 — chaos engineering (fault injection)
  - Present in: SQLServer, PostgreSQL, SQLite transport projects
  - Absent from: Redis and LiteDb transport projects
  - Evidence: `Source/Samples/SQLServer/SQLServerProducer/SQLServerProducer.csproj` (line 40); `Source/Samples/Redis/RedisProducer/RedisProducer.csproj` — not present

### Dependency Injection

- **SimpleInjector** 5.5.0 — used in all executable projects for DI container
  - Evidence: `Source/Samples/Redis/RedisProducer/RedisProducer.csproj` (line 48), confirmed in all transport producer csproj files

### Serialization

- **Newtonsoft.Json** 13.0.4 — present in all transport executables
  - Evidence: `Source/Samples/Redis/RedisProducer/RedisProducer.csproj` (line 39)
- **MsgPack.Cli** 1.0.1 — MessagePack binary serialization; present in Redis, LiteDb, and Scheduler projects; absent from SQLite and SQLServer
  - Evidence: `Source/Samples/Redis/RedisProducer/RedisProducer.csproj` (line 38); `Source/Samples/SQLServer/SQLServerProducer/SQLServerProducer.csproj` — not present
- **System.Text.Json** 10.0.1 — present in all transport executables
  - Evidence: `Source/Samples/Redis/RedisProducer/RedisProducer.csproj` (line 65)

### Dashboard UI

- **DotNetWorkQueue.Dashboard.Api** 0.9.10 — serves the dashboard REST API
- **DotNetWorkQueue.Dashboard.Ui** 0.9.10 — serves the dashboard Blazor UI
- **MudBlazor** 9.1.0 — Blazor component library for the dashboard UI
  - Evidence: `Source/Samples/DashBoard.Api/DashBoard.Api/DashBoard.Api.csproj` (lines 14-16)

### Microsoft Extensions (shared baseline across all projects)

All transport executables and SampleShared share a common set of `Microsoft.Extensions.*` packages at version 10.0.1:

| Package | Version |
|---------|---------|
| `Microsoft.Extensions.Configuration` | 10.0.1 |
| `Microsoft.Extensions.Configuration.Json` | 10.0.1 |
| `Microsoft.Extensions.DependencyInjection` | 10.0.1 |
| `Microsoft.Extensions.Logging` | 10.0.1 |
| `Microsoft.Extensions.Caching.Memory` | 10.0.1 |
| `Microsoft.Extensions.Options` | 10.0.1 |

Evidence: `Source/Samples/Redis/RedisProducer/RedisProducer.csproj` (lines 20-36), pattern confirmed across all transport csproj files.

### Legacy / Compatibility Packages

These appear in some or all transport executables for net48 compatibility shims:

- `System.Configuration.ConfigurationManager` 10.0.1
- `System.Buffers` 4.6.1, `System.Memory` 4.6.3, `System.ValueTuple` 4.6.1
- `System.Runtime.CompilerServices.Unsafe` 6.1.2
- `System.IO` 4.3.0, `System.Runtime` 4.3.1 (old netstandard polyfills)
- `Microsoft.IO.RecyclableMemoryStream` 3.0.1
- `NETStandard.Library` 2.0.3 (SQLite only — unusual for a multi-targeted project)
  - Evidence: `Source/Samples/SQLite/SQLiteProducer/SQLiteProducer.csproj` (line 43)

### Legacy Tracing Packages (SQLite only)

SQLite projects carry two packages absent from all other transports, suggesting they were not updated when the project migrated to OpenTelemetry:

- `Jaeger.Thrift` 0.3.7
- `Jaeger.Thrift.VendoredThrift` 0.3.7
- `OpenTracing` 0.12.1
  - Evidence: `Source/Samples/SQLite/SQLiteProducer/SQLiteProducer.csproj` (lines 19, 20, 49)

## Summary Table

| Item | Detail | Confidence |
|------|--------|------------|
| Language | C# | Observed |
| Primary frameworks | net8.0 + net48 (dual-target) | Observed |
| Dashboard.Api framework | net10.0 | Observed |
| Build tool | dotnet CLI (SDK-style) | Observed |
| CI provider | AppVeyor | Inferred (CLAUDE.md; yml absent) |
| Local dev tool | ilspycmd 9.1.0.7988 | Observed |
| Package manager | NuGet (PackageReference) | Observed |
| DotNetWorkQueue (SampleShared) | 0.9.11 | Observed |
| DotNetWorkQueue (executables) | 0.9.10 | Observed |
| DI container | SimpleInjector 5.5.0 | Observed |
| Logging | Serilog 4.3.0 | Observed |
| Tracing | OpenTelemetry 1.14.0 via OTLP | Observed |
| Metrics | OpenTelemetry 1.14.0 via OTLP | Observed |
| Chaos engineering | Polly.Contrib.Simmy 0.3.0 (partial) | Observed |
| Dashboard UI | MudBlazor 9.1.0 + Blazor | Observed |
| No Directory.Build.props | Not present anywhere | Observed |
| No global.json | Not present | Observed |
| No .editorconfig | Not present | Observed |

## Open Questions

- The `appveyor.yml` file is referenced in CLAUDE.md but not present on disk — was it deleted, or is it excluded via `.gitignore`?
- SampleShared references `DotNetWorkQueue` 0.9.11 while all transport executables reference 0.9.10. Is this intentional (e.g., SampleShared was updated ahead of the transports) or an oversight?
- `DotNetWorkQueue.Dashboard.Client` also has the same 0.9.11 vs 0.9.10 split between SampleShared and executables.
- SQLite projects retain `Jaeger.Thrift` 0.3.7 and `OpenTracing` 0.12.1 — are these dead dependencies from a prior tracing implementation, or still in active use?
- `Polly.Contrib.Simmy` is missing from Redis and LiteDb transport projects — is chaos engineering intentionally disabled for those transports?
- `NETStandard.Library` 2.0.3 appears only in SQLite projects — is this intentional?
- CLAUDE.md states Dashboard.Api targets `net8.0` but the csproj shows `net10.0` — the documentation should be updated to reflect the actual target.
