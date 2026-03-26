---
phase: update-transport-projects
plan: "1.3"
wave: 1
dependencies: ["1.1"]
must_haves:
  - Bump DotNetWorkQueue, DotNetWorkQueue.Transport.PostgreSQL, and DotNetWorkQueue.Dashboard.Client from 0.9.10 to 0.9.11 in all 7 PostgreSQL .csproj files
  - Remove queue.Configuration.History.Enabled line from 4 PostgreSQL consumer Program.cs files
files_touched:
  - Source/Samples/PostgreSQL/PostGreSQLConsumer/PostGreSQLConsumer.csproj
  - Source/Samples/PostgreSQL/PostGreSQLConsumerAsync/PostGreSQLConsumerAsync.csproj
  - Source/Samples/PostgreSQL/PostGreSQLConsumerLinq/PostGreSQLConsumerLinq.csproj
  - Source/Samples/PostgreSQL/PostgreSQLProducer/PostgreSQLProducer.csproj
  - Source/Samples/PostgreSQL/PostgreSQLProducerLinq/PostgreSQLProducerLinq.csproj
  - Source/Samples/PostgreSQL/PostGreSQLScheduler/PostGreSQLScheduler.csproj
  - Source/Samples/PostgreSQL/PostGreSQLSchedulerConsumer/PostGreSQLSchedulerConsumer.csproj
  - Source/Samples/PostgreSQL/PostGreSQLConsumer/Program.cs
  - Source/Samples/PostgreSQL/PostGreSQLConsumerAsync/Program.cs
  - Source/Samples/PostgreSQL/PostGreSQLConsumerLinq/Program.cs
  - Source/Samples/PostgreSQL/PostGreSQLSchedulerConsumer/Program.cs
tdd: false
---

# Plan 1.3: PostgreSQL Transport -- NuGet Bump and History Removal

## Context

Phase 1 removed `IHistoryConfiguration` from SampleShared. This plan upgrades all 7
PostgreSQL sample projects to DotNetWorkQueue 0.9.11 and removes the now-invalid
`History.Enabled` configuration line from consumer-side Program.cs files.

Note the mixed casing in PostgreSQL folder/project names: folders use `PostGreSQL` for
consumers and schedulers but `PostgreSQL` for producers. Be careful with paths.

## Dependencies

Phase 1 Plan 1.1 (SampleShared must be rebuilt against 0.9.11 first).

## Tasks

<task id="1" files="Source/Samples/PostgreSQL/PostGreSQLConsumer/PostGreSQLConsumer.csproj, Source/Samples/PostgreSQL/PostGreSQLConsumerAsync/PostGreSQLConsumerAsync.csproj, Source/Samples/PostgreSQL/PostGreSQLConsumerLinq/PostGreSQLConsumerLinq.csproj, Source/Samples/PostgreSQL/PostgreSQLProducer/PostgreSQLProducer.csproj, Source/Samples/PostgreSQL/PostgreSQLProducerLinq/PostgreSQLProducerLinq.csproj, Source/Samples/PostgreSQL/PostGreSQLScheduler/PostGreSQLScheduler.csproj, Source/Samples/PostgreSQL/PostGreSQLSchedulerConsumer/PostGreSQLSchedulerConsumer.csproj" tdd="false">
  <action>
    In all 7 PostgreSQL .csproj files, find-and-replace the version string for every
    DotNetWorkQueue.* PackageReference from `0.9.10` to `0.9.11`. Each file contains
    these DotNetWorkQueue packages:

    - `DotNetWorkQueue` (unconditional ItemGroup) -- all 7 files
    - `DotNetWorkQueue.Transport.PostgreSQL` (unconditional ItemGroup) -- all 7 files
    - `DotNetWorkQueue.Dashboard.Client` (net8.0-conditional ItemGroup) -- all 7 files

    Do NOT change version numbers for any non-DotNetWorkQueue packages.
  </action>
  <verify>
    cd "F:/Git/DotNetWorkQueue.Samples" && grep -r "DotNetWorkQueue.*0\.9\.10" Source/Samples/PostgreSQL/ ; echo "EXIT:$?"
  </verify>
  <done>
    1. grep returns no matches (no DotNetWorkQueue package at 0.9.10 remains in any PostgreSQL .csproj).
    2. grep for "DotNetWorkQueue.*0\.9\.11" returns exactly 21 matches (3 packages x 7 files).
  </done>
</task>

<task id="2" files="Source/Samples/PostgreSQL/PostGreSQLConsumer/Program.cs, Source/Samples/PostgreSQL/PostGreSQLConsumerAsync/Program.cs, Source/Samples/PostgreSQL/PostGreSQLConsumerLinq/Program.cs, Source/Samples/PostgreSQL/PostGreSQLSchedulerConsumer/Program.cs" tdd="false">
  <action>
    Delete the `queue.Configuration.History.Enabled = SharedConfiguration.EnableHistory;`
    line from 4 PostgreSQL consumer/scheduler-consumer Program.cs files. Delete only the
    single line; do not leave a double-blank-line gap.

    Exact lines to delete:
    - PostGreSQLConsumer/Program.cs line 66: `queue.Configuration.History.Enabled = SharedConfiguration.EnableHistory;`
    - PostGreSQLConsumerAsync/Program.cs line 89: `queue.Configuration.History.Enabled = SharedConfiguration.EnableHistory;`
    - PostGreSQLConsumerLinq/Program.cs line 94: `queue.Configuration.History.Enabled = SharedConfiguration.EnableHistory;`
    - PostGreSQLSchedulerConsumer/Program.cs line 90: `queue.Configuration.History.Enabled = SharedConfiguration.EnableHistory;`
  </action>
  <verify>
    cd "F:/Git/DotNetWorkQueue.Samples" && grep -r "History\.Enabled" Source/Samples/PostgreSQL/ ; echo "EXIT:$?"
  </verify>
  <done>
    1. grep returns no matches -- no History.Enabled reference remains in any PostgreSQL source file.
    2. No double-blank-line artifacts introduced.
  </done>
</task>

<task id="3" files="Source/Samples/PostgreSQL/PostGreSQLConsumer/PostGreSQLConsumer.csproj, Source/Samples/PostgreSQL/PostGreSQLConsumerAsync/PostGreSQLConsumerAsync.csproj, Source/Samples/PostgreSQL/PostGreSQLConsumerLinq/PostGreSQLConsumerLinq.csproj, Source/Samples/PostgreSQL/PostgreSQLProducer/PostgreSQLProducer.csproj, Source/Samples/PostgreSQL/PostgreSQLProducerLinq/PostgreSQLProducerLinq.csproj, Source/Samples/PostgreSQL/PostGreSQLScheduler/PostGreSQLScheduler.csproj, Source/Samples/PostgreSQL/PostGreSQLSchedulerConsumer/PostGreSQLSchedulerConsumer.csproj" tdd="false">
  <action>
    Build the entire PostgreSQL solution to confirm all changes compile correctly.
    This is a verification-only task -- no code changes.
  </action>
  <verify>
    cd "F:/Git/DotNetWorkQueue.Samples" && dotnet restore "Source/Samples/PostgreSQL/Samples.sln" && dotnet build "Source/Samples/PostgreSQL/Samples.sln" -c Debug
  </verify>
  <done>
    1. `dotnet build` succeeds with 0 errors for all 7 PostgreSQL projects (both net8.0 and net48).
  </done>
</task>

## Verification

```bash
# 1. No 0.9.10 DotNetWorkQueue references remain
grep -r "DotNetWorkQueue.*0\.9\.10" Source/Samples/PostgreSQL/
# Expected: no output

# 2. No History.Enabled in any PostgreSQL file
grep -r "History\.Enabled" Source/Samples/PostgreSQL/
# Expected: no output

# 3. Full build
dotnet restore "Source/Samples/PostgreSQL/Samples.sln"
dotnet build "Source/Samples/PostgreSQL/Samples.sln" -c Debug
# Expected: Build succeeded, 0 errors
```
