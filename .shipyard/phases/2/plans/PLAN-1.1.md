---
phase: update-transport-projects
plan: "1.1"
wave: 1
dependencies: ["1.1"]
must_haves:
  - Bump DotNetWorkQueue, DotNetWorkQueue.Transport.Redis, and DotNetWorkQueue.Dashboard.Client from 0.9.10 to 0.9.11 in all 7 Redis .csproj files
  - Remove queue.Configuration.History.Enabled line from 4 Redis consumer Program.cs files
  - Add RedisBaseTransportOptions.EnableHistory to 2 Redis producer Program.cs files
files_touched:
  - Source/Samples/Redis/RedisConsumer/RedisConsumer.csproj
  - Source/Samples/Redis/RedisConsumerAsync/RedisConsumerAsync.csproj
  - Source/Samples/Redis/RedisConsumerLinq/RedisConsumerLinq.csproj
  - Source/Samples/Redis/RedisProducer/RedisProducer.csproj
  - Source/Samples/Redis/RedisProducerLinq/RedisProducerLinq.csproj
  - Source/Samples/Redis/RedisScheduler/RedisScheduler.csproj
  - Source/Samples/Redis/RedisSchedulerConsumer/RedisSchedulerConsumer.csproj
  - Source/Samples/Redis/RedisConsumer/Program.cs
  - Source/Samples/Redis/RedisConsumerAsync/Program.cs
  - Source/Samples/Redis/RedisConsumerLinq/Program.cs
  - Source/Samples/Redis/RedisSchedulerConsumer/Program.cs
  - Source/Samples/Redis/RedisProducer/Program.cs
  - Source/Samples/Redis/RedisProducerLinq/Program.cs
tdd: false
---

# Plan 1.1: Redis Transport -- NuGet Bump, History Removal, and EnableHistory Addition

## Context

Phase 1 removed `IHistoryConfiguration` from SampleShared. Phase 2 propagates the 0.9.11
upgrade to each transport. This plan handles all Redis projects.

Redis is unique among transports because it requires an additional change: setting
`RedisBaseTransportOptions.EnableHistory` in the producer options lambda. The other four
transports do not need this -- their queue-creation options are handled differently. Redis
already imports `DotNetWorkQueue.Transport.Redis.Basic` in both producer files.

## Dependencies

Phase 1 Plan 1.1 (SampleShared must be rebuilt against 0.9.11 first).

## Tasks

<task id="1" files="Source/Samples/Redis/RedisConsumer/RedisConsumer.csproj, Source/Samples/Redis/RedisConsumerAsync/RedisConsumerAsync.csproj, Source/Samples/Redis/RedisConsumerLinq/RedisConsumerLinq.csproj, Source/Samples/Redis/RedisProducer/RedisProducer.csproj, Source/Samples/Redis/RedisProducerLinq/RedisProducerLinq.csproj, Source/Samples/Redis/RedisScheduler/RedisScheduler.csproj, Source/Samples/Redis/RedisSchedulerConsumer/RedisSchedulerConsumer.csproj" tdd="false">
  <action>
    In all 7 Redis .csproj files, find-and-replace the version string for every
    DotNetWorkQueue.* PackageReference from `0.9.10` to `0.9.11`. Each file contains
    these DotNetWorkQueue packages (all at 0.9.10):

    - `DotNetWorkQueue` (unconditional ItemGroup) -- all 7 files
    - `DotNetWorkQueue.Transport.Redis` (unconditional ItemGroup) -- all 7 files
    - `DotNetWorkQueue.Dashboard.Client` (net8.0-conditional ItemGroup) -- all 7 files

    Do NOT change version numbers for any non-DotNetWorkQueue packages (Microsoft.*,
    OpenTelemetry, Serilog, etc.).

    Affected lines per file (approximate):
    - RedisConsumer.csproj: lines 14-15, 66
    - RedisConsumerAsync.csproj: lines 18-19, 70
    - RedisConsumerLinq.csproj: lines 18-19, 70
    - RedisProducer.csproj: lines 18-19, 85
    - RedisProducerLinq.csproj: lines 18-19, 85
    - RedisScheduler.csproj: lines 18-19, 85
    - RedisSchedulerConsumer.csproj: lines 18-19, 70
  </action>
  <verify>
    cd "F:/Git/DotNetWorkQueue.Samples" && grep -r "DotNetWorkQueue.*0\.9\.10" Source/Samples/Redis/ ; echo "EXIT:$?"
  </verify>
  <done>
    1. grep returns no matches (no DotNetWorkQueue package at 0.9.10 remains in any Redis .csproj).
    2. grep for "DotNetWorkQueue.*0\.9\.11" returns exactly 21 matches (3 packages x 7 files).
  </done>
</task>

<task id="2" files="Source/Samples/Redis/RedisConsumer/Program.cs, Source/Samples/Redis/RedisConsumerAsync/Program.cs, Source/Samples/Redis/RedisConsumerLinq/Program.cs, Source/Samples/Redis/RedisSchedulerConsumer/Program.cs" tdd="false">
  <action>
    Delete the `queue.Configuration.History.Enabled = SharedConfiguration.EnableHistory;`
    line from 4 Redis consumer/scheduler-consumer Program.cs files. Delete only the single
    line; do not leave a double-blank-line gap (collapse to one blank line if the deletion
    would create two consecutive blank lines).

    Exact lines to delete:
    - RedisConsumer/Program.cs line 61: `queue.Configuration.History.Enabled = SharedConfiguration.EnableHistory;`
    - RedisConsumerAsync/Program.cs line 71: `queue.Configuration.History.Enabled = SharedConfiguration.EnableHistory;`
    - RedisConsumerLinq/Program.cs line 75: `queue.Configuration.History.Enabled = SharedConfiguration.EnableHistory;`
    - RedisSchedulerConsumer/Program.cs line 71: `queue.Configuration.History.Enabled = SharedConfiguration.EnableHistory;`
  </action>
  <verify>
    cd "F:/Git/DotNetWorkQueue.Samples" && grep -r "History\.Enabled" Source/Samples/Redis/ ; echo "EXIT:$?"
  </verify>
  <done>
    1. grep returns no matches -- no History.Enabled reference remains in any Redis source file.
    2. No double-blank-line artifacts introduced.
  </done>
</task>

<task id="3" files="Source/Samples/Redis/RedisProducer/Program.cs, Source/Samples/Redis/RedisProducerLinq/Program.cs" tdd="false">
  <action>
    In both Redis producer Program.cs files, convert the options lambda from an
    expression lambda to a block lambda that also sets EnableHistory. Both files
    already have `using DotNetWorkQueue.Transport.Redis.Basic;` (line 7) so no new
    using directive is needed.

    **RedisProducer/Program.cs** (line 32):
    Change:
        , options => Injectors.SetOptions(options, SharedConfiguration.EnableChaos)))
    To:
        , options => {
            Injectors.SetOptions(options, SharedConfiguration.EnableChaos);
            options.GetInstance<RedisBaseTransportOptions>().EnableHistory = SharedConfiguration.EnableHistory;
        }))

    **RedisProducerLinq/Program.cs** (line 36):
    Change:
        , options => Injectors.SetOptions(options, SharedConfiguration.EnableChaos)))
    To:
        , options => {
            Injectors.SetOptions(options, SharedConfiguration.EnableChaos);
            options.GetInstance<RedisBaseTransportOptions>().EnableHistory = SharedConfiguration.EnableHistory;
        }))
  </action>
  <verify>
    cd "F:/Git/DotNetWorkQueue.Samples" && dotnet restore "Source/Samples/Redis/Samples.sln" && dotnet build "Source/Samples/Redis/Samples.sln" -c Debug
  </verify>
  <done>
    1. `dotnet build` succeeds with 0 errors for all 7 Redis projects (both net8.0 and net48).
    2. Both RedisProducer/Program.cs and RedisProducerLinq/Program.cs contain `RedisBaseTransportOptions().EnableHistory`.
    3. grep for "History" in Redis producer files returns only the new EnableHistory lines.
  </done>
</task>

## Verification

```bash
# 1. No 0.9.10 DotNetWorkQueue references remain
grep -r "DotNetWorkQueue.*0\.9\.10" Source/Samples/Redis/
# Expected: no output

# 2. Correct count of 0.9.11 references
grep -c "0\.9\.11" Source/Samples/Redis/**/*.csproj | grep -v ":0$"
# Expected: 3 matches per file, 7 files = 21 total

# 3. No History.Enabled in consumers
grep -r "History\.Enabled" Source/Samples/Redis/
# Expected: no output

# 4. EnableHistory present in producers
grep -r "EnableHistory" Source/Samples/Redis/
# Expected: 2 matches (RedisProducer/Program.cs and RedisProducerLinq/Program.cs)

# 5. Full build
dotnet restore "Source/Samples/Redis/Samples.sln"
dotnet build "Source/Samples/Redis/Samples.sln" -c Debug
# Expected: Build succeeded, 0 errors
```
