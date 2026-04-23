# DotNetWorkQueue.Samples

[![Build status](https://github.com/blehnen/DotNetWorkQueue.Samples/actions/workflows/ci.yml/badge.svg)](https://github.com/blehnen/DotNetWorkQueue.Samples/actions/workflows/ci.yml)

Sample applications for [DotNetWorkQueue](https://github.com/blehnen/DotNetWorkQueue). Build `SampleShared.sln` first — all other projects depend on it.

## Samples

| Sample | Description |
|--------|-------------|
| Producer | Queue messages |
| ProducerLinq | Queue LINQ expressions |
| Consumer | Dedicated processing threads |
| ConsumerAsync | Dedicated reader + separate processing thread pool |
| ConsumerLinq | Process LINQ expressions |
| Scheduler | Recurring jobs |
| SchedulerConsumer | Scheduler + consumer |
| Dashboard.Api | Queue monitoring API (ASP.NET Core) |

**Transports:** Redis, SQL Server, SQLite, PostgreSQL, LiteDB

## Configuration

| Project | Config file | Details |
|---------|-------------|---------|
| Samples | `App.config` | Connection string, queue name, GZIP, encryption, tracing, and metrics toggles |
| Dashboard.Api | `appsettings.json` | Connection strings and queue names (see `appsettings.example.json`). Swagger at `/swagger` |

## Integration tests

Produce-consume round-trip tests for each transport. Catches DI wiring breaks without manual testing.

```bash
# Build SampleShared first
dotnet build "Source/Samples/SampleShared/SampleShared.sln" -c Debug

# SQLite + LiteDb only (no external services needed)
dotnet test "Source/Samples/IntegrationTests/IntegrationTests.sln" -c Debug --filter "TestCategory=CI"

# All transports (needs Redis, SQL Server, PostgreSQL)
dotnet test "Source/Samples/IntegrationTests/IntegrationTests.sln" -c Debug
```

SQLite and LiteDb tests run in GitHub Actions. Redis, SQL Server, and PostgreSQL tests are local-only -- they read connection strings from each transport's sample `App.config`.

## Observability

| Feature | Config file | Backend |
|---------|-------------|---------|
| Tracing | `tracesettings.json` | [Jaeger](https://www.jaegertracing.io/download/) |
| Metrics | `metricsettings.json` | [InfluxDB](https://www.influxdata.com/products/influxdb/) |

Both can be enabled/disabled in `App.config`. Point the JSON config files at your instances.

License
--------
Copyright (c) 2017-2026 blehnen

All rights reserved.

MIT License

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
