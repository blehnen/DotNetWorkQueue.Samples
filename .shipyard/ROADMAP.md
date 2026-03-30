# Roadmap: Integration Tests for DotNetWorkQueue Samples

## Overview

Add an MSTest integration test project that verifies produce-consume round-trips across all 5 transports (SQLite, LiteDb, Redis, SQL Server, PostgreSQL). SQLite and LiteDb tests run in CI (GitHub Actions); server-based transports (Redis, SQL Server, PostgreSQL) run locally only. Also update CHANGELOG.md for the 0.9.13 version bump and new integration tests.

The 0.9.13 NuGet version bump is already applied to all csproj files. Phases 1-2 (0.9.11 upgrade) are complete.

## Prior Work (Completed)

| Phase | Description | Status |
|-------|-------------|--------|
| 1 | Fix SampleShared for 0.9.11 breaking change | Done |
| 2 | Update all transport projects to 0.9.11 | Done |

## Scope

- **In scope:** New MSTest project (`Source/Samples/IntegrationTests/`), test classes for 5 transports, shared test helper, CI workflow update, CHANGELOG update
- **Out of scope:** Feature-matrix testing (compression/encryption/history combos), error/poison/scheduler scenarios, changes to existing sample projects, performance testing

## Risk Assessment

1. **Highest risk -- test lifecycle correctness (Phase 3).** The produce-consume-assert pattern is the core of the project. If the shared test helper gets the queue creation, message send, consumer start, notification wait, or cleanup wrong, every transport test will fail. This must be built and verified first against at least one transport (SQLite, which requires no external server). Mitigated by building the helper and SQLite test together as a vertical slice.

2. **Medium risk -- transport-specific quirks (Phase 4).** Each transport has different connection string formats, queue init types, queue creation option types, and cleanup requirements. LiteDb uses `LiteDbMessageQueueInit` + `Filename=...;Connection=shared;` connection strings. Redis uses a bare host:port string. SQL Server and PostgreSQL use full ADO.NET connection strings. File-based transports (SQLite, LiteDb) need temp file cleanup. The App.config XML parsing must handle each format correctly. Mitigated by reading the actual App.config files from existing samples (proven config) rather than duplicating connection info.

3. **Low risk -- CI/CHANGELOG (Phase 5).** Mechanical additions to existing files. The CI workflow already builds SampleShared first and runs on `windows-latest`. Adding restore/build/test steps for the integration test project follows the established pattern.

## Connection String Patterns (Reference)

| Transport | App.config `Database` value | Connection string construction |
|-----------|---------------------------|-------------------------------|
| SQLite | `\test.db3` | `Data Source={userprofile}\Documents{Database};Version=3;` |
| LiteDb | `\test.db` | `Filename={userprofile}\Documents{Database};Connection=shared;` |
| Redis | `192.168.0.2,defaultDatabase=1,syncTimeout=15000` | Used directly as connection string |
| SQL Server | Full ADO.NET connection string | Used directly as connection string |
| PostgreSQL | Full ADO.NET connection string | Used directly as connection string |

For CI tests (SQLite, LiteDb), the connection string must be overridden to use a temp directory instead of `%userprofile%\Documents`.

## Queue API Patterns (Reference)

Each transport follows the same DotNetWorkQueue API surface:

- **Init type:** `SqLiteMessageQueueInit`, `LiteDbMessageQueueInit`, `RedisQueueInit`, `SqlServerMessageQueueInit`, `PostgreSqlMessageQueueInit`
- **Creation type:** `SqLiteMessageQueueCreation`, `LiteDbMessageQueueCreation`, `RedisQueueCreation`, `SqlServerMessageQueueCreation`, `PostgreSqlMessageQueueCreation`
- **Queue creation:** `QueueCreationContainer<TInit>` -> `GetQueueCreation<TCreation>(queueConnection)` -> set `Options.*` -> `CreateQueue()`
- **Produce:** `QueueContainer<TInit>` -> `CreateProducer<SimpleMessage>(queueConnection)` -> `queue.Send(message, additionalData)`
- **Consume:** `QueueContainer<TInit>` -> `CreateConsumer(queueConnection)` -> `queue.Start<SimpleMessage>(handler, notifications)`
- **Notifications:** `ConsumerQueueNotifications` with `OnMessageCompleted`, `OnPoisonMessage`, `OnError` callbacks

---

## Phase 3: Test Project Infrastructure + SQLite Vertical Slice

**Description:** Create the MSTest integration test project with solution file, csproj (dual-target net8.0/net48, all 5 transport NuGet packages, SampleShared HintPath reference), a shared `ProduceConsumeTestHelper` class encapsulating the full lifecycle (parse App.config, create queue with unique name, produce N messages, start consumer, wait for OnMessageCompleted count with timeout, assert no errors/poison, cleanup queue and temp files), and the first transport test class (`SqliteTests.cs` with `[TestCategory("CI")]`). This is a vertical slice: when Phase 3 is done, `dotnet test --filter "TestCategory=CI&FullyQualifiedName~Sqlite"` passes end-to-end.

**Requirements covered:** R1 (project structure), R3 (test flow), R4 (App.config parsing), partial R2 (SQLite test class)

**Depends on:** Phase 2 (SampleShared must be built at 0.9.13)

**Files created:**
- `Source/Samples/IntegrationTests/IntegrationTests.sln`
- `Source/Samples/IntegrationTests/IntegrationTests.csproj`
- `Source/Samples/IntegrationTests/ProduceConsumeTestHelper.cs` -- shared lifecycle helper
- `Source/Samples/IntegrationTests/SqliteTests.cs` -- `[TestCategory("CI")]`

**Key design decisions:**
- The test helper is generic over `TInit` (the transport's `ITransportInit` implementation) and `TCreation` (the queue creation type), so each transport test class only supplies its types, connection string, queue options, and cleanup logic
- App.config files are read via `System.Xml.Linq` (XDocument) from relative paths like `../../SQLite/SQLiteProducer/App.config` -- no `ConfigurationManager` (which reads the test project's own config, not arbitrary files)
- Each test generates a unique queue name `test_{Guid}` for isolation
- For file-based transports in CI, the database path is overridden to `Path.GetTempPath()` instead of `%userprofile%\Documents`
- Compression and encryption are enabled (exercises the full serialize -> compress -> encrypt -> decrypt -> decompress -> deserialize pipeline); tracing, metrics, and chaos are disabled
- Message count is 5 with 0ms processing time for fast tests
- Consumer wait timeout is 30 seconds via `ManualResetEventSlim` signaled from `OnMessageCompleted` callback
- `[TestCleanup]` deletes the queue via the transport's removal method and deletes temp database files

**Success criteria:**
- `IntegrationTests.csproj` restores and builds for both net8.0 and net48
- `dotnet test --filter "TestCategory=CI&FullyQualifiedName~Sqlite"` passes with 5 messages produced and consumed
- No leftover SQLite database files in temp directory after test run

**Verification commands:**
```bash
# Build SampleShared first (prerequisite)
dotnet restore "Source/Samples/SampleShared/SampleShared.sln"
dotnet build "Source/Samples/SampleShared/SampleShared.sln" -c Debug

# Build integration tests
dotnet restore "Source/Samples/IntegrationTests/IntegrationTests.sln"
dotnet build "Source/Samples/IntegrationTests/IntegrationTests.sln" -c Debug

# Run SQLite CI tests only
dotnet test "Source/Samples/IntegrationTests/IntegrationTests.sln" -c Debug --filter "TestCategory=CI&FullyQualifiedName~Sqlite" --no-build -v normal
```

**Estimated scope:** ~50% of total work. Highest complexity -- establishing the shared helper and proving the pattern works end-to-end.

---

## Phase 4: Remaining Transport Test Classes

**Description:** Add the 4 remaining transport test classes, each following the pattern established by the shared `ProduceConsumeTestHelper` in Phase 3. LiteDb gets `[TestCategory("CI")]`; Redis, SQL Server, and PostgreSQL get `[TestCategory("LocalOnly")]`. Each test class supplies its transport-specific types, connection string parsing logic, queue creation options, and cleanup behavior.

**Requirements covered:** R2 (all 5 test classes complete)

**Depends on:** Phase 3 (shared helper and project structure must exist)

**Files created:**
- `Source/Samples/IntegrationTests/LiteDbTests.cs` -- `[TestCategory("CI")]`
- `Source/Samples/IntegrationTests/RedisTests.cs` -- `[TestCategory("LocalOnly")]`
- `Source/Samples/IntegrationTests/SqlServerTests.cs` -- `[TestCategory("LocalOnly")]`
- `Source/Samples/IntegrationTests/PostgreSqlTests.cs` -- `[TestCategory("LocalOnly")]`

**Transport-specific notes:**

| Transport | Init Type | Creation Type | Connection String Source | Cleanup |
|-----------|-----------|---------------|------------------------|---------|
| LiteDb | `LiteDbMessageQueueInit` | `LiteDbMessageQueueCreation` | `../../LiteDb/LiteDbProducer/App.config` -- `Filename={tempDir}{Database};Connection=shared;` | Delete `*.db` temp files |
| Redis | `RedisQueueInit` | `RedisQueueCreation` | `../../Redis/RedisProducer/App.config` -- `Database` value used directly | Queue removal via API only |
| SQL Server | `SqlServerMessageQueueInit` | `SqlServerMessageQueueCreation` | `../../SQLServer/SQLServerProducer/App.config` -- `Database` value used directly | Queue removal via API only |
| PostgreSQL | `PostgreSqlMessageQueueInit` | `PostgreSqlMessageQueueCreation` | `../../PostgreSQL/PostgreSQLProducer/App.config` -- `Database` value used directly | Queue removal via API only |

**Success criteria:**
- All 5 test classes exist and compile
- `dotnet test --filter TestCategory=CI` passes (runs SQLite + LiteDb tests)
- `dotnet test --filter TestCategory=LocalOnly` discovers Redis, SQL Server, PostgreSQL tests (they may fail if servers are unavailable, but must compile and be discoverable)

**Verification commands:**
```bash
# Build
dotnet build "Source/Samples/IntegrationTests/IntegrationTests.sln" -c Debug

# Run CI tests (SQLite + LiteDb)
dotnet test "Source/Samples/IntegrationTests/IntegrationTests.sln" -c Debug --filter "TestCategory=CI" --no-build -v normal

# List all discovered tests (verify all 5 transports are present)
dotnet test "Source/Samples/IntegrationTests/IntegrationTests.sln" -c Debug --list-tests --no-build
```

**Estimated scope:** ~30% of total work. Mechanical application of the pattern from Phase 3, but each transport has unique connection string handling and cleanup needs.

---

## Phase 5: CI Workflow Update + CHANGELOG

**Description:** Update `.github/workflows/ci.yml` to restore, build, and run the integration test project with `--filter TestCategory=CI` (SQLite + LiteDb only). Update `CHANGELOG.md` with entries for the 0.9.13 version bump and the new integration test project.

**Requirements covered:** R5 (CI workflow), R6 (CHANGELOG)

**Depends on:** Phase 4 (all test classes must exist and CI tests must pass)

**Files modified:**
- `.github/workflows/ci.yml` -- add 3 steps after the existing DashBoard.Api build: Restore IntegrationTests, Build IntegrationTests, Run IntegrationTests with `--filter TestCategory=CI`
- `CHANGELOG.md` -- add dated entry for 0.9.13 bump and integration tests

**CI workflow additions (inserted after DashBoard.Api build steps):**
```yaml
- name: Restore IntegrationTests
  run: dotnet restore "Source\Samples\IntegrationTests\IntegrationTests.sln"

- name: Build IntegrationTests
  run: dotnet build "Source\Samples\IntegrationTests\IntegrationTests.sln" -c Debug --no-restore

- name: Run Integration Tests (CI category)
  run: dotnet test "Source\Samples\IntegrationTests\IntegrationTests.sln" -c Debug --no-build --filter "TestCategory=CI" -v normal
```

**CHANGELOG additions:**
```
### {date}
- Update all DotNetWorkQueue.* packages to 0.9.13
- Add MSTest integration test project verifying produce-consume round-trips for all 5 transports
- SQLite and LiteDb tests run in CI; Redis, SQL Server, PostgreSQL tests are local-only
```

**Success criteria:**
- CI workflow YAML is valid and contains IntegrationTests restore/build/test steps
- Test step uses `--filter "TestCategory=CI"` to run only SQLite and LiteDb tests
- CHANGELOG.md has a dated entry for 0.9.13 with integration test mention
- Full local simulation of CI passes: build SampleShared, build all transports, build IntegrationTests, run CI tests

**Verification commands:**
```bash
# Validate YAML syntax (requires python3)
python3 -c "import yaml; yaml.safe_load(open('.github/workflows/ci.yml'))"

# Verify CHANGELOG has 0.9.13 entry
grep -q "0.9.13" CHANGELOG.md && echo "PASS: CHANGELOG updated" || echo "FAIL"

# Verify CI workflow has IntegrationTests steps
grep -q "IntegrationTests" .github/workflows/ci.yml && echo "PASS: CI updated" || echo "FAIL"

# Full CI simulation (local)
dotnet restore "Source/Samples/SampleShared/SampleShared.sln"
dotnet build "Source/Samples/SampleShared/SampleShared.sln" -c Debug
dotnet restore "Source/Samples/IntegrationTests/IntegrationTests.sln"
dotnet build "Source/Samples/IntegrationTests/IntegrationTests.sln" -c Debug
dotnet test "Source/Samples/IntegrationTests/IntegrationTests.sln" -c Debug --no-build --filter "TestCategory=CI" -v normal
```

**Estimated scope:** ~20% of total work. Mechanical file edits with well-defined patterns.

---

## Phase Dependency Graph

```
Phase 1 (done) ──> Phase 2 (done) ──> Phase 3: Test Infrastructure + SQLite
                                            │
                                            v
                                       Phase 4: Remaining Transport Tests
                                            │
                                            v
                                       Phase 5: CI + CHANGELOG
```

All phases are sequential. Phase 3 must complete before Phase 4 because the shared helper must be proven to work before writing 4 more test classes against it. Phase 5 must come last because it adds CI steps that depend on all test classes existing and passing.

## Final Verification (Post-Phase 5)

After all phases are complete, the following must all pass:

```bash
# 1. Full build chain
dotnet build "Source/Samples/SampleShared/SampleShared.sln" -c Debug
dotnet build "Source/Samples/IntegrationTests/IntegrationTests.sln" -c Debug

# 2. CI-category tests pass (SQLite + LiteDb, no external dependencies)
dotnet test "Source/Samples/IntegrationTests/IntegrationTests.sln" -c Debug --filter "TestCategory=CI" --no-build -v normal

# 3. All 5 transport test classes are discoverable
dotnet test "Source/Samples/IntegrationTests/IntegrationTests.sln" -c Debug --list-tests --no-build | grep -c "ProduceConsume"
# Expected: 5 (one per transport)

# 4. CI workflow references IntegrationTests
grep "IntegrationTests" .github/workflows/ci.yml

# 5. CHANGELOG documents 0.9.13
grep "0.9.13" CHANGELOG.md

# 6. No changes to existing sample projects
git diff --name-only Source/Samples/Redis Source/Samples/SQLServer Source/Samples/PostgreSQL Source/Samples/SQLite Source/Samples/LiteDb Source/Samples/SampleShared Source/Samples/DashBoard.Api | wc -l
# Expected: 0 (no modifications to existing projects)
```

## Notes

- **SampleShared is NOT modified.** The test project references the existing compiled SampleShared.dll via HintPath, just like all other projects. The `SimpleMessage`, `MessageProcessing.HandleMessages`, `CreateNotifications.Create`, `Injectors`, and `SharedConfiguration` classes are used as-is.
- **SharedConfiguration static constructor reads ConfigurationManager.AppSettings** from the running process's config. In the test project, this will read the test project's own App.config (or defaults). The test helper must NOT rely on SharedConfiguration for connection strings -- it must parse the transport sample's App.config files directly via XDocument.
- **The test helper should disable tracing, metrics, and chaos** to minimize external dependencies. **Compression and encryption should be enabled** -- these interceptors (`GZipMessageInterceptor`, `TripleDesMessageInterceptor`) are part of the DI wiring and must be exercised to catch IoC registration breaks. The round-trip then proves: serialize -> compress -> encrypt -> send -> receive -> decrypt -> decompress -> deserialize.
- **LiteDb's `EnableHeartBeat` option does not exist** (unlike SQLite). Each transport's queue creation options are slightly different -- the test classes must set only options that exist for their transport.
- **The `ConsumerQueueNotifications` callback is the primary synchronization mechanism.** The `OnMessageCompleted` callback fires for each successfully processed message. The test helper counts completions and signals a `ManualResetEventSlim` when the expected count is reached.
