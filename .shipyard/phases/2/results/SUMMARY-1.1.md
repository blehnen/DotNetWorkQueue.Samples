---
plan: "1.1"
transport: Redis
status: completed
date: 2026-03-26
---

# SUMMARY-1.1: Redis Transport -- NuGet Bump, History Removal, and EnableHistory Addition

## Tasks Executed

### Task 1: NuGet Bump (7 .csproj files)

Bumped all three DotNetWorkQueue.* packages from 0.9.10 to 0.9.11 in every Redis .csproj:

| File | DotNetWorkQueue | Transport.Redis | Dashboard.Client |
|---|---|---|---|
| RedisConsumer.csproj | 0.9.11 | 0.9.11 | 0.9.11 |
| RedisConsumerAsync.csproj | 0.9.11 | 0.9.11 | 0.9.11 |
| RedisConsumerLinq.csproj | 0.9.11 | 0.9.11 | 0.9.11 |
| RedisProducer.csproj | 0.9.11 | 0.9.11 | 0.9.11 |
| RedisProducerLinq.csproj | 0.9.11 | 0.9.11 | 0.9.11 |
| RedisScheduler.csproj | 0.9.11 | 0.9.11 | 0.9.11 |
| RedisSchedulerConsumer.csproj | 0.9.11 | 0.9.11 | 0.9.11 |

Verification: `grep -r "DotNetWorkQueue.*0\.9\.10" --include="*.csproj"` returned EXIT:1 (no matches).
Count check: 21 matches for 0.9.11 (3 packages x 7 files). Confirmed.

### Task 2: Remove History.Enabled (4 consumer Program.cs files)

Deleted `queue.Configuration.History.Enabled = SharedConfiguration.EnableHistory;` from:

- RedisConsumer/Program.cs (was line 61)
- RedisConsumerAsync/Program.cs (was line 71)
- RedisConsumerLinq/Program.cs (was line 75)
- RedisSchedulerConsumer/Program.cs (was line 71)

No double-blank-line artifacts introduced. Each deletion removed a single statement line;
adjacent lines had no blank-line gaps that needed collapsing.

Verification: `grep -r "History\.Enabled" --include="*.cs"` returned EXIT:1 (no matches).

### Task 3: Add RedisBaseTransportOptions.EnableHistory (2 producer Program.cs files)

Converted the expression lambda to a block lambda in both producers. Both files already
had `using DotNetWorkQueue.Transport.Redis.Basic;` -- no new using directive was needed.

RedisProducer/Program.cs and RedisProducerLinq/Program.cs now contain:

```csharp
, options => {
    Injectors.SetOptions(options, SharedConfiguration.EnableChaos);
    options.GetInstance<RedisBaseTransportOptions>().EnableHistory = SharedConfiguration.EnableHistory;
}))
```

Verification: `grep -r "EnableHistory" --include="*.cs"` returned exactly 2 matches,
one in each producer file.

## Build Results

```
dotnet restore "Source/Samples/Redis/Samples.sln"  -- all 7 projects restored
dotnet build "Source/Samples/Redis/Samples.sln" -c Debug

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:10.79
```

All 7 projects built successfully for both net8.0 and net48 target frameworks.

## Deviations

None. Plan was executed exactly as specified. The Task 1 verify command
(`grep -r "DotNetWorkQueue.*0\.9\.10" Source/Samples/Redis/`) produces output from
`bin/` and `.vs/` artifacts when run without `--include="*.csproj"`, but the done
criteria (no matches in .csproj files) is fully satisfied -- those artifacts are
pre-existing build outputs from the previous version and will be overwritten on
the next build.

## Files Modified

- `Source/Samples/Redis/RedisConsumer/RedisConsumer.csproj`
- `Source/Samples/Redis/RedisConsumerAsync/RedisConsumerAsync.csproj`
- `Source/Samples/Redis/RedisConsumerLinq/RedisConsumerLinq.csproj`
- `Source/Samples/Redis/RedisProducer/RedisProducer.csproj`
- `Source/Samples/Redis/RedisProducerLinq/RedisProducerLinq.csproj`
- `Source/Samples/Redis/RedisScheduler/RedisScheduler.csproj`
- `Source/Samples/Redis/RedisSchedulerConsumer/RedisSchedulerConsumer.csproj`
- `Source/Samples/Redis/RedisConsumer/Program.cs`
- `Source/Samples/Redis/RedisConsumerAsync/Program.cs`
- `Source/Samples/Redis/RedisConsumerLinq/Program.cs`
- `Source/Samples/Redis/RedisSchedulerConsumer/Program.cs`
- `Source/Samples/Redis/RedisProducer/Program.cs`
- `Source/Samples/Redis/RedisProducerLinq/Program.cs`
