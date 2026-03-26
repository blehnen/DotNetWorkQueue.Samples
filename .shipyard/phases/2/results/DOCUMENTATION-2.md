# Documentation Report
**Phase:** 2 -- Update Transport Projects
**Date:** 2026-03-26

## Summary

- API/Code docs: 0 files (no public API surface changed; all changes are configuration-level)
- Architecture updates: 1 (CLAUDE.md -- version reference and configuration section)
- User-facing docs: 1 file updated (CLAUDE.md)

## API Documentation

No public API changes in this phase. All changes are:
- NuGet version bumps in .csproj files
- Deletion of `queue.Configuration.History.Enabled` calls from consumer Program.cs files
- Addition of `RedisBaseTransportOptions.EnableHistory` in Redis producer options lambdas

These are call-site changes, not interface changes. No docstring or API reference updates required.

## Architecture Updates

### History configuration split by transport role

**Change:** `History.Enabled` is no longer set on the consumer queue configuration object.
For Redis, `EnableHistory` is now set on `RedisBaseTransportOptions` inside the producer's
options lambda. For all other transports (SQL Server, PostgreSQL, SQLite, LiteDB), history
enablement remains on the producer's `createQueue.Options.EnableHistory` property.
Consumers no longer configure history at all.

**Reason:** In DotNetWorkQueue 0.9.11, `IHistoryConfiguration` was removed from the consumer
queue configuration interface. History is a producer-side concern -- it controls whether
processed message records are written. The consumer side no longer exposes this setting.

**Impact on CLAUDE.md:** The configuration section lists `App.config` feature toggles. The
`EnableHistory` key is still present in `App.config` for all projects, so no change to that
list is needed. However, the version reference `v0.9.0` in the Project Overview and Key
Dependencies sections is stale and was updated.

## User Documentation

### CLAUDE.md
- **Type:** Project instructions / developer reference
- **Status:** Updated

**Changes made:**
1. Project Overview: `v0.9.0` corrected to `v0.9.11`
2. Key Dependencies: `DotNetWorkQueue v0.9.0` corrected to `v0.9.11`

The Configuration section already accurately lists `EnableHistory` as one of the App.config
feature toggles and does not need to be changed -- `EnableHistory` is still a valid key in
all App.config files. No description of where the value is applied (consumer vs. producer)
belongs in CLAUDE.md at that level of detail.

## Gaps

**Dashboard.Api intentionally excluded:** `DashBoard.Api/DashBoard.Api.csproj` still
references DotNetWorkQueue packages at 0.9.10. CONTEXT-2.md records this as a deliberate
non-goal for Phase 2. If Phase 3 upgrades Dashboard.Api, CLAUDE.md will not need further
changes -- the project structure description already covers it accurately.

## Recommendations

None. CLAUDE.md is now accurate for the 0.9.11 state of the codebase. The configuration
section is correct as-is: `EnableHistory` remains a valid App.config key for all transports,
and the internal plumbing difference (producer options lambda vs. queue options property) is
implementation detail that does not belong in CLAUDE.md.
