# TESTING.md

## Overview

This repository contains no automated tests of any kind. It is explicitly a samples-only project whose purpose is to demonstrate DotNetWorkQueue library usage across multiple transport backends. Quality assurance relies entirely on manual runtime validation, CI build verification, and the correctness guarantees of the upstream DotNetWorkQueue library itself.

---

## Findings

### Test Infrastructure

- **No test projects present**: No `.Tests`, `.Specs`, or `.IntegrationTests` project folders exist anywhere in the repository. No xUnit, NUnit, MSTest, or Shouldly package references appear in any `.csproj`.
  - Evidence: Glob search for `*.csproj` returned 38 projects — all are either executable console apps or the Dashboard API web app. No test runner projects exist.
- **No test runner configuration**: No `xunit.runner.json`, `nunit.runner.json`, `.runsettings`, or `coverlet.json` files exist.
  - Evidence: No such files found during repository scan.
- **No code coverage configuration**: No `coverlet`, `dotCover`, or OpenCover references in any project file.
  - Evidence: All `.csproj` files examined (`RedisConsumer.csproj`, `SQLServerConsumer.csproj`, `PostGreSQLConsumer.csproj`, `LiteDbConsumer.csproj`) contain no coverage tooling references.
- **Intentional design**: The absence of tests is by design and documented in `CLAUDE.md`: "There are no tests in this repository — it is a samples-only project."
  - Evidence: `CLAUDE.md` (line: "There are no tests in this repository — it is a samples-only project.")

---

### CI Configuration

- **No CI configuration file found**: An `appveyor.yml` is referenced in `CLAUDE.md` but the file is not present in the repository at the time of this analysis. No `.github/workflows/`, `azure-pipelines.yml`, or other CI manifests were found.
  - Evidence: Glob search for `appveyor*` across the entire repository returned no results. `CLAUDE.md` states "AppVeyor is used for CI (`appveyor.yml`). It restores and builds all 7 solutions."
  - [Inferred] The CI configuration may have been removed or may exist on a remote branch not currently checked out.
- **CI scope (per CLAUDE.md)**: When present, CI restores and builds all seven solutions (`LiteDb`, `PostgreSQL`, `Redis`, `SQLite`, `SQLServer`, `DashBoard.Api`, and `SampleShared`) in Debug configuration using Visual Studio 2022. No test execution step is defined because there are no tests.

---

### Quality Assurance Approach

- **Manual runtime validation**: The samples are designed to be run interactively against live infrastructure (Redis, SQL Server, PostgreSQL, SQLite files, LiteDB files). Correctness is verified by observing console output, checking queue state, and exercising the interactive menu options.
  - Evidence: `Source/Samples/SampleShared/RunProducer.cs` (lines 139–350) — the `RunLoop` method presents an interactive menu covering 26 scenarios (send 10/500/1000 jobs, error cases, retry cases, expiry, batch, delayed processing) that serve as manual test cases.
- **Error scenario coverage in shared code**: `SampleShared` encodes three message error scenarios that must be manually triggered: fatal error (divide-by-zero), retryable error that eventually succeeds, and retryable error that always fails. These validate the queue library's retry and dead-letter behavior.
  - Evidence: `Source/Samples/SampleShared/MessageProcessing.cs` (lines 22–68); `Source/Samples/SampleShared/Messages.cs` (lines 32–52: `CreateSimpleMessageError`, `CreateSimpleMessageRetryError`).
- **Chaos engineering support**: All projects expose an `EnableChaos` App.config flag that activates Polly chaos policies via `Injectors.SetOptions()`. This provides a lightweight fault-injection mechanism for manual resilience testing.
  - Evidence: `Source/Samples/SampleShared/Injectors.cs` (lines 59–66: `pol.EnableChaos = enableChaos`); `Source/Samples/Redis/RedisConsumer/App.config` (line 13: `<add key="EnableChaos" value="false" />`).
- **Observable telemetry as validation signal**: OpenTelemetry tracing (Jaeger) and metrics (OTLP/Prometheus) are wired as optional feature flags. When enabled, trace spans and metric counters provide indirect confirmation that the queue pipeline is functioning correctly end-to-end.
  - Evidence: `Source/Samples/SampleShared/Injectors.cs` (lines 119–155: metrics setup; lines 220–256: trace setup).
- **Dashboard as runtime monitor**: The `DashBoard.Api` project provides a live web UI that shows queue depths, consumer status, message history, and error counts across all transports simultaneously. This serves as a runtime health check during manual validation sessions.
  - Evidence: `Source/Samples/DashBoard.Api/DashBoard.Api/Program.cs` (lines 31–45: transport connections registered; line 87: `app.UseDotNetWorkQueueDashboard()`).

---

### What Is Not Tested

The following quality dimensions have no automated verification coverage:

| Gap | Description |
|-----|-------------|
| Unit tests | No unit tests for `SampleShared` logic (`MessageProcessing`, `RunProducer`, `Messages`, `SharedConfiguration`) |
| Integration tests | No automated tests that spin up a transport backend and send/receive messages |
| Cross-transport parity | No automated check that all 5 transports produce identical behavior for the same scenario |
| Configuration validation | No test verifying that malformed `App.config` values produce meaningful errors |
| Regression detection | No automated guard against behavioral regressions when DotNetWorkQueue packages are updated |
| Build matrix | No automated verification that all projects build successfully against both `net8.0` and `net48` targets (per-framework) — only the combined solution build is run in CI |

---

## Summary Table

| Item | Detail | Confidence |
|------|--------|------------|
| Test framework | None | Observed |
| Test projects | None (0 of 38 projects are test projects) | Observed |
| Code coverage tooling | None configured | Observed |
| CI pipeline file | Not present in working tree (referenced in CLAUDE.md) | Observed |
| CI test step | Not applicable — no tests exist | Observed |
| Manual test coverage | Interactive console menu with 26 scenarios in `RunProducer.RunLoop` | Observed |
| Error scenario coverage | 3 message error types exercisable manually | Observed |
| Fault injection | Polly chaos via `EnableChaos` flag | Observed |
| Observability as QA signal | OpenTelemetry traces + OTLP metrics + Dashboard UI | Observed |
| Intentional test absence | Confirmed by CLAUDE.md | Observed |

---

## Open Questions

- Is there a separate integration test suite in the main DotNetWorkQueue library repository that covers the transport contracts these samples demonstrate? If so, the samples may reasonably rely on that upstream coverage.
- Should a smoke-test script (e.g., a shell script that launches producer → verifies queue count → launches consumer → verifies queue drains) be added to give CI a minimal runtime validation step?
- The `appveyor.yml` referenced in `CLAUDE.md` is absent from the working tree. Was it deleted, is CI currently non-functional, or does it live on a different branch?
