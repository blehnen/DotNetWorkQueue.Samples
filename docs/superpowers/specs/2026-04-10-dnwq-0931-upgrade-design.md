# DotNetWorkQueue 0.9.31 Sample Upgrade — Design

**Date:** 2026-04-10
**Author:** Brian Lehnen (with Claude)
**Status:** Approved for planning

## Problem

The sample projects in this repo pin DotNetWorkQueue (DNWQ) at `0.9.14` / Dashboard packages at `0.9.16`. The core library has since released through `0.9.31` (2026-04-09), which introduces several breaking changes that affect the samples. The samples must be upgraded so they build and run against the current library.

## Goals

- Upgrade every sample (core, transports, dashboard) to DNWQ `0.9.31`.
- Handle the breaking changes from 0.9.17 → 0.9.31 without losing any existing sample behavior.
- Keep the sample set as a faithful, runnable reference for users of the library.
- Leave behavior functionally identical wherever the breaking changes allow.

## Non-goals

- No new samples, no architectural rework, no refactoring unrelated to the upgrade.
- No behavioral changes to scheduler cadence beyond what cron conversion requires.
- No changes to CI infrastructure layout (Jenkinsfile + GitHub Actions); only framework-level updates if needed.

## Breaking changes and their impact

### 0.9.19 — dropped net48 + netstandard2.0, removed dynamic LINQ

| Breaking change | Impact |
|---|---|
| DNWQ now targets only net10.0 + net8.0 | Every sample csproj currently dual-targets `net8.0;net48` (some also `net10.0`). Must drop `net48`. |
| Dynamic LINQ (JpLabs.DynamicCode, net48-only) removed | `SampleShared/RunProducer.cs` contains `#if net48` blocks (`RunDynamic`, `RunDynamicAsync`, and the `case 'b'`/`case 'd'` dispatches in `RunLoop`). These are already conditionally excluded but should be deleted outright now. |
| Sample `*ProducerLinq`/`*ConsumerLinq` projects | **Not affected.** These all use `CreateMethodProducer` (static compiled-expression producer), which is still fully supported. They stay. |

### 0.9.30 — Schyntax → cron (Cronos)

All schedule strings are now standard cron expressions (5-field or 6-field with seconds). The scheduler `Program.cs` files currently pass Schyntax strings like `"sec(0,5,10,15,20,25,30,35,40,45,50,55)"` and `"min(*)"`. These must be converted.

`IJobSchedule.Previous()` now returns `DateTimeOffset?` — no sample consumes this, so no code change required.

### 0.9.31 — multi-source Dashboard config

| Breaking change | Impact |
|---|---|
| `DashboardApi:BaseUrl` / `DashboardApi:ApiKey` → `DashboardApi:Sources[]` array | DashBoard.Api `appsettings.json` must be updated. Old format throws `InvalidOperationException` at startup via `DashboardConfigParser.ValidateNoLegacyConfig`. |
| All UI page URLs now prefixed with `/source/{slug}` | No code change needed — routing is internal to the Ui package. |
| In-process API auto-registers as "Local" | Sample's `Program.cs` must mirror the canonical DNWQ `Dashboard.Ui/Program.cs` self-contained startup pattern. |
| Auth config moved from `Dashboard:Auth` to top-level `DashboardAuth` | Sample `appsettings.json` must move the auth block. |

## Design

### 1. Package versions (all 39 csproj files)

Bump every `PackageReference` to the following:

- `DotNetWorkQueue` → `0.9.31`
- `DotNetWorkQueue.Transport.LiteDb` → `0.9.31`
- `DotNetWorkQueue.Transport.PostgreSQL` → `0.9.31`
- `DotNetWorkQueue.Transport.Redis` → `0.9.31`
- `DotNetWorkQueue.Transport.SQLite` → `0.9.31`
- `DotNetWorkQueue.Transport.SqlServer` → `0.9.31`
- `DotNetWorkQueue.Dashboard.Api` → `0.9.31`
- `DotNetWorkQueue.Dashboard.Ui` → `0.9.31`
- `DotNetWorkQueue.Dashboard.Client` → `0.9.31`

### 2. Target frameworks

Every sample csproj collapses to a single target:

```xml
<TargetFramework>net10.0</TargetFramework>
```

Covered projects:
- `SampleShared/SampleShared.csproj`
- All transport sample executables (37 projects across LiteDb, PostgreSQL, Redis, SQLite, SQLServer)
- `DashBoard.Api/DashBoard.Api.csproj` (already net10.0 — verify)
- `IntegrationTests/IntegrationTests.csproj`

Any conditional `<Reference>` or `<PackageReference>` blocks gated on `'$(TargetFramework)' == 'net48'` are removed.

HintPaths to SampleShared.dll change from `..\..\SampleShared\bin\$(Configuration)\net8.0\SampleShared.dll` (and net48 variants) to `..\..\SampleShared\bin\$(Configuration)\net10.0\SampleShared.dll`.

Any `App.config` files that exist purely for net48 binding redirects can stay (ignored by net10.0) — do not delete during this pass to keep the diff minimal, unless verification shows them causing build issues.

### 3. SampleShared/RunProducer.cs — delete dynamic LINQ dead code

Remove the following regions entirely (no `#if` guards, no placeholder comments):

- Lines 54–65 (`RunDynamic` method and its `#if net48` guards)
- Lines 82–97 (`RunDynamicAsync` method and its `#if net48` guards)
- `case 'b'` dispatch in `RunLoop` (lines 163–166)
- `case 'd'` dispatch in `RunLoop` (lines 171–174)
- The corresponding menu lines in the `Console.WriteLine` prompt: `"b) Send 1 dynamic job (full framework only)"` and `"d) Send 1 dynamic job (full framework only)"`

Renumber the remaining menu options only if doing so does not change the meaning of any user-visible letter. Simpler: leave `a` (static sync) and `c` (static async) where they are; the `b`/`d` slots are simply gone.

Static paths (`RunStatic`, `RunStaticAsync`, `RunLoop` case `'a'` / `'c'`) and the `*ProducerLinq` / `*ConsumerLinq` sample projects are untouched.

### 4. Scheduler samples — Schyntax → cron

Affected files (verified scheduler `Program.cs` files):

- `Source/Samples/LiteDb/LiteDbScheduler/Program.cs`
- `Source/Samples/PostgreSQL/PostGreSQLScheduler/Program.cs`
- `Source/Samples/Redis/RedisScheduler/Program.cs`
- `Source/Samples/SQLServer/SQLServerScheduler/Program.cs`
- `Source/Samples/SQLite/SQLiteScheduler/Program.cs` (if present)

Conversion table (6-field cron with seconds, as Cronos supports):

| Schyntax | Cron equivalent | Meaning |
|---|---|---|
| `sec(0,5,10,15,20,25,30,35,40,45,50,55)` | `*/5 * * * * *` | every 5 seconds |
| `sec(0,30)` | `0,30 * * * * *` | :00 and :30 of each minute |
| `min(*)` | `0 * * * * *` | every minute at :00 |

Each scheduler `Program.cs` will be read to enumerate its exact strings; the three jobs in each scheduler get mapped deterministically to these cron expressions so cadence stays identical.

### 5. DashBoard.Api — mirror DNWQ 0.9.31 Dashboard.Ui reference

#### `Program.cs`

Rewrite to mirror `/mnt/f/Git/DotNetWorkQueue/Source/DotNetWorkQueue.Dashboard.Ui/Program.cs` structure. Keep Serilog setup (sample-specific) but otherwise use the same service/middleware wiring:

- Detect self-contained mode via `dashboardSection.GetSection("Connections").GetChildren().Any()`.
- If self-contained: `AddDotNetWorkQueueDashboard(dashboardSection)` (unchanged).
- Call `DashboardConfigParser.ValidateNoLegacyConfig(builder.Configuration)`.
- Parse sources via `DashboardConfigParser.ParseSources(builder.Configuration)`.
- In self-contained mode, auto-add a `Local` source (matching the reference pattern) and register `LocalSourceHostedService`.
- Register `SourceRegistry`, per-source `HttpClient`, `MultiSourceDashboardApiClient`, `SourceHealthMonitor`.
- Read auth from top-level `DashboardAuth:Username` / `DashboardAuth:PasswordHash` instead of `Dashboard:Auth:*`.
- Keep existing cookie auth handlers (login/logout endpoints).
- Call `UseDotNetWorkQueueDashboard()` in self-contained mode.

#### `appsettings.json` and `appsettings.example.json`

Target shape (preserves existing connections + interceptors verbatim, restructures auth, adds Sources):

```json
{
  "Dashboard": {
    "EnableSwagger": true,
    "ApiKey": "",
    "Interceptors": {
      "GZip": { "Enabled": true, "MinimumSize": 150 },
      "TripleDes": { "Enabled": true, "Key": "...", "IV": "..." }
    },
    "Connections": [ /* unchanged */ ]
  },
  "DashboardApi": {
    "Sources": [
      { "Name": "Local", "BaseUrl": "http://192.168.0.2:9998", "ApiKey": "" }
    ]
  },
  "DashboardAuth": {
    "Username": "",
    "PasswordHash": ""
  }
}
```

Explicit `Local` entry is included (rather than relying on auto-add) so that the sample clearly documents the new config shape for users reading it. BaseUrl uses the deployment URL that was set in the previous session (`http://192.168.0.2:9998`); the example file uses `http://localhost:5000`.

#### Queue name cleanup

Drop `sampleQueueLinq` from the dashboard `Connections[].Queues` lists — these samples now share the regular queue with the static producer/consumer and don't need a separate queue to monitor.

*(Verify during implementation: if `sampleQueueLinq` is still actually used by the Linq samples' App.config, leave it in; the cleanup is purely cosmetic and depends on what the Linq samples currently target.)*

### 6. Documentation

**`CLAUDE.md`:**
- Version reference `v0.9.14` → `v0.9.31`
- Target framework section: remove `net48`, state net10.0 only
- Build commands: remove `net8.0`-specific HintPath notes; update to net10.0
- Keep the "build SampleShared first" guidance (still accurate)

**`CHANGELOG.md`:**
- New entry for 2026-04-10 documenting: version bumps (0.9.14→0.9.31 core, 0.9.16→0.9.31 dashboard), net48 drop, schedule format migration, dashboard multi-source config migration, dynamic LINQ dead code removal

## Verification

Executed in order:

1. `dotnet restore` + `dotnet build -c Debug` for `SampleShared/SampleShared.sln` → 0 errors
2. `dotnet restore` + `dotnet build -c Debug` for each transport solution (`LiteDb`, `PostgreSQL`, `Redis`, `SQLite`, `SQLServer`) → 0 errors each
3. `dotnet restore` + `dotnet build -c Debug` for `DashBoard.Api/DashBoard.Api.sln` → 0 errors
4. `dotnet restore` + `dotnet build -c Debug` for `IntegrationTests/IntegrationTests.sln` → 0 errors
5. `dotnet test IntegrationTests.sln --filter "TestCategory=CI"` → all pass (SQLite + LiteDb round-trips)
6. Spot-check a scheduler sample manually if possible (cron strings are correct)
7. Launch `DashBoard.Api`, confirm startup logs, browse UI at `http://.../source/local/` (manual, if practical)

## Risks and mitigations

| Risk | Mitigation |
|---|---|
| 0.9.31 not yet on NuGet.org | User confirmed it was published 2026-04-09. Restore will fail fast if wrong. |
| HintPath updates missed in one csproj → build error | Systematic pass over every csproj; verification step 2 catches it immediately. |
| Cron conversion changes a job's cadence | Conversion table above keeps cadence identical; reviewer inspects each scheduler file. |
| Dashboard.Api `Program.cs` rewrite breaks auth flow | Rewrite keeps the existing cookie login/logout handlers verbatim; only the source/auth wiring around them changes. |
| `App.config` files for net48 binding redirects cause net10.0 warnings | Leave them in place on first pass; only delete if build output shows warnings. |

## Rollback

All changes are a single commit (or a small series). `git revert` restores the previous working tree. No schema changes, no runtime state.
