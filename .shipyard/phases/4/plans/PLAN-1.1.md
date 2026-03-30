---
phase: remaining-transport-tests
plan: "1.1"
wave: 1
dependencies: ["phase-3"]
must_haves:
  - DotNetWorkQueue.Transport.LiteDb, .Redis, .SqlServer, .PostgreSQL NuGet packages added to csproj
  - LiteDbTests.cs with [TestCategory("CI")]
  - RedisTests.cs with [TestCategory("LocalOnly")]
  - SqlServerTests.cs with [TestCategory("LocalOnly")]
  - PostgreSqlTests.cs with [TestCategory("LocalOnly")]
  - dotnet test --filter TestCategory=CI passes (SQLite + LiteDb)
  - All 5 transport test classes discoverable via --list-tests
files_touched:
  - Source/Samples/IntegrationTests/IntegrationTests.csproj
  - Source/Samples/IntegrationTests/LiteDbTests.cs
  - Source/Samples/IntegrationTests/RedisTests.cs
  - Source/Samples/IntegrationTests/SqlServerTests.cs
  - Source/Samples/IntegrationTests/PostgreSqlTests.cs
tdd: false
---

# Plan 1.1: Add Remaining 4 Transport Test Classes

> **For Claude:** REQUIRED SUB-SKILL: Use shipyard:shipyard-executing-plans to implement this plan task-by-task.

**Goal:** Add LiteDb, Redis, SQL Server, and PostgreSQL test classes following the established SqliteTests pattern.

**Architecture:** Each test class follows the identical pattern: TestInitialize creates a ProduceConsumeTestHelper with transport-specific connection info, TestMethod calls RunTest with transport-specific types and queue options, TestCleanup removes the queue and any temp files.

## Dependencies

- Phase 3 complete (ProduceConsumeTestHelper and SqliteTests exist and pass)

## Tasks

<task id="1" files="Source/Samples/IntegrationTests/IntegrationTests.csproj" tdd="false">
  <action>
    Add the remaining 4 transport NuGet packages to `IntegrationTests.csproj`. Add these
    PackageReferences to the existing unconditional ItemGroup:

    ```xml
    <PackageReference Include="DotNetWorkQueue.Transport.LiteDb" Version="0.9.13" />
    <PackageReference Include="DotNetWorkQueue.Transport.Redis" Version="0.9.13" />
    <PackageReference Include="DotNetWorkQueue.Transport.SqlServer" Version="0.9.13" />
    <PackageReference Include="DotNetWorkQueue.Transport.PostgreSQL" Version="0.9.13" />
    ```

    Also add the Npgsql package needed by the PostgreSQL transport:
    ```xml
    <PackageReference Include="Npgsql" Version="9.0.3" />
    ```

    After adding, run `dotnet restore` and `dotnet build` to verify no package conflicts.
  </action>
  <verify>
    dotnet restore "Source/Samples/IntegrationTests/IntegrationTests.sln" && dotnet build "Source/Samples/IntegrationTests/IntegrationTests.sln" -c Debug -f net10.0 2>&1 | tail -3
  </verify>
  <done>
    1. All 4 new transport packages restore successfully.
    2. Build succeeds with 0 errors.
    3. Existing SQLite test still passes.
  </done>
</task>

<task id="2" files="Source/Samples/IntegrationTests/LiteDbTests.cs, Source/Samples/IntegrationTests/RedisTests.cs, Source/Samples/IntegrationTests/SqlServerTests.cs, Source/Samples/IntegrationTests/PostgreSqlTests.cs" tdd="false">
  <action>
    Create all 4 test classes. Each follows the SqliteTests pattern exactly. Here are the
    transport-specific details:

    ---

    **LiteDbTests.cs** -- `[TestCategory("CI")]`

    ```csharp
    using System;
    using System.IO;
    using System.Xml.Linq;
    using DotNetWorkQueue.Transport.LiteDb.Basic;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    namespace IntegrationTests
    {
        [TestClass]
        public class LiteDbTests
        {
            private ProduceConsumeTestHelper _helper;
            private string _dbFilePath;

            [TestInitialize]
            public void Setup()
            {
                _dbFilePath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.db");
                var queueName = $"test_{Guid.NewGuid():N}";
                var connectionString = $"Filename={_dbFilePath};Connection=shared;";
                _helper = new ProduceConsumeTestHelper(queueName, connectionString, messageCount: 5);
            }

            [TestMethod]
            [TestCategory("CI")]
            public void ProduceConsume()
            {
                _helper.RunTest<LiteDbMessageQueueInit, LiteDbMessageQueueCreation>(createQueue =>
                {
                    createQueue.Options.EnableDelayedProcessing = true;
                    createQueue.Options.EnableMessageExpiration = true;
                    createQueue.Options.EnableStatusTable = true;
                    createQueue.Options.EnableHistory = false;
                    // NOTE: LiteDb does NOT have EnableHeartBeat or EnableStatus
                });
            }

            [TestCleanup]
            public void Cleanup()
            {
                _helper?.RemoveQueue<LiteDbMessageQueueInit, LiteDbMessageQueueCreation>();
                foreach (var suffix in new[] { "", "-journal", "-log" })
                {
                    var path = _dbFilePath + suffix;
                    try
                    {
                        if (File.Exists(path))
                            File.Delete(path);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Cleanup] Warning: could not delete {path}: {ex.Message}");
                    }
                }
            }
        }
    }
    ```

    ---

    **RedisTests.cs** -- `[TestCategory("LocalOnly")]`

    Connection string is read directly from the Redis sample App.config's `Database` key.

    ```csharp
    using System;
    using System.IO;
    using System.Xml.Linq;
    using DotNetWorkQueue.Transport.Redis.Basic;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    namespace IntegrationTests
    {
        [TestClass]
        public class RedisTests
        {
            private ProduceConsumeTestHelper _helper;

            [TestInitialize]
            public void Setup()
            {
                var connectionString = ReadAppConfigValue(
                    Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
                        "..", "..", "..", "..", "Redis", "RedisProducer", "App.config")),
                    "Database");
                var queueName = $"test_{Guid.NewGuid():N}";
                _helper = new ProduceConsumeTestHelper(queueName, connectionString, messageCount: 5);
            }

            [TestMethod]
            [TestCategory("LocalOnly")]
            public void ProduceConsume()
            {
                _helper.RunTest<RedisQueueInit, RedisQueueCreation>(createQueue =>
                {
                    createQueue.Options.EnableHistory = false;
                    // Redis has minimal queue creation options
                });
            }

            [TestCleanup]
            public void Cleanup()
            {
                _helper?.RemoveQueue<RedisQueueInit, RedisQueueCreation>();
            }

            private static string ReadAppConfigValue(string appConfigPath, string key)
            {
                var doc = XDocument.Load(appConfigPath);
                var element = doc.Root?.Element("appSettings")?
                    .Elements("add")
                    .FirstOrDefault(e => e.Attribute("key")?.Value == key);
                return element?.Attribute("value")?.Value
                    ?? throw new InvalidOperationException(
                        $"Key '{key}' not found in {appConfigPath}");
            }
        }
    }
    ```

    NOTE: The `ReadAppConfigValue` helper is duplicated in each server-based test class.
    This is intentional -- it's 8 lines, used once per class, and keeps each test class
    self-contained. Do NOT extract it to a shared helper.

    ---

    **SqlServerTests.cs** -- `[TestCategory("LocalOnly")]`

    Same pattern as Redis but with SQL Server types and full queue options.

    ```csharp
    using System;
    using System.IO;
    using System.Linq;
    using System.Xml.Linq;
    using DotNetWorkQueue.Transport.SqlServer.Basic;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    namespace IntegrationTests
    {
        [TestClass]
        public class SqlServerTests
        {
            private ProduceConsumeTestHelper _helper;

            [TestInitialize]
            public void Setup()
            {
                var connectionString = ReadAppConfigValue(
                    Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
                        "..", "..", "..", "..", "SQLServer", "SQLServerProducer", "App.config")),
                    "Database");
                var queueName = $"test_{Guid.NewGuid():N}";
                _helper = new ProduceConsumeTestHelper(queueName, connectionString, messageCount: 5);
            }

            [TestMethod]
            [TestCategory("LocalOnly")]
            public void ProduceConsume()
            {
                _helper.RunTest<SqlServerMessageQueueInit, SqlServerMessageQueueCreation>(createQueue =>
                {
                    createQueue.Options.EnableDelayedProcessing = true;
                    createQueue.Options.EnableHeartBeat = true;
                    createQueue.Options.EnableMessageExpiration = true;
                    createQueue.Options.EnableStatus = true;
                    createQueue.Options.EnableStatusTable = true;
                    createQueue.Options.EnableHistory = false;
                });
            }

            [TestCleanup]
            public void Cleanup()
            {
                _helper?.RemoveQueue<SqlServerMessageQueueInit, SqlServerMessageQueueCreation>();
            }

            private static string ReadAppConfigValue(string appConfigPath, string key)
            {
                var doc = XDocument.Load(appConfigPath);
                var element = doc.Root?.Element("appSettings")?
                    .Elements("add")
                    .FirstOrDefault(e => e.Attribute("key")?.Value == key);
                return element?.Attribute("value")?.Value
                    ?? throw new InvalidOperationException(
                        $"Key '{key}' not found in {appConfigPath}");
            }
        }
    }
    ```

    ---

    **PostgreSqlTests.cs** -- `[TestCategory("LocalOnly")]`

    Same pattern as SQL Server but with PostgreSQL types.

    ```csharp
    using System;
    using System.IO;
    using System.Linq;
    using System.Xml.Linq;
    using DotNetWorkQueue.Transport.PostgreSQL.Basic;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    namespace IntegrationTests
    {
        [TestClass]
        public class PostgreSqlTests
        {
            private ProduceConsumeTestHelper _helper;

            [TestInitialize]
            public void Setup()
            {
                var connectionString = ReadAppConfigValue(
                    Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
                        "..", "..", "..", "..", "PostgreSQL", "PostgreSQLProducer", "App.config")),
                    "Database");
                var queueName = $"test_{Guid.NewGuid():N}";
                _helper = new ProduceConsumeTestHelper(queueName, connectionString, messageCount: 5);
            }

            [TestMethod]
            [TestCategory("LocalOnly")]
            public void ProduceConsume()
            {
                _helper.RunTest<PostgreSqlMessageQueueInit, PostgreSqlMessageQueueCreation>(createQueue =>
                {
                    createQueue.Options.EnableDelayedProcessing = true;
                    createQueue.Options.EnableHeartBeat = true;
                    createQueue.Options.EnableMessageExpiration = true;
                    createQueue.Options.EnableStatus = true;
                    createQueue.Options.EnableStatusTable = true;
                    createQueue.Options.EnableHistory = false;
                });
            }

            [TestCleanup]
            public void Cleanup()
            {
                _helper?.RemoveQueue<PostgreSqlMessageQueueInit, PostgreSqlMessageQueueCreation>();
            }

            private static string ReadAppConfigValue(string appConfigPath, string key)
            {
                var doc = XDocument.Load(appConfigPath);
                var element = doc.Root?.Element("appSettings")?
                    .Elements("add")
                    .FirstOrDefault(e => e.Attribute("key")?.Value == key);
                return element?.Attribute("value")?.Value
                    ?? throw new InvalidOperationException(
                        $"Key '{key}' not found in {appConfigPath}");
            }
        }
    }
    ```

    IMPORTANT NOTES FOR THE BUILDER:
    - The `using System.Linq;` is needed for `FirstOrDefault()` in the XML query.
    - The App.config relative path uses `AppContext.BaseDirectory` (the test output directory)
      and navigates up to `Source/Samples/` then into the transport folder. The exact number
      of `..` segments depends on the output path structure. Verify by logging the resolved
      path in the test if it fails.
    - For Redis, the `Database` value from App.config is used directly as the connection string.
    - For SQL Server and PostgreSQL, the `Database` value is a full ADO.NET connection string.
    - LiteDb uses temp file path (like SQLite), NOT the App.config Database value.
  </action>
  <verify>
    dotnet build "Source/Samples/IntegrationTests/IntegrationTests.sln" -c Debug -f net10.0 && dotnet test "Source/Samples/IntegrationTests/IntegrationTests.sln" -c Debug -f net10.0 --no-build --filter "TestCategory=CI" -v normal 2>&1 | tail -15
  </verify>
  <done>
    1. All 4 new test classes compile with 0 errors.
    2. `dotnet test --filter TestCategory=CI` passes (runs SQLite + LiteDb tests).
    3. `dotnet test --list-tests` shows 5 ProduceConsume test methods (one per transport).
  </done>
</task>

<task id="3" files="Source/Samples/IntegrationTests/LiteDbTests.cs, Source/Samples/IntegrationTests/RedisTests.cs, Source/Samples/IntegrationTests/SqlServerTests.cs, Source/Samples/IntegrationTests/PostgreSqlTests.cs" tdd="false">
  <action>
    Verify the full test suite end-to-end:

    1. Run CI tests: `dotnet test --filter TestCategory=CI` -- both SQLite and LiteDb must pass.
    2. List all tests: `dotnet test --list-tests` -- should show 5 ProduceConsume methods.
    3. Verify LiteDb cleanup: no leftover `.db` files in temp directory after test run.
    4. For server-based tests (Redis, SqlServer, PostgreSQL), verify they are discoverable
       but do NOT run them unless the servers are available. If you want to test one and
       the server is reachable, run with `--filter "FullyQualifiedName~Redis"` etc.

    Common failure modes:
    - App.config path resolution: The relative path from the test output directory to the
      sample App.config must be correct. If `FileNotFoundException`, adjust the `..` count.
      The output dir is typically `bin/Debug/net10.0/` so you need 4 levels up to reach
      `Source/Samples/` then down into the transport folder.
    - LiteDb `Connection=shared` requirement: LiteDb requires this in the connection string
      for multi-process access. Without it, the producer and consumer containers may deadlock.
    - Redis connection timeout: If Redis is on a remote host and unreachable, the test will
      hang. The 30-second timeout in ProduceConsumeTestHelper will catch this.
  </action>
  <verify>
    dotnet test "Source/Samples/IntegrationTests/IntegrationTests.sln" -c Debug -f net10.0 --no-build --filter "TestCategory=CI" -v normal && dotnet test "Source/Samples/IntegrationTests/IntegrationTests.sln" -c Debug -f net10.0 --no-build --list-tests 2>&1 | grep ProduceConsume
  </verify>
  <done>
    1. CI tests pass (SQLite + LiteDb).
    2. All 5 ProduceConsume tests are discoverable.
    3. No leftover temp files from LiteDb tests.
  </done>
</task>

## Verification

```bash
# Build
dotnet build "Source/Samples/IntegrationTests/IntegrationTests.sln" -c Debug

# CI tests (SQLite + LiteDb, no external deps)
dotnet test "Source/Samples/IntegrationTests/IntegrationTests.sln" -c Debug --filter "TestCategory=CI" -v normal

# All tests discoverable
dotnet test "Source/Samples/IntegrationTests/IntegrationTests.sln" -c Debug --list-tests | grep -c "ProduceConsume"
# Expected: 5

# No leftover temp files
ls /tmp/test_*.db3 /tmp/test_*.db 2>/dev/null && echo "FAIL" || echo "PASS"
```
