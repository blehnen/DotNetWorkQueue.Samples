---
phase: update-transport-projects
plan: "1.4"
wave: 1
dependencies: ["1.1"]
must_haves:
  - Bump DotNetWorkQueue, DotNetWorkQueue.Transport.SQLite, and DotNetWorkQueue.Dashboard.Client from 0.9.10 to 0.9.11 in all 7 SQLite .csproj files
  - Remove queue.Configuration.History.Enabled line from 4 SQLite consumer Program.cs files
files_touched:
  - Source/Samples/SQLite/SQLiteConsumer/SQLiteConsumer.csproj
  - Source/Samples/SQLite/SQLiteConsumerAsync/SQLiteConsumerAsync.csproj
  - Source/Samples/SQLite/SQLiteConsumerLinq/SQLiteConsumerLinq.csproj
  - Source/Samples/SQLite/SQLiteProducer/SQLiteProducer.csproj
  - Source/Samples/SQLite/SQLiteProducerLinq/SQLiteProducerLinq.csproj
  - Source/Samples/SQLite/SQliteScheduler/SQliteScheduler.csproj
  - Source/Samples/SQLite/SQLiteSchedulerConsumer/SQLiteSchedulerConsumer.csproj
  - Source/Samples/SQLite/SQLiteConsumer/Program.cs
  - Source/Samples/SQLite/SQLiteConsumerAsync/Program.cs
  - Source/Samples/SQLite/SQLiteConsumerLinq/Program.cs
  - Source/Samples/SQLite/SQLiteSchedulerConsumer/Program.cs
tdd: false
---

# Plan 1.4: SQLite Transport -- NuGet Bump and History Removal

## Context

Phase 1 removed `IHistoryConfiguration` from SampleShared. This plan upgrades all 7 SQLite
sample projects to DotNetWorkQueue 0.9.11 and removes the now-invalid `History.Enabled`
configuration line from consumer-side Program.cs files.

Note the inconsistent casing in the scheduler folder name: `SQliteScheduler` (lowercase 'l').
Be careful with this path.

## Dependencies

Phase 1 Plan 1.1 (SampleShared must be rebuilt against 0.9.11 first).

## Tasks

<task id="1" files="Source/Samples/SQLite/SQLiteConsumer/SQLiteConsumer.csproj, Source/Samples/SQLite/SQLiteConsumerAsync/SQLiteConsumerAsync.csproj, Source/Samples/SQLite/SQLiteConsumerLinq/SQLiteConsumerLinq.csproj, Source/Samples/SQLite/SQLiteProducer/SQLiteProducer.csproj, Source/Samples/SQLite/SQLiteProducerLinq/SQLiteProducerLinq.csproj, Source/Samples/SQLite/SQliteScheduler/SQliteScheduler.csproj, Source/Samples/SQLite/SQLiteSchedulerConsumer/SQLiteSchedulerConsumer.csproj" tdd="false">
  <action>
    In all 7 SQLite .csproj files, find-and-replace the version string for every
    DotNetWorkQueue.* PackageReference from `0.9.10` to `0.9.11`. Each file contains
    these DotNetWorkQueue packages:

    - `DotNetWorkQueue` (unconditional ItemGroup) -- all 7 files
    - `DotNetWorkQueue.Transport.SQLite` (unconditional ItemGroup) -- all 7 files
    - `DotNetWorkQueue.Dashboard.Client` (net8.0-conditional ItemGroup) -- all 7 files

    Do NOT change version numbers for any non-DotNetWorkQueue packages. Watch for the
    scheduler folder name `SQliteScheduler` (lowercase 'l').
  </action>
  <verify>
    cd "F:/Git/DotNetWorkQueue.Samples" && grep -r "DotNetWorkQueue.*0\.9\.10" Source/Samples/SQLite/ ; echo "EXIT:$?"
  </verify>
  <done>
    1. grep returns no matches (no DotNetWorkQueue package at 0.9.10 remains in any SQLite .csproj).
    2. grep for "DotNetWorkQueue.*0\.9\.11" returns exactly 21 matches (3 packages x 7 files).
  </done>
</task>

<task id="2" files="Source/Samples/SQLite/SQLiteConsumer/Program.cs, Source/Samples/SQLite/SQLiteConsumerAsync/Program.cs, Source/Samples/SQLite/SQLiteConsumerLinq/Program.cs, Source/Samples/SQLite/SQLiteSchedulerConsumer/Program.cs" tdd="false">
  <action>
    Delete the `queue.Configuration.History.Enabled = SharedConfiguration.EnableHistory;`
    line from 4 SQLite consumer/scheduler-consumer Program.cs files. Delete only the
    single line; do not leave a double-blank-line gap.

    Exact lines to delete:
    - SQLiteConsumer/Program.cs line 68: `queue.Configuration.History.Enabled = SharedConfiguration.EnableHistory;`
    - SQLiteConsumerAsync/Program.cs line 90: `queue.Configuration.History.Enabled = SharedConfiguration.EnableHistory;`
    - SQLiteConsumerLinq/Program.cs line 96: `queue.Configuration.History.Enabled = SharedConfiguration.EnableHistory;`
    - SQLiteSchedulerConsumer/Program.cs line 89: `queue.Configuration.History.Enabled = SharedConfiguration.EnableHistory;`
  </action>
  <verify>
    cd "F:/Git/DotNetWorkQueue.Samples" && grep -r "History\.Enabled" Source/Samples/SQLite/ ; echo "EXIT:$?"
  </verify>
  <done>
    1. grep returns no matches -- no History.Enabled reference remains in any SQLite source file.
    2. No double-blank-line artifacts introduced.
  </done>
</task>

<task id="3" files="Source/Samples/SQLite/SQLiteConsumer/SQLiteConsumer.csproj, Source/Samples/SQLite/SQLiteConsumerAsync/SQLiteConsumerAsync.csproj, Source/Samples/SQLite/SQLiteConsumerLinq/SQLiteConsumerLinq.csproj, Source/Samples/SQLite/SQLiteProducer/SQLiteProducer.csproj, Source/Samples/SQLite/SQLiteProducerLinq/SQLiteProducerLinq.csproj, Source/Samples/SQLite/SQliteScheduler/SQliteScheduler.csproj, Source/Samples/SQLite/SQLiteSchedulerConsumer/SQLiteSchedulerConsumer.csproj" tdd="false">
  <action>
    Build the entire SQLite solution to confirm all changes compile correctly.
    This is a verification-only task -- no code changes.
  </action>
  <verify>
    cd "F:/Git/DotNetWorkQueue.Samples" && dotnet restore "Source/Samples/SQLite/Samples.sln" && dotnet build "Source/Samples/SQLite/Samples.sln" -c Debug
  </verify>
  <done>
    1. `dotnet build` succeeds with 0 errors for all 7 SQLite projects (both net8.0 and net48).
  </done>
</task>

## Verification

```bash
# 1. No 0.9.10 DotNetWorkQueue references remain
grep -r "DotNetWorkQueue.*0\.9\.10" Source/Samples/SQLite/
# Expected: no output

# 2. No History.Enabled in any SQLite file
grep -r "History\.Enabled" Source/Samples/SQLite/
# Expected: no output

# 3. Full build
dotnet restore "Source/Samples/SQLite/Samples.sln"
dotnet build "Source/Samples/SQLite/Samples.sln" -c Debug
# Expected: Build succeeded, 0 errors
```
