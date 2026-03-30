---
phase: test-infrastructure-sqlite-slice
plan: "2.1"
wave: 2
dependencies: ["1.1"]
must_haves:
  - ProduceConsumeTestHelper class encapsulating full produce-consume-assert lifecycle
  - SqliteTests class with [TestCategory("CI")] and a ProduceConsume test method
  - Queue creation with EnableDelayedProcessing, EnableHeartBeat, EnableMessageExpiration, EnableStatus, EnableStatusTable
  - Consumer notification wiring with CountdownEvent for completion signaling
  - Queue cleanup via RemoveQueue() plus temp file deletion
  - dotnet test --filter "TestCategory=CI&FullyQualifiedName~Sqlite" passes end-to-end
files_touched:
  - Source/Samples/IntegrationTests/ProduceConsumeTestHelper.cs
  - Source/Samples/IntegrationTests/SqliteTests.cs
tdd: false
---

# Plan 2.1: ProduceConsumeTestHelper + SqliteTests Vertical Slice

## Context

Plan 1.1 established the project infrastructure (sln, csproj, App.config). This plan adds the
two C# source files that make the tests actually run: a shared helper class and the SQLite test
class. When this plan is complete, `dotnet test --filter "TestCategory=CI&FullyQualifiedName~Sqlite"`
passes end-to-end with 5 messages produced and consumed.

## Dependencies

- Plan 1.1 (project must restore and build).
- SampleShared must be built (Phase 2 prerequisite).

## Key Design Decisions

1. **ProduceConsumeTestHelper is NOT generic over TInit/TCreation.** Making it generic creates
   complexity around constraint resolution. Instead, it is a static helper with methods that
   accept the required containers and types as parameters. Each transport test class is responsible
   for creating its own `QueueCreationContainer<TInit>` and `QueueContainer<TInit>` -- the helper
   provides the produce, consume, and assert logic.

   Actually, on reconsideration: the helper should encapsulate the full lifecycle to avoid
   duplicating the produce-consume-wait-assert pattern in every test class. The approach:
   `ProduceConsumeTestHelper` is a concrete class instantiated per test, holding the connection
   info, message count, and timeout. It provides `RunTest<TInit, TCreation>(Action<TCreation> configureOptions)`
   as the main entry point. Each test class calls this with its transport-specific types and
   option configuration lambda.

2. **ConsumerQueueNotifications with CountdownEvent.** The test helper creates its own
   `ConsumerQueueNotifications` (from `DotNetWorkQueue.Notifications` / `DotNetWorkQueue.Queue`)
   that wraps a `CountdownEvent(messageCount)`. The `OnMessageCompleted` callback calls
   `countdownEvent.Signal()`. The `OnPoisonMessage` and `OnError` callbacks increment atomic
   error counters. After starting the consumer, the helper waits on the CountdownEvent with a
   30-second timeout, then asserts: countdown reached zero (all messages processed), error
   count is zero, poison count is zero.

3. **Queue creation options.** Per D3 (CONTEXT-3.md), match sample defaults:
   `EnableDelayedProcessing=true`, `EnableHeartBeat=true`, `EnableMessageExpiration=true`,
   `EnableStatus=true`, `EnableStatusTable=true`, `EnableHistory=false` (from App.config via
   SharedConfiguration). The `configureOptions` lambda sets these on the creation object.

4. **Message handler.** Per D2 (CONTEXT-3.md), reuse `MessageProcessing.HandleMessages` from
   SampleShared. Messages are created with `ErrorTypes.None` and `ProcessingTime=0`, so the
   handler returns immediately with no sleep and no exceptions.

5. **Consumer configuration.** Use `WorkerCount=1` (simpler for tests), minimal heartbeat
   settings, and message expiration enabled. The consumer's `Start<SimpleMessage>` call takes
   `MessageProcessing.HandleMessages` as the handler and a custom `ConsumerQueueNotifications`
   with the CountdownEvent wiring.

6. **SQLite connection string.** Construct directly in code: `Data Source={tempFilePath};Version=3;`.
   Use `Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.db3")` for the temp file.
   This avoids any dependency on App.config's Database setting or %userprofile% expansion.

7. **Cleanup.** In `[TestCleanup]`: (a) call `createQueue.RemoveQueue()` on the
   `SqLiteMessageQueueCreation` object to drop queue tables, (b) delete the `.db3` file and
   any `-journal` or `-wal` files from the temp directory using the known base path.

## Tasks

<task id="1" files="Source/Samples/IntegrationTests/ProduceConsumeTestHelper.cs" tdd="false">
  <action>
    Create `Source/Samples/IntegrationTests/ProduceConsumeTestHelper.cs` with a class that
    encapsulates the produce-consume-assert lifecycle. Key structure:

    **Namespace:** `IntegrationTests`

    **Using directives:**
    ```
    using System;
    using System.Threading;
    using DotNetWorkQueue;
    using DotNetWorkQueue.Configuration;
    using DotNetWorkQueue.Notifications;
    using DotNetWorkQueue.Queue;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using SampleShared;
    using Serilog;
    ```

    **Class: `ProduceConsumeTestHelper`** (public, non-static)

    **Constructor parameters:**
    - `string queueName` -- unique name like `$"test_{Guid.NewGuid():N}"`
    - `string connectionString` -- transport-specific connection string
    - `int messageCount` -- number of messages to produce (default 5)
    - `TimeSpan? timeout` -- consumer wait timeout (default 30 seconds)

    Store these as readonly fields.

    **Method: `RunTest<TInit, TCreation>(Action<TCreation> configureQueueOptions)`**
    where `TInit : class, ITransportInit, new()` and `TCreation : class, IQueueCreation`

    Implementation flow:

    1. Set up Serilog logger:
       ```csharp
       Log.Logger = new LoggerConfiguration()
           .WriteTo.Console()
           .MinimumLevel.Information()
           .CreateLogger();
       var logFactory = Helpers.CreateForSerilog();
       ```

    2. Create queue connection:
       ```csharp
       var queueConnection = new QueueConnection(queueName, connectionString);
       ```

    3. Create and configure queue:
       ```csharp
       using (var createQueueContainer = new QueueCreationContainer<TInit>(
           serviceRegister => Injectors.AddInjectors(logFactory,
               SharedConfiguration.EnableTrace,
               SharedConfiguration.EnableMetrics,
               SharedConfiguration.EnableCompression,
               SharedConfiguration.EnableEncryption,
               "IntegrationTest", serviceRegister),
           options => Injectors.SetOptions(options, SharedConfiguration.EnableChaos)))
       {
           using (var createQueue = createQueueContainer.GetQueueCreation<TCreation>(queueConnection))
           {
               configureQueueOptions(createQueue);
               var result = createQueue.CreateQueue();
               // Assert queue was created (result.Status should indicate success or already exists)
           }
       }
       ```

    4. Produce messages:
       ```csharp
       using (var queueContainer = new QueueContainer<TInit>(
           serviceRegister => Injectors.AddInjectors(logFactory,
               SharedConfiguration.EnableTrace,
               SharedConfiguration.EnableMetrics,
               SharedConfiguration.EnableCompression,
               SharedConfiguration.EnableEncryption,
               "IntegrationTest", serviceRegister),
           options => Injectors.SetOptions(options, SharedConfiguration.EnableChaos)))
       {
           using (var queue = queueContainer.CreateProducer<SimpleMessage>(queueConnection))
           {
               for (int i = 0; i < messageCount; i++)
               {
                   var message = Messages.CreateSimpleMessage(10, 0); // 10-char payload, 0ms processing
                   queue.Send(message);
               }
           }
       }
       ```

    5. Consume messages with completion tracking:
       ```csharp
       var completedCount = 0;
       var poisonCount = 0;
       var errorCount = 0;
       using (var completionEvent = new ManualResetEventSlim(false))
       {
           var notifications = new ConsumerQueueNotifications(
               (notification) => { Interlocked.Increment(ref errorCount); },    // OnError
               (notification) => { Interlocked.Increment(ref errorCount); },    // OnReceiveMessageError
               (notification) => { Interlocked.Increment(ref errorCount); },    // OnMessageMovedToErrorQueue
               (notification) => { Interlocked.Increment(ref poisonCount); },   // OnPoisonMessage
               (notification) => { },                                            // OnMessageRollBack
               (notification) => {                                               // OnMessageCompleted
                   if (Interlocked.Increment(ref completedCount) >= messageCount)
                       completionEvent.Set();
               });

           using (var queueContainer = new QueueContainer<TInit>(
               serviceRegister => Injectors.AddInjectors(logFactory,
                   SharedConfiguration.EnableTrace,
                   SharedConfiguration.EnableMetrics,
                   SharedConfiguration.EnableCompression,
                   SharedConfiguration.EnableEncryption,
                   "IntegrationTest", serviceRegister),
               options => Injectors.SetOptions(options, SharedConfiguration.EnableChaos)))
           {
               using (var queue = queueContainer.CreateConsumer(queueConnection))
               {
                   queue.Configuration.Worker.WorkerCount = 1;
                   queue.Configuration.HeartBeat.UpdateTime = "sec(*%10)";
                   queue.Configuration.HeartBeat.MonitorTime = TimeSpan.FromSeconds(15);
                   queue.Configuration.HeartBeat.Time = TimeSpan.FromSeconds(35);
                   queue.Configuration.MessageExpiration.Enabled = true;
                   queue.Configuration.MessageExpiration.MonitorTime = TimeSpan.FromSeconds(20);

                   queue.Start<SimpleMessage>(MessageProcessing.HandleMessages, notifications);

                   var waitResult = completionEvent.Wait(timeout ?? TimeSpan.FromSeconds(30));
                   Assert.IsTrue(waitResult,
                       $"Timed out waiting for messages. Completed: {completedCount}/{messageCount}");
               }
           }
       }

       // Final assertions
       Assert.AreEqual(messageCount, completedCount,
           $"Expected {messageCount} completed messages but got {completedCount}");
       Assert.AreEqual(0, poisonCount, $"Expected 0 poison messages but got {poisonCount}");
       Assert.AreEqual(0, errorCount, $"Expected 0 errors but got {errorCount}");
       ```

    **Method: `RemoveQueue<TInit, TCreation>()`**
    where `TInit : class, ITransportInit, new()` and `TCreation : class, IQueueCreation`

    Called from test cleanup. Creates a `QueueCreationContainer<TInit>`, gets the creation
    object, calls `createQueue.RemoveQueue()`, and disposes. Wrapped in try-catch so cleanup
    failures don't mask test failures.

    ```csharp
    public void RemoveQueue<TInit, TCreation>()
        where TInit : class, ITransportInit, new()
        where TCreation : class, IQueueCreation
    {
        try
        {
            var logFactory = Helpers.CreateForSerilog();
            var queueConnection = new QueueConnection(queueName, connectionString);
            using (var createQueueContainer = new QueueCreationContainer<TInit>(
                serviceRegister => Injectors.AddInjectors(logFactory,
                    false, false, false, false,
                    "IntegrationTest", serviceRegister),
                options => Injectors.SetOptions(options, false)))
            {
                using (var createQueue = createQueueContainer.GetQueueCreation<TCreation>(queueConnection))
                {
                    createQueue.RemoveQueue();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Queue removal failed: {ex.Message}");
        }
    }
    ```

    IMPORTANT IMPLEMENTATION NOTES:
    - The `IQueueCreation` interface is in namespace `DotNetWorkQueue`. Verify at build time
      that `RemoveQueue()` exists on it. If not, the method may be on a more specific interface
      or base class. Check the DotNetWorkQueue NuGet package's public API. If `RemoveQueue()`
      is not available, remove that call and rely solely on file deletion for SQLite cleanup.
    - The `ITransportInit` interface is in `DotNetWorkQueue`.
    - The `ConsumerQueueNotifications` constructor takes 6 `Action<T>` delegates in this order:
      `Action<ErrorNotification>`, `Action<ErrorReceiveNotification>`, `Action<ErrorNotification>`,
      `Action<PoisonMessageNotification>`, `Action<RollBackNotification>`,
      `Action<MessageCompleteNotification>`. These types are in `DotNetWorkQueue.Notifications`.
    - `QueueConnection` is in `DotNetWorkQueue.Configuration`.
  </action>
  <verify>
    dotnet build "Source/Samples/IntegrationTests/IntegrationTests.sln" -c Debug 2>&1 | tail -5
  </verify>
  <done>
    1. `ProduceConsumeTestHelper.cs` exists and compiles with 0 errors.
    2. The class has `RunTest<TInit, TCreation>()` and `RemoveQueue<TInit, TCreation>()` methods.
    3. `dotnet build` succeeds for both net8.0 and net48.
  </done>
</task>

<task id="2" files="Source/Samples/IntegrationTests/SqliteTests.cs" tdd="false">
  <action>
    Create `Source/Samples/IntegrationTests/SqliteTests.cs` with the SQLite integration test
    class. Key structure:

    **Namespace:** `IntegrationTests`

    **Using directives:**
    ```
    using System;
    using System.IO;
    using DotNetWorkQueue.Transport.SQLite.Basic;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    ```

    **Class: `SqliteTests`**
    - `[TestClass]` attribute
    - Private fields:
      - `ProduceConsumeTestHelper _helper;`
      - `string _dbFilePath;`

    **`[TestInitialize]` method: `Setup()`**
    ```csharp
    _dbFilePath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.db3");
    var queueName = $"test_{Guid.NewGuid():N}";
    var connectionString = $"Data Source={_dbFilePath};Version=3;";
    _helper = new ProduceConsumeTestHelper(queueName, connectionString, messageCount: 5);
    ```

    **`[TestMethod]`, `[TestCategory("CI")]` method: `ProduceConsume()`**
    ```csharp
    _helper.RunTest<SqLiteMessageQueueInit, SqLiteMessageQueueCreation>(createQueue =>
    {
        createQueue.Options.EnableDelayedProcessing = true;
        createQueue.Options.EnableHeartBeat = true;
        createQueue.Options.EnableMessageExpiration = true;
        createQueue.Options.EnableStatus = true;
        createQueue.Options.EnableStatusTable = true;
        createQueue.Options.EnableHistory = false;
        createQueue.CreateQueue();
    });
    ```

    IMPORTANT: Note the `configureQueueOptions` lambda receives the `SqLiteMessageQueueCreation`
    object. It must set the Options properties AND call `createQueue.CreateQueue()`. The helper's
    `RunTest` method calls the lambda and does NOT call `CreateQueue()` itself -- the lambda is
    responsible for both configuration and creation. This gives each transport full control.

    Wait -- re-reading the helper design in Task 1, the helper calls `configureQueueOptions(createQueue)`
    then calls `createQueue.CreateQueue()`. Let me make this consistent. The cleaner design:
    the lambda only configures options, the helper calls CreateQueue(). Update the lambda to NOT
    call CreateQueue():

    ```csharp
    _helper.RunTest<SqLiteMessageQueueInit, SqLiteMessageQueueCreation>(createQueue =>
    {
        createQueue.Options.EnableDelayedProcessing = true;
        createQueue.Options.EnableHeartBeat = true;
        createQueue.Options.EnableMessageExpiration = true;
        createQueue.Options.EnableStatus = true;
        createQueue.Options.EnableStatusTable = true;
        createQueue.Options.EnableHistory = false;
    });
    ```

    The helper's `RunTest` method handles calling `createQueue.CreateQueue()` after the lambda.

    **`[TestCleanup]` method: `Cleanup()`**
    ```csharp
    _helper?.RemoveQueue<SqLiteMessageQueueInit, SqLiteMessageQueueCreation>();
    CleanupSqliteFiles();
    ```

    **Private method: `CleanupSqliteFiles()`**
    ```csharp
    private void CleanupSqliteFiles()
    {
        if (string.IsNullOrEmpty(_dbFilePath)) return;
        var filesToDelete = new[]
        {
            _dbFilePath,
            _dbFilePath + "-journal",
            _dbFilePath + "-wal",
            _dbFilePath + "-shm"
        };
        foreach (var file in filesToDelete)
        {
            try
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not delete {file}: {ex.Message}");
            }
        }
    }
    ```

    Additionally, SQLite creates companion tables with the queue name. The `RemoveQueue()` call
    handles dropping those. The file deletion handles the physical database file.
  </action>
  <verify>
    dotnet build "Source/Samples/IntegrationTests/IntegrationTests.sln" -c Debug && dotnet test "Source/Samples/IntegrationTests/IntegrationTests.sln" -c Debug --no-build --filter "TestCategory=CI&FullyQualifiedName~Sqlite" -v normal 2>&1 | tail -20
  </verify>
  <done>
    1. `SqliteTests.cs` exists and compiles with 0 errors.
    2. `dotnet test --filter "TestCategory=CI&FullyQualifiedName~Sqlite"` passes with 1 test passing.
    3. No SQLite `.db3`, `-journal`, `-wal`, or `-shm` files remain in the temp directory after test cleanup.
    4. Test output shows 5 messages produced and 5 messages completed (visible in Serilog console output at -v normal).
  </done>
</task>

<task id="3" files="Source/Samples/IntegrationTests/ProduceConsumeTestHelper.cs, Source/Samples/IntegrationTests/SqliteTests.cs" tdd="false">
  <action>
    Build and run the full vertical slice end-to-end. If the test fails, debug and fix. Common
    failure modes and their fixes:

    1. **`RemoveQueue()` not found on `IQueueCreation`**: Check if the method is named differently
       (e.g., `DeleteQueue()`, `DropQueue()`). If no queue removal method exists on the interface,
       remove the `RemoveQueue` call from the helper and rely solely on file deletion for SQLite.
       Check the `SqLiteMessageQueueCreation` class's public methods directly.

    2. **`CreateQueue()` returns an error status**: The `CreateQueue()` return type is
       `QueueCreationResult` with a `Status` property. Log the status. If it returns
       `QueueCreationStatus.AlreadyExists`, that's fine for tests (idempotent). Only fail on
       actual errors.

    3. **Consumer timeout (30s exceeded)**: Increase to 60 seconds. Check that messages were
       actually enqueued by logging the producer's send results. Verify the connection string
       points to the same file for both producer and consumer. Verify WorkerCount >= 1.

    4. **Compression/encryption mismatch**: Both producer and consumer must use the same
       `Injectors.AddInjectors()` parameters for `enableGzip` and `enableEncryption`. Since
       both read from `SharedConfiguration` (which reads the test project's App.config), they
       will be consistent. Verify App.config has `EnableCompression=true` and
       `EnableEncryption=true`.

    5. **net48 build failure with missing System.* types**: The csproj needs the same
       framework references as other projects. If CS0012 errors appear for net48, add the
       missing `<Reference Include="System.ComponentModel.Composition" />` etc. in the
       net48-conditional ItemGroup, matching the pattern from SQLiteConsumer.csproj:
       ```xml
       <Reference Include="System.ComponentModel.Composition" />
       <Reference Include="System.ComponentModel.DataAnnotations" />
       <Reference Include="System.Configuration" />
       <Reference Include="System.Runtime.Remoting" />
       <Reference Include="System.Data.DataSetExtensions" />
       ```

    6. **SQLite DLL not found at runtime on net48**: Ensure `Stub.System.Data.SQLite.Core.NetFramework`
       is referenced in the csproj. This package provides the native SQLite interop DLL for net48.

    Run the full verification sequence and confirm the test passes on at least one target framework.
    If net48 has issues that cannot be resolved quickly, the test can be verified on net8.0 first
    and net48 issues logged as a follow-up.
  </action>
  <verify>
    dotnet restore "Source/Samples/IntegrationTests/IntegrationTests.sln" && dotnet build "Source/Samples/IntegrationTests/IntegrationTests.sln" -c Debug --no-restore && dotnet test "Source/Samples/IntegrationTests/IntegrationTests.sln" -c Debug --no-build --filter "TestCategory=CI&FullyQualifiedName~Sqlite" -v normal
  </verify>
  <done>
    1. Full restore-build-test pipeline passes with 0 errors.
    2. Test output shows: "Passed ProduceConsume" (or equivalent MSTest pass indicator).
    3. Console output (at -v normal) shows messages being produced and consumed via Serilog logs.
    4. No leftover temp files after test run (check `ls /tmp/test_*.db3` or equivalent).
    5. The filter `TestCategory=CI&FullyQualifiedName~Sqlite` matches exactly 1 test.
  </done>
</task>

## Verification

```bash
# Prerequisite: SampleShared built
dotnet build "Source/Samples/SampleShared/SampleShared.sln" -c Debug

# Full pipeline
dotnet restore "Source/Samples/IntegrationTests/IntegrationTests.sln"
dotnet build "Source/Samples/IntegrationTests/IntegrationTests.sln" -c Debug --no-restore
dotnet test "Source/Samples/IntegrationTests/IntegrationTests.sln" -c Debug --no-build --filter "TestCategory=CI&FullyQualifiedName~Sqlite" -v normal

# Verify no leftover files
ls /tmp/test_*.db3 2>/dev/null && echo "FAIL: leftover db files" || echo "PASS: no leftover files"

# Verify test count
dotnet test "Source/Samples/IntegrationTests/IntegrationTests.sln" -c Debug --no-build --list-tests | grep -c "ProduceConsume"
# Expected: 1
```
