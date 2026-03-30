# Phase 3 Research: MSTest Integration Test Project (SQLite Vertical Slice)

## 1. SQLite Producer -- Queue Creation and Message Production

**File:** `Source/Samples/SQLite/SQLiteProducer/Program.cs`

### Queue creation pattern

```csharp
using (var createQueueContainer = new QueueCreationContainer<SqLiteMessageQueueInit>(
    serviceRegister => Injectors.AddInjectors(logFactory, enableTrace, enableMetrics,
        enableCompression, enableEncryption, "SQLiteProducer", serviceRegister),
    options => Injectors.SetOptions(options, enableChaos)))
{
    using (var createQueue = createQueueContainer.GetQueueCreation<SqLiteMessageQueueCreation>(queueConnection))
    {
        if (!createQueue.QueueExists)
        {
            createQueue.Options.EnableDelayedProcessing = true;
            createQueue.Options.EnableHeartBeat = true;
            createQueue.Options.EnableMessageExpiration = true;
            createQueue.Options.EnableStatus = true;
            createQueue.Options.EnableStatusTable = true;
            createQueue.Options.EnableHistory = false; // set from config; use false for tests
            var result = createQueue.CreateQueue();
        }
    }
}
```

Key types (namespaces):
- `QueueCreationContainer<T>` -- `DotNetWorkQueue` (core)
- `SqLiteMessageQueueInit` -- `DotNetWorkQueue.Transport.SQLite.Basic`
- `SqLiteMessageQueueCreation` -- `DotNetWorkQueue.Transport.SQLite.Basic`
- `QueueConnection` -- `DotNetWorkQueue.Configuration`

### Producer creation and message send pattern

```csharp
using (var queueContainer = new QueueContainer<SqLiteMessageQueueInit>(
    serviceRegister => Injectors.AddInjectors(..., serviceRegister),
    options => Injectors.SetOptions(options, enableChaos)))
{
    using (var queue = queueContainer.CreateProducer<SimpleMessage>(queueConnection))
    {
        queue.Send(message);           // or queue.Send(message, additionalData)
    }
}
```

`RunProducer.RunLoop` is the interactive loop used in samples; tests should call `queue.Send()` directly.

### Connection string format

```
Data Source={filePath};Version=3;
```

Example from App.config + runtime assembly:
```
Data Source=C:\Users\<user>\Documents\test.db3;Version=3;
```

For integration tests, use `System.IO.Path.GetTempFileName()` (replace extension to `.db3`) or a fixed temp path so cleanup is deterministic. The file path is fully caller-controlled -- no server required.

### Queue options set in all SQLite producer samples

| Option | Value |
|--------|-------|
| `EnableDelayedProcessing` | `true` |
| `EnableHeartBeat` | `true` |
| `EnableMessageExpiration` | `true` |
| `EnableStatus` | `true` |
| `EnableStatusTable` | `true` |
| `EnableHistory` | config-driven (use `false` in tests) |

---

## 2. SQLite Consumer -- Startup, Notifications, Worker Config

**File:** `Source/Samples/SQLite/SQLiteConsumer/Program.cs`

### Consumer creation and start pattern

```csharp
using (var queueContainer = new QueueContainer<SqLiteMessageQueueInit>(
    serviceRegister => Injectors.AddInjectors(..., serviceRegister),
    options => Injectors.SetOptions(options, enableChaos)))
{
    using (var queue = queueContainer.CreateConsumer(queueConnection))
    {
        queue.Configuration.Worker.WorkerCount = 4;
        queue.Configuration.HeartBeat.UpdateTime = "sec(*%10)";
        queue.Configuration.HeartBeat.MonitorTime = TimeSpan.FromSeconds(15);
        queue.Configuration.HeartBeat.Time = TimeSpan.FromSeconds(35);
        queue.Configuration.TransportConfiguration.RetryDelayBehavior.Add(
            typeof(InvalidDataException),
            new List<TimeSpan> { TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(9) });
        queue.Configuration.MessageExpiration.Enabled = true;
        queue.Configuration.MessageExpiration.MonitorTime = TimeSpan.FromSeconds(20);

        queue.Start<SimpleMessage>(MessageProcessing.HandleMessages, CreateNotifications.Create(log));
        // blocks until Ctrl+C in samples; tests should use a ManualResetEvent + timeout
    }
}
```

For integration tests: use `WorkerCount = 1`, reduced heartbeat times, and a `CountdownEvent` or `ManualResetEventSlim` signalled inside a custom message handler to detect completion without blocking forever.

The consumer checks `createQueue.QueueExists` before starting -- tests must ensure the queue was created first (same pattern as producer).

---

## 3. SQLite Producer App.config

**File:** `Source/Samples/SQLite/SQLiteProducer/App.config`

```xml
<appSettings>
  <add key="Database" value="\test.db3" />
  <add key="QueueName" value="testing" />
  <add key="EnableTrace" value="true" />
  <add key="EnableMetrics" value="false" />
  <add key="EnableCompression" value="true" />
  <add key="EnableEncryption" value="true" />
  <add key="EnableChaos" value="false" />
  <add key="EnableHistory" value="true" />
</appSettings>
```

The `Database` value is appended to `%userprofile%\Documents\`. Integration tests should **not** use App.config for the connection string -- construct it directly in code using a temp path. All feature toggles (trace, metrics, compression, encryption, chaos) should be `false` in tests to eliminate external dependencies.

---

## 4. SampleShared Key Files

### SharedConfiguration.cs

A static class that reads `ConfigurationManager.AppSettings` in a static constructor. Properties are all `public static bool` (get-only):

- `EnableTrace`
- `EnableMetrics`
- `EnableCompression`
- `EnableEncryption`
- `EnableChaos`
- `EnableDashboard`
- `EnableHistory`
- `DashboardApiUrl` (string, default `"https://localhost:32906"`)

**Integration test impact:** `SharedConfiguration` reads from `ConfigurationManager.AppSettings` which reads from the test project's app.config / appsettings. Tests should not call `SharedConfiguration` properties directly; pass the values as literals (`false`) when calling `Injectors.AddInjectors(...)`.

### Injectors.cs

**Namespace:** `SampleShared`

Key method signatures:

```csharp
// Register DI services into the container
public static void AddInjectors(
    ILoggerFactory logFactory,
    bool addTrace,
    bool addMetrics,
    bool enableGzip,
    bool enableEncryption,
    string appName,
    IContainer container)

// Set chaos policy option
public static void SetOptions(IContainer container, bool enableChaos)
```

For tests: pass `false` for all boolean flags. `logFactory` can be obtained from `Helpers.CreateForSerilog()` or `LoggerFactory.Create(...)` with a null/noop sink. No trace, metrics, compression, or encryption needed for a basic integration test.

The `AddTrace` path requires `tracesettings.json` to exist and contain a `Jaeger` section -- if `addTrace = false` this file is never read.

The `AddMetrics` path requires an OTLP endpoint or falls back to console -- if `addMetrics = false` this is skipped entirely.

### MessageProcessing.cs

**Namespace:** `SampleShared`

```csharp
public static void HandleMessages(
    IReceivedMessage<SimpleMessage> arg1,
    IWorkerNotification arg2)
```

Behavior:
- Logs the message ID and processing time
- If `arg1.Body.Error == ErrorTypes.Error` -- triggers divide-by-zero (poison message scenario)
- If `arg1.Body.Error == ErrorTypes.RetryableErrorFail` -- throws `InvalidDataException` every time
- If `arg1.Body.Error == ErrorTypes.RetryableError` -- throws `InvalidDataException` first time only, then succeeds
- Otherwise: sleeps for `arg1.Body.ProcessingTime` ms (or waits on cancellation token if transport supports rollback)

For tests: create `SimpleMessage` with `Error = ErrorTypes.None` and `ProcessingTime = 0` (or a small value like `10`) to avoid sleep delays.

### Messages.cs

**Namespace:** `SampleShared`

Key factory methods:

```csharp
// Create a single message with specified payload and processing time
public static SimpleMessage CreateSimpleMessage(int messagePayloadLength, int processingTime)

// Create an IEnumerable of N messages
public static IEnumerable<SimpleMessage> CreateSimpleMessage(int count, int sleepTime, int size)
```

For tests: `Messages.CreateSimpleMessage(10, 0)` creates a message with 10-char payload and 0ms processing time.

### CreateNotifications.cs

**Namespace:** `SampleShared`

```csharp
public static ConsumerQueueNotifications Create(ILogger logger)
```

`ConsumerQueueNotifications` is from `DotNetWorkQueue.Queue` (namespace) / `DotNetWorkQueue.Notifications` (notification types). Constructor takes six `Action<T>` delegates: error, receive error, moved-to-error-queue, poison message, rollback, completed.

For tests: call `CreateNotifications.Create(Log.Logger)` where `Log.Logger` is a Serilog logger, or create a `ConsumerQueueNotifications` directly with no-op lambdas.

### SimpleMessage.cs

**Namespace:** `SampleShared`

```csharp
public class SimpleMessage
{
    public string Message { get; set; }
    public int ProcessingTime { get; set; }   // milliseconds to sleep during handling
    public ErrorTypes Error { get; set; }     // default: ErrorTypes.None (0)
}

public enum ErrorTypes { None = 0, Error = 1, RetryableError = 2, RetryableErrorFail = 3 }
```

---

## 5. Existing csproj Patterns

**Reference file:** `Source/Samples/SQLite/SQLiteConsumer/SQLiteConsumer.csproj`

### Dual-target framework setup

```xml
<TargetFrameworks>net8.0;net48</TargetFrameworks>
```

### SampleShared HintPath reference (framework-conditional)

```xml
<ItemGroup Condition=" '$(TargetFramework)' == 'net48' ">
  <Reference Include="SampleShared">
    <HintPath>..\..\SampleShared\bin\Debug\net48\SampleShared.dll</HintPath>
  </Reference>
</ItemGroup>
<ItemGroup Condition=" '$(TargetFramework)' == 'net8.0' ">
  <Reference Include="SampleShared">
    <HintPath>..\..\SampleShared\bin\Debug\net8.0\SampleShared.dll</HintPath>
  </Reference>
</ItemGroup>
```

The test project will be at `Source/Samples/IntegrationTests/IntegrationTests/`, so the HintPath relative to that location will need to go up three levels to reach SampleShared:
- `net48`: `..\..\..\SampleShared\bin\Debug\net48\SampleShared.dll`
- `net8.0`: `..\..\..\SampleShared\bin\Debug\net8.0\SampleShared.dll`

### Key NuGet packages in SQLite consumer csproj (versions as of current state)

| Package | Version |
|---------|---------|
| `DotNetWorkQueue` | 0.9.13 |
| `DotNetWorkQueue.Transport.SQLite` | 0.9.13 |
| `System.Data.SQLite.Core` | 1.0.119 |
| `Stub.System.Data.SQLite.Core.NetFramework` | 1.0.119 |
| `Serilog` | 4.3.0 |
| `Serilog.Extensions.Logging` | 10.0.0 |
| `Serilog.Sinks.Console` | 6.1.1 |
| `SimpleInjector` | 5.5.0 |
| `OpenTelemetry` | 1.14.0 |
| `System.Configuration.ConfigurationManager` | 10.0.1 |
| `Microsoft.Extensions.Logging` | 10.0.1 |
| `Microsoft.Extensions.Logging.Abstractions` | 10.0.1 |
| `Newtonsoft.Json` | 13.0.4 |

The test project does NOT need most of these -- it needs the core queue packages, SQLite transport, Serilog, and MSTest. The `DotNetWorkQueue.Dashboard.Client` package is `net8.0`-only and not needed in tests.

### CopyToOutputDirectory items

- `tracesettings.json` -- `Always` (not needed in test project if trace is disabled)
- `metricsettings.json` -- `PreserveNewest` (linked from SampleShared folder, not needed if metrics disabled)

The test project needs neither if both trace and metrics are `false`.

---

## 6. Queue Cleanup / Removal

**Finding:** There are no `RemoveQueue`, `DeleteQueue`, or queue-deletion calls anywhere in the existing samples. The samples never clean up after themselves.

The `SqLiteMessageQueueCreation` object (returned by `createQueueContainer.GetQueueCreation<SqLiteMessageQueueCreation>(...)`) inherits from the base creation class. The DotNetWorkQueue API does expose a `RemoveQueue()` method on creation objects in the library (visible in the library source), but it is not used in any sample.

**For integration tests:** Queue cleanup should be done by:
1. Calling `createQueue.RemoveQueue()` inside the test teardown (if the method exists and is accessible on `SqLiteMessageQueueCreation` -- needs verification at implementation time)
2. Fallback: delete the SQLite `.db3` file directly using `System.IO.File.Delete(dbFilePath)` in `[TestCleanup]` -- this is reliable since SQLite is file-based
3. Use a unique per-test queue name (e.g., `$"test_queue_{Guid.NewGuid():N}"`) and a temp file path to ensure test isolation

SQLite being file-based makes cleanup straightforward: delete the file. The connection string `Data Source={path};Version=3;` points to an ordinary file on disk.

---

## 7. SQLite-Specific Type Names

All in namespace `DotNetWorkQueue.Transport.SQLite.Basic` (assembly: `DotNetWorkQueue.Transport.SQLite`):

| Type | Role |
|------|------|
| `SqLiteMessageQueueInit` | Transport init type -- generic parameter for `QueueCreationContainer<T>` and `QueueContainer<T>` |
| `SqLiteMessageQueueCreation` | Creation/schema management -- generic parameter for `GetQueueCreation<T>()` |

Both are used identically across all 7 SQLite sample projects (confirmed in producer, consumer, consumerlinq, consumerasync, scheduler, schedulerconsumer, producerlinq).

The scheduler variant uses `JobQueueCreationContainer<SqLiteMessageQueueInit>` instead of `QueueCreationContainer<SqLiteMessageQueueInit>` -- not relevant for basic produce/consume tests.

---

## 8. Test Architecture Implications

### Produce-then-consume flow for integration test

1. Generate unique DB file path in temp dir
2. Build `QueueConnection(queueName, connectionString)`
3. Create queue via `QueueCreationContainer<SqLiteMessageQueueInit>` + `SqLiteMessageQueueCreation` with all options enabled
4. Produce N messages via `QueueContainer<SqLiteMessageQueueInit>` + `CreateProducer<SimpleMessage>`
5. Start consumer via `QueueContainer<SqLiteMessageQueueInit>` + `CreateConsumer` with a custom handler that counts processed messages and signals a `CountdownEvent`
6. Wait on `CountdownEvent` with a timeout (e.g., 30 seconds) -- assert it was signalled
7. Dispose consumer, then delete the DB file in `[TestCleanup]`

### Notes on avoiding SharedConfiguration

Tests should NOT rely on `SharedConfiguration` (it reads `ConfigurationManager.AppSettings` from the running process's config). Pass all feature flags as literal `false` values directly to `Injectors.AddInjectors(...)`.

### Notes on Serilog logger for tests

`CreateNotifications.Create(ILogger)` takes a Serilog `ILogger`. In test code, use `Log.Logger` after calling `Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger()` in `[TestInitialize]`, or create a null sink logger: `new LoggerConfiguration().CreateLogger()`.

`Helpers.CreateForSerilog()` returns a `Microsoft.Extensions.Logging.ILoggerFactory` wired to Serilog -- usable directly for `Injectors.AddInjectors(logFactory, ...)`.
