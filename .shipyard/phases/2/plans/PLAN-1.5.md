---
phase: update-transport-projects
plan: "1.5"
wave: 1
dependencies: ["1.1"]
must_haves:
  - Bump DotNetWorkQueue, DotNetWorkQueue.Transport.LiteDb, and DotNetWorkQueue.Dashboard.Client from 0.9.10 to 0.9.11 in all 8 LiteDB .csproj files
  - Remove queue/consumeQueue.Configuration.History.Enabled line from 5 LiteDB consumer Program.cs files
files_touched:
  - Source/Samples/LiteDb/LiteDbConsumer/LiteDbConsumer.csproj
  - Source/Samples/LiteDb/LiteDbConsumerAsync/LiteDbConsumerAsync.csproj
  - Source/Samples/LiteDb/LiteDbConsumerLinq/LiteDbConsumerLinq.csproj
  - Source/Samples/LiteDb/LiteDbProducer/LiteDbProducer.csproj
  - Source/Samples/LiteDb/LiteDbProducerConsumer/LiteDbProducerConsumer.csproj
  - Source/Samples/LiteDb/LiteDbProducerLinq/LiteDbProducerLinq.csproj
  - Source/Samples/LiteDb/LiteDbScheduler/LiteDbScheduler.csproj
  - Source/Samples/LiteDb/LiteDbSchedulerConsumer/LiteDbSchedulerConsumer.csproj
  - Source/Samples/LiteDb/LiteDbConsumer/Program.cs
  - Source/Samples/LiteDb/LiteDbConsumerAsync/Program.cs
  - Source/Samples/LiteDb/LiteDbConsumerLinq/Program.cs
  - Source/Samples/LiteDb/LiteDbSchedulerConsumer/Program.cs
  - Source/Samples/LiteDb/LiteDbProducerConsumer/Program.cs
tdd: false
---

# Plan 1.5: LiteDB Transport -- NuGet Bump and History Removal

## Context

Phase 1 removed `IHistoryConfiguration` from SampleShared. This plan upgrades all 8 LiteDB
sample projects to DotNetWorkQueue 0.9.11 and removes the now-invalid `History.Enabled`
configuration line from consumer-side Program.cs files.

LiteDB is unique among transports in two ways:
1. It has **8 projects** (not 7) because it includes a `LiteDbProducerConsumer` project
   that combines both producer and consumer in one executable.
2. The `LiteDbProducerConsumer/Program.cs` file uses `consumeQueue` (not `queue`) as the
   variable name for the History.Enabled line.

## Dependencies

Phase 1 Plan 1.1 (SampleShared must be rebuilt against 0.9.11 first).

## Tasks

<task id="1" files="Source/Samples/LiteDb/LiteDbConsumer/LiteDbConsumer.csproj, Source/Samples/LiteDb/LiteDbConsumerAsync/LiteDbConsumerAsync.csproj, Source/Samples/LiteDb/LiteDbConsumerLinq/LiteDbConsumerLinq.csproj, Source/Samples/LiteDb/LiteDbProducer/LiteDbProducer.csproj, Source/Samples/LiteDb/LiteDbProducerConsumer/LiteDbProducerConsumer.csproj, Source/Samples/LiteDb/LiteDbProducerLinq/LiteDbProducerLinq.csproj, Source/Samples/LiteDb/LiteDbScheduler/LiteDbScheduler.csproj, Source/Samples/LiteDb/LiteDbSchedulerConsumer/LiteDbSchedulerConsumer.csproj" tdd="false">
  <action>
    In all 8 LiteDB .csproj files, find-and-replace the version string for every
    DotNetWorkQueue.* PackageReference from `0.9.10` to `0.9.11`. Each file contains
    these DotNetWorkQueue packages:

    - `DotNetWorkQueue` (unconditional ItemGroup) -- all 8 files
    - `DotNetWorkQueue.Transport.LiteDb` (unconditional ItemGroup) -- all 8 files
    - `DotNetWorkQueue.Dashboard.Client` (net8.0-conditional ItemGroup) -- all 8 files

    Do NOT change version numbers for any non-DotNetWorkQueue packages (LiteDB v5.0.21,
    Microsoft.*, etc.).
  </action>
  <verify>
    cd "F:/Git/DotNetWorkQueue.Samples" && grep -r "DotNetWorkQueue.*0\.9\.10" Source/Samples/LiteDb/ ; echo "EXIT:$?"
  </verify>
  <done>
    1. grep returns no matches (no DotNetWorkQueue package at 0.9.10 remains in any LiteDB .csproj).
    2. grep for "DotNetWorkQueue.*0\.9\.11" returns exactly 24 matches (3 packages x 8 files).
  </done>
</task>

<task id="2" files="Source/Samples/LiteDb/LiteDbConsumer/Program.cs, Source/Samples/LiteDb/LiteDbConsumerAsync/Program.cs, Source/Samples/LiteDb/LiteDbConsumerLinq/Program.cs, Source/Samples/LiteDb/LiteDbSchedulerConsumer/Program.cs, Source/Samples/LiteDb/LiteDbProducerConsumer/Program.cs" tdd="false">
  <action>
    Delete the History.Enabled configuration line from 5 LiteDB Program.cs files. Delete
    only the single line each time; do not leave a double-blank-line gap.

    Exact lines to delete:
    - LiteDbConsumer/Program.cs line 67: `queue.Configuration.History.Enabled = SharedConfiguration.EnableHistory;`
    - LiteDbConsumerAsync/Program.cs line 89: `queue.Configuration.History.Enabled = SharedConfiguration.EnableHistory;`
    - LiteDbConsumerLinq/Program.cs line 95: `queue.Configuration.History.Enabled = SharedConfiguration.EnableHistory;`
    - LiteDbSchedulerConsumer/Program.cs line 88: `queue.Configuration.History.Enabled = SharedConfiguration.EnableHistory;`
    - LiteDbProducerConsumer/Program.cs line 127: `consumeQueue.Configuration.History.Enabled = SharedConfiguration.EnableHistory;`

    NOTE: The LiteDbProducerConsumer file uses `consumeQueue` not `queue` as the variable
    name. The line to delete is:
    `consumeQueue.Configuration.History.Enabled = SharedConfiguration.EnableHistory;`
  </action>
  <verify>
    cd "F:/Git/DotNetWorkQueue.Samples" && grep -r "History\.Enabled" Source/Samples/LiteDb/ ; echo "EXIT:$?"
  </verify>
  <done>
    1. grep returns no matches -- no History.Enabled reference remains in any LiteDB source file.
    2. No double-blank-line artifacts introduced.
  </done>
</task>

<task id="3" files="Source/Samples/LiteDb/LiteDbConsumer/LiteDbConsumer.csproj, Source/Samples/LiteDb/LiteDbConsumerAsync/LiteDbConsumerAsync.csproj, Source/Samples/LiteDb/LiteDbConsumerLinq/LiteDbConsumerLinq.csproj, Source/Samples/LiteDb/LiteDbProducer/LiteDbProducer.csproj, Source/Samples/LiteDb/LiteDbProducerConsumer/LiteDbProducerConsumer.csproj, Source/Samples/LiteDb/LiteDbProducerLinq/LiteDbProducerLinq.csproj, Source/Samples/LiteDb/LiteDbScheduler/LiteDbScheduler.csproj, Source/Samples/LiteDb/LiteDbSchedulerConsumer/LiteDbSchedulerConsumer.csproj" tdd="false">
  <action>
    Build the entire LiteDB solution to confirm all changes compile correctly.
    This is a verification-only task -- no code changes.
  </action>
  <verify>
    cd "F:/Git/DotNetWorkQueue.Samples" && dotnet restore "Source/Samples/LiteDb/Samples.sln" && dotnet build "Source/Samples/LiteDb/Samples.sln" -c Debug
  </verify>
  <done>
    1. `dotnet build` succeeds with 0 errors for all 8 LiteDB projects (both net8.0 and net48).
  </done>
</task>

## Verification

```bash
# 1. No 0.9.10 DotNetWorkQueue references remain
grep -r "DotNetWorkQueue.*0\.9\.10" Source/Samples/LiteDb/
# Expected: no output

# 2. No History.Enabled in any LiteDB file
grep -r "History\.Enabled" Source/Samples/LiteDb/
# Expected: no output

# 3. Full build
dotnet restore "Source/Samples/LiteDb/Samples.sln"
dotnet build "Source/Samples/LiteDb/Samples.sln" -c Debug
# Expected: Build succeeded, 0 errors
```
