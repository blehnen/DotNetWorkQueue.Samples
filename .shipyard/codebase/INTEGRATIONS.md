# INTEGRATIONS.md

## Overview

DotNetWorkQueue.Samples integrates with five distinct storage/messaging backends (Redis, SQL Server, PostgreSQL, SQLite, LiteDB), an OpenTelemetry-compatible tracing backend (configured for Jaeger-protocol OTLP), a Prometheus-compatible metrics endpoint (via OTLP over HTTP), and an optional in-process dashboard API. All external endpoints are developer-local by default and are configured through per-project `App.config` and JSON settings files — there are no environment variable patterns or secrets management beyond plaintext config files.

## Findings

### Queue Transport Backends

Each transport is independently configurable via the `Database` key in each project's `App.config`. The five transports are:

#### Redis
- **Package**: `DotNetWorkQueue.Transport.Redis` 0.9.10 + `StackExchange.Redis` 2.10.1
- **Connection format**: `<host>,defaultDatabase=<n>,syncTimeout=<ms>`
- **Sample connection**: `192.168.0.2,defaultDatabase=1,syncTimeout=15000`
  - Evidence: `Source/Samples/Redis/RedisProducer/App.config` (line 7)
- **Dashboard connection**: same format used in Dashboard.Api config
  - Evidence: `Source/Samples/DashBoard.Api/DashBoard.Api/appsettings.json` (line 43)
- **Queues referenced in dashboard**: `sampleQueue`, `sampleQueueLinq`, `sampleQueueScheduler`
  - Evidence: `Source/Samples/DashBoard.Api/DashBoard.Api/appsettings.json` (lines 44-46)

#### SQL Server
- **Package**: `DotNetWorkQueue.Transport.SqlServer` 0.9.10 + `System.Data.SqlClient` 4.9.0
- **Connection format**: standard ADO.NET SQL Server connection string
- **Sample connection**: `Server=192.168.0.2;Application Name=IntegrationTesting;Database=IntegrationTests;user=brian;password=123abc;max pool size=500;TrustServerCertificate=True;`
  - Evidence: `Source/Samples/SQLServer/SQLServerProducer/App.config` (line 7)
- **Transport-specific option**: `UseUserDequeue` toggle in App.config (false by default)
  - Evidence: `Source/Samples/SQLServer/SQLServerProducer/App.config` (line 15)
- **Dashboard connection**: `Server=192.168.0.2;Database=IntegrationTests;user=brian;password=123abc;TrustServerCertificate=True;`
  - Evidence: `Source/Samples/DashBoard.Api/DashBoard.Api/appsettings.json` (line 20)
- **Queue referenced in dashboard**: `sampleQueueExample`

#### PostgreSQL
- **Package**: `DotNetWorkQueue.Transport.PostgreSQL` 0.9.10 + `Npgsql` 8.0.8
- **Connection format**: Npgsql connection string
- **Sample connection**: `Server=192.168.0.2;Port=5432;Database=integrationtesting;Maximum Pool Size=250;userid=brian;Trust Server Certificate=true`
  - Evidence: `Source/Samples/PostgreSQL/PostgreSQLProducer/App.config` (line 7)
- **Dashboard connection**: `Server=192.168.0.2;Port=5432;Database=integrationtesting;userid=brian;Trust Server Certificate=true`
  - Evidence: `Source/Samples/DashBoard.Api/DashBoard.Api/appsettings.json` (line 26)
- **Queues referenced in dashboard**: `sampleQueueNew`, `sampleQueueLinq`, `sampleQueueScheduler`

#### SQLite
- **Package**: `DotNetWorkQueue.Transport.SQLite` 0.9.10 + `System.Data.SQLite.Core` 1.0.119
- **Connection format**: SQLite file path (relative path in sample, absolute in Dashboard.Api)
- **Sample connection**: `\test.db3` (relative to executable working directory)
  - Evidence: `Source/Samples/SQLite/SQLiteProducer/App.config` (line 7)
- **Dashboard connection**: `Data Source=C:\Users\brian\Documents\test.db3;Version=3;` (absolute, developer-local)
  - Evidence: `Source/Samples/DashBoard.Api/DashBoard.Api/appsettings.json` (line 34)
- **Queue referenced in dashboard**: `testing`

#### LiteDB
- **Package**: `DotNetWorkQueue.Transport.LiteDb` 0.9.10 + `LiteDB` 5.0.21
- **Connection format**: LiteDB connection string
- **Sample connection**: `\test.db` (relative path)
  - Evidence: `Source/Samples/LiteDb/LiteDbProducer/App.config` (line 7)
- **Dashboard connection**: `Filename=C:\Users\brian\Documents\test.db;Connection=shared;` (absolute, developer-local)
  - Evidence: `Source/Samples/DashBoard.Api/DashBoard.Api/appsettings.json` (line 40)
- **Queue referenced in dashboard**: `testing`

### Distributed Tracing (OpenTelemetry / Jaeger)

- **Protocol**: OTLP (OpenTelemetry Protocol) over HTTP, targeting a Jaeger-compatible backend
- **Configuration file**: `tracesettings.json` copied to each executable's output directory
- **Configuration keys**: `JAEGER_SERVICE_NAME`, `JAEGER_AGENT_HOST`, `JAEGER_AGENT_PORT`
- **Default endpoint**: `http://192.168.0.2:4319` (OTLP HTTP port)
- **Service name pattern**: `dotnetworkqueue-{Transport}-sample` (e.g., `dotnetworkqueue-Redis-sample`)
  - Evidence: `Source/Samples/Redis/RedisProducer/tracesettings.json`
- **Exporter configuration in code**: Batch exporter with queue size 2048, 5s scheduled delay, 30s timeout, batch size 512
  - Evidence: `Source/Samples/SampleShared/Injectors.cs` (lines 232-252)
- **Packages**: `OpenTelemetry.Exporter.OpenTelemetryProtocol` 1.14.0, `OpenTelemetry.Api` 1.14.0
- **Toggle**: `EnableTrace` in `App.config` — defaults vary by transport and project type
  - Evidence: `Source/Samples/Redis/RedisProducer/App.config` (line 9): `true`; `Source/Samples/LiteDb/LiteDbProducer/App.config` (line 9): `false`

### Metrics (OpenTelemetry / Prometheus)

- **Protocol**: OTLP over HTTP (Prometheus OTLP ingestion endpoint, available since Prometheus v2.47)
- **Configuration file**: `metricsettings.json` — single file in SampleShared, linked into each executable's output via `Content` element
  - Evidence: `Source/Samples/Redis/RedisProducer/RedisProducer.csproj` (lines 93-96): `<Content Include="..\..\SampleShared\metricsettings.json" Link="metricsettings.json">`
- **Configuration key**: `Metrics:OtlpEndpoint`
- **Default endpoint**: `http://192.168.0.2:9090/api/v1/otlp/v1/metrics`
  - Evidence: `Source/Samples/SampleShared/metricsettings.json` (line 3)
- **Export interval**: 5 seconds (hardcoded in `Injectors.cs`)
  - Evidence: `Source/Samples/SampleShared/Injectors.cs` (lines 128-131)
- **Fallback**: if `OtlpEndpoint` is absent or `metricsettings.json` does not exist, falls back to console exporter
  - Evidence: `Source/Samples/SampleShared/Injectors.cs` (lines 136-144)
- **Prometheus start command**: `prometheus --config.file=prometheus.yml --web.enable-otlp-receiver`
  - Evidence: `prometheus.yml` (lines 8-9)
- **Meter name**: `DotNetWorkQueue` (hardcoded in `AddMeter` call)
  - Evidence: `Source/Samples/SampleShared/Injectors.cs` (line 120)
- **Toggle**: `EnableMetrics` in `App.config`; defaults to `true` for Redis, `false` for SQLServer, PostgreSQL, SQLite, LiteDb producers
  - Evidence: `Source/Samples/Redis/RedisProducer/App.config` (line 10); `Source/Samples/SQLServer/SQLServerProducer/App.config` (line 10)
- **Grafana dashboard**: `grafana-dashboard.json` present at repo root for visualising the Prometheus metrics
  - Evidence: repo root `grafana-dashboard.json`

### Dashboard API

- **Package**: `DotNetWorkQueue.Dashboard.Api` 0.9.10 + `DotNetWorkQueue.Dashboard.Ui` 0.9.10
- **Framework**: ASP.NET Core (net10.0), SDK `Microsoft.NET.Sdk.Web`
  - Evidence: `Source/Samples/DashBoard.Api/DashBoard.Api/DashBoard.Api.csproj` (lines 1, 4)
- **Configuration file**: `Source/Samples/DashBoard.Api/DashBoard.Api/appsettings.json`
- **Default URL**: `https://localhost:32906` (hardcoded default in `SharedConfiguration.cs` as `DashboardApiUrl`)
  - Evidence: `Source/Samples/SampleShared/SharedConfiguration.cs` (line 55)
- **Toggle**: `EnableDashboard` in `App.config` (read by `SharedConfiguration.cs` line 33); `DashboardApiUrl` override also available
- **Authentication**: optional; `Auth.Username` and `Auth.PasswordHash` fields in `appsettings.json`
  - Evidence: `Source/Samples/DashBoard.Api/DashBoard.Api/appsettings.json` (lines 5-8)
- **API key**: optional `ApiKey` field
  - Evidence: `Source/Samples/DashBoard.Api/DashBoard.Api/appsettings.json` (line 3)
- **Swagger**: enabled via `EnableSwagger: true`
  - Evidence: `Source/Samples/DashBoard.Api/DashBoard.Api/appsettings.json` (line 2)
- **Consumer-side client**: `DotNetWorkQueue.Dashboard.Client` 0.9.10 (net8.0 only) — pushed from each consumer to the dashboard via `DashboardConsumerClient`
  - Evidence: `Source/Samples/SampleShared/Injectors.cs` (lines 178-217); `Source/Samples/Redis/RedisProducer/RedisProducer.csproj` (lines 84-86)

### Message Interceptors (in-process)

These are not external services but represent data transformation applied before messages are stored in any transport:

- **GZip compression** (`DotNetWorkQueue.Interceptors.GZipMessageInterceptor`) — toggleable via `EnableCompression`
- **TripleDES encryption** (`DotNetWorkQueue.Interceptors.TripleDesMessageInterceptor`) — toggleable via `EnableEncryption`
  - Key and IV are hardcoded sample values (`"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"` / `"aaaaaaaaaaa="`) in both `Injectors.cs` and `appsettings.json`
  - Evidence: `Source/Samples/SampleShared/Injectors.cs` (lines 72-73)
  - Evidence: `Source/Samples/DashBoard.Api/DashBoard.Api/appsettings.json` (lines 12-15)

### Chaos Engineering

- **Polly.Contrib.Simmy** 0.3.0 — fault injection at the transport level
- **Toggle**: `EnableChaos` in `App.config` (defaults to `false` in all observed configs)
  - Evidence: `Source/Samples/Redis/RedisProducer/App.config` (line 13)
- **Scope**: present only in SQLServer, PostgreSQL, and SQLite transport projects; absent from Redis and LiteDb
  - Evidence: `Source/Samples/SQLServer/SQLServerProducer/SQLServerProducer.csproj` (line 40); `Source/Samples/Redis/RedisProducer/RedisProducer.csproj` — `Polly.Contrib.Simmy` not present

## Summary Table

| Integration | Type | Config Location | Default Endpoint | Confidence |
|-------------|------|-----------------|------------------|------------|
| Redis | Queue transport | `App.config` `Database` key | `192.168.0.2:6379` (default Redis port) [Inferred] | Observed |
| SQL Server | Queue transport | `App.config` `Database` key | `192.168.0.2` | Observed |
| PostgreSQL | Queue transport | `App.config` `Database` key | `192.168.0.2:5432` | Observed |
| SQLite | Queue transport | `App.config` `Database` key | local file `\test.db3` | Observed |
| LiteDB | Queue transport | `App.config` `Database` key | local file `\test.db` | Observed |
| Jaeger (tracing) | OTLP HTTP | `tracesettings.json` per project | `http://192.168.0.2:4319` | Observed |
| Prometheus (metrics) | OTLP HTTP push | `SampleShared/metricsettings.json` | `http://192.168.0.2:9090/api/v1/otlp/v1/metrics` | Observed |
| Grafana | Dashboard (external) | `grafana-dashboard.json` | Not configured in code | Observed |
| Dashboard.Api | Internal ASP.NET Core service | `appsettings.json` | `https://localhost:32906` | Observed |
| GZip interceptor | In-process | `App.config` `EnableCompression` | enabled by default | Observed |
| TripleDES interceptor | In-process | `App.config` `EnableEncryption` | enabled by default | Observed |
| Simmy chaos | In-process | `App.config` `EnableChaos` | disabled by default | Observed |

## Open Questions

- All remote endpoints (`192.168.0.2`) appear to be a single developer workstation or local VM. There is no staging or production endpoint configuration — is there a separate config layer for non-developer deployments?
- The SQLite and LiteDB `appsettings.json` dashboard connections use absolute paths (`C:\Users\brian\Documents\...`). These are not portable across machines.
- The TripleDES key/IV is hardcoded as all-`a` characters in both `Injectors.cs` and `appsettings.json` — appropriate for samples but should be prominently flagged in documentation as not suitable for production patterns.
- The `tracesettings.json` file contains a trailing comma after the last JSON property (after `JAEGER_AGENT_PORT`) making it technically invalid JSON. This appears to be tolerated by `Microsoft.Extensions.Configuration` but may fail with strict JSON parsers.
  - Evidence: `Source/Samples/Redis/RedisProducer/tracesettings.json` (line 6)
- Redis and LiteDb samples lack `Polly.Contrib.Simmy` — is chaos engineering planned for those transports or intentionally omitted?
- Is a Jaeger instance required, or can the OTLP endpoint be pointed at another backend (e.g., Tempo, Zipkin with OTLP adapter)? The config key name `JAEGER_*` implies Jaeger but the exporter is generic OTLP.
