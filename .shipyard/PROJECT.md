# Integration Tests & Version Bump for DotNetWorkQueue Samples

## Description

Add automated integration tests to the DotNetWorkQueue samples project to catch IoC/DI wiring breaks without manual sample testing. The tests verify produce-consume round-trips across all 5 transports (SQLite, LiteDb, Redis, SQL Server, PostgreSQL). SQLite and LiteDb tests run in CI (GitHub Actions); server-based transports run locally only. Also update CHANGELOG.md for the 0.9.13 version bump already applied to all csproj files.

## Goals

1. Create an MSTest integration test project that verifies produce-consume round-trips for all 5 transports
2. Use `[TestCategory]` attributes to separate CI-safe tests (SQLite, LiteDb) from local-only tests (Redis, SqlServer, PostgreSQL)
3. Read connection strings from existing sample App.config files -- no duplicate configuration
4. Update GitHub Actions CI workflow to build and run the CI-category tests
5. Update CHANGELOG.md to document the 0.9.13 version bump and new integration tests

## Non-Goals

- Feature-matrix testing (varying compression/encryption/history toggle combinations) -- deferred to future scope. Compression and encryption are enabled by default in tests to exercise the interceptor DI wiring.
- Full scenario coverage (error handling, poison messages, expiration, schedulers) -- deferred to future scope
- Changing existing sample project structure or behavior
- Performance/load testing

## Requirements

### R1: Integration Test Project
- Single MSTest project at `Source/Samples/IntegrationTests/IntegrationTests.csproj`
- Dual-target net8.0 and net48 (consistent with all other projects)
- References all 5 transport NuGet packages (DotNetWorkQueue.Transport.SQLite, .LiteDb, .Redis, .SqlServer, .PostgreSQL) at 0.9.13
- References SampleShared via HintPath (same pattern as other projects)
- Own solution file: `Source/Samples/IntegrationTests/IntegrationTests.sln`

### R2: Test Classes (one per transport)
- `SqliteTests.cs` -- `[TestCategory("CI")]`
- `LiteDbTests.cs` -- `[TestCategory("CI")]`
- `RedisTests.cs` -- `[TestCategory("LocalOnly")]`
- `SqlServerTests.cs` -- `[TestCategory("LocalOnly")]`
- `PostgreSqlTests.cs` -- `[TestCategory("LocalOnly")]`
- Shared base class or helper for the common produce-consume-assert lifecycle

### R3: Test Flow (per transport)
- **Setup** (`[TestInitialize]`): Parse the transport's sample App.config for connection string, generate unique queue name (`test_{guid}`), create queue via `QueueCreationContainer`
- **Test** (`[TestMethod]`): Produce ~5 simple messages (small payload, minimal processing time), start consumer, wait for `OnMessageCompleted` count to reach N with 30-second timeout, assert all completed with no errors/poison messages
- **Cleanup** (`[TestCleanup]`): Stop consumer, delete queue via transport removal utilities, delete temp database files for file-based transports

### R4: App.config Parsing
- Read connection strings from existing sample App.config files via relative paths (e.g., `../../SQLite/SQLiteProducer/App.config`)
- For SQLite/LiteDb in CI: override database file path to temp directory for clean isolation
- No duplicate configuration maintenance

### R5: CI Workflow Update
- Add steps to `.github/workflows/ci.yml` to restore, build, and run the integration test project
- Run with `dotnet test --filter TestCategory=CI` to execute only SQLite and LiteDb tests
- Must run after SampleShared build (existing dependency)

### R6: CHANGELOG Update
- Add entry for 0.9.13 version bump
- Add entry for new integration test project

## Non-Functional Requirements

- Tests must complete within 60 seconds total (small message count, minimal processing time)
- Per-test queue isolation via unique names -- no interference with running sample apps
- File-based transport tests must clean up all database files (no leftover state)
- Test project must build on both net8.0 and net48

## Success Criteria

1. `dotnet test --filter TestCategory=CI` passes for SQLite and LiteDb with no external dependencies
2. `dotnet test` (no filter) passes for all 5 transports when appropriate services are available
3. CI workflow builds and runs CI-category tests successfully
4. No changes to existing sample projects required
5. CHANGELOG.md reflects 0.9.13 bump and integration tests

## Constraints

- SampleShared must be built before the integration test project (HintPath dependency)
- Server-based transports (Redis, SQL Server, PostgreSQL) are on remote machines -- tests must read connection info from existing App.config files
- MSTest framework (not xUnit or NUnit)
- Git strategy: manual (user controls commits)
