---
phase: update-transport-projects
plan: "1.2"
wave: 1
dependencies: ["1.1"]
must_haves:
  - Bump DotNetWorkQueue, DotNetWorkQueue.Transport.SqlServer, and DotNetWorkQueue.Dashboard.Client from 0.9.10 to 0.9.11 in all 7 SQL Server .csproj files
  - Remove queue.Configuration.History.Enabled line from 4 SQL Server consumer Program.cs files
files_touched:
  - Source/Samples/SQLServer/SQLServerConsumer/SQLServerConsumer.csproj
  - Source/Samples/SQLServer/SQLServerConsumerAsync/SQLServerConsumerAsync.csproj
  - Source/Samples/SQLServer/SQLServerConsumerLinq/SQLServerConsumerLinq.csproj
  - Source/Samples/SQLServer/SQLServerProducer/SQLServerProducer.csproj
  - Source/Samples/SQLServer/SQLServerProducerLinq/SQLServerProducerLinq.csproj
  - Source/Samples/SQLServer/SQLServerScheduler/SQLServerScheduler.csproj
  - Source/Samples/SQLServer/SQLServerSchedulerConsumer/SQLServerSchedulerConsumer.csproj
  - Source/Samples/SQLServer/SQLServerConsumer/Program.cs
  - Source/Samples/SQLServer/SQLServerConsumerAsync/Program.cs
  - Source/Samples/SQLServer/SQLServerConsumerLinq/Program.cs
  - Source/Samples/SQLServer/SQLServerSchedulerConsumer/Program.cs
tdd: false
---

# Plan 1.2: SQL Server Transport -- NuGet Bump and History Removal

## Context

Phase 1 removed `IHistoryConfiguration` from SampleShared. This plan upgrades all 7 SQL
Server sample projects to DotNetWorkQueue 0.9.11 and removes the now-invalid
`History.Enabled` configuration line from consumer-side Program.cs files.

SQL Server does not need an EnableHistory addition to producers (unlike Redis). History for
SQL-based transports is controlled at queue-creation time via the transport options that are
already wired in SampleShared.

## Dependencies

Phase 1 Plan 1.1 (SampleShared must be rebuilt against 0.9.11 first).

## Tasks

<task id="1" files="Source/Samples/SQLServer/SQLServerConsumer/SQLServerConsumer.csproj, Source/Samples/SQLServer/SQLServerConsumerAsync/SQLServerConsumerAsync.csproj, Source/Samples/SQLServer/SQLServerConsumerLinq/SQLServerConsumerLinq.csproj, Source/Samples/SQLServer/SQLServerProducer/SQLServerProducer.csproj, Source/Samples/SQLServer/SQLServerProducerLinq/SQLServerProducerLinq.csproj, Source/Samples/SQLServer/SQLServerScheduler/SQLServerScheduler.csproj, Source/Samples/SQLServer/SQLServerSchedulerConsumer/SQLServerSchedulerConsumer.csproj" tdd="false">
  <action>
    In all 7 SQL Server .csproj files, find-and-replace the version string for every
    DotNetWorkQueue.* PackageReference from `0.9.10` to `0.9.11`. Each file contains
    these DotNetWorkQueue packages:

    - `DotNetWorkQueue` (unconditional ItemGroup) -- all 7 files
    - `DotNetWorkQueue.Transport.SqlServer` (unconditional ItemGroup) -- all 7 files
    - `DotNetWorkQueue.Dashboard.Client` (net8.0-conditional ItemGroup) -- all 7 files

    Do NOT change version numbers for any non-DotNetWorkQueue packages.
  </action>
  <verify>
    cd "F:/Git/DotNetWorkQueue.Samples" && grep -r "DotNetWorkQueue.*0\.9\.10" Source/Samples/SQLServer/ ; echo "EXIT:$?"
  </verify>
  <done>
    1. grep returns no matches (no DotNetWorkQueue package at 0.9.10 remains in any SQL Server .csproj).
    2. grep for "DotNetWorkQueue.*0\.9\.11" returns exactly 21 matches (3 packages x 7 files).
  </done>
</task>

<task id="2" files="Source/Samples/SQLServer/SQLServerConsumer/Program.cs, Source/Samples/SQLServer/SQLServerConsumerAsync/Program.cs, Source/Samples/SQLServer/SQLServerConsumerLinq/Program.cs, Source/Samples/SQLServer/SQLServerSchedulerConsumer/Program.cs" tdd="false">
  <action>
    Delete the `queue.Configuration.History.Enabled = SharedConfiguration.EnableHistory;`
    line from 4 SQL Server consumer/scheduler-consumer Program.cs files. Delete only the
    single line; do not leave a double-blank-line gap.

    Exact lines to delete:
    - SQLServerConsumer/Program.cs line 79: `queue.Configuration.History.Enabled = SharedConfiguration.EnableHistory;`
    - SQLServerConsumerAsync/Program.cs line 89: `queue.Configuration.History.Enabled = SharedConfiguration.EnableHistory;`
    - SQLServerConsumerLinq/Program.cs line 94: `queue.Configuration.History.Enabled = SharedConfiguration.EnableHistory;`
    - SQLServerSchedulerConsumer/Program.cs line 93: `queue.Configuration.History.Enabled = SharedConfiguration.EnableHistory;`
  </action>
  <verify>
    cd "F:/Git/DotNetWorkQueue.Samples" && grep -r "History\.Enabled" Source/Samples/SQLServer/ ; echo "EXIT:$?"
  </verify>
  <done>
    1. grep returns no matches -- no History.Enabled reference remains in any SQL Server source file.
    2. No double-blank-line artifacts introduced.
  </done>
</task>

<task id="3" files="Source/Samples/SQLServer/SQLServerConsumer/SQLServerConsumer.csproj, Source/Samples/SQLServer/SQLServerConsumerAsync/SQLServerConsumerAsync.csproj, Source/Samples/SQLServer/SQLServerConsumerLinq/SQLServerConsumerLinq.csproj, Source/Samples/SQLServer/SQLServerProducer/SQLServerProducer.csproj, Source/Samples/SQLServer/SQLServerProducerLinq/SQLServerProducerLinq.csproj, Source/Samples/SQLServer/SQLServerScheduler/SQLServerScheduler.csproj, Source/Samples/SQLServer/SQLServerSchedulerConsumer/SQLServerSchedulerConsumer.csproj" tdd="false">
  <action>
    Build the entire SQL Server solution to confirm all changes compile correctly.
    This is a verification-only task -- no code changes.
  </action>
  <verify>
    cd "F:/Git/DotNetWorkQueue.Samples" && dotnet restore "Source/Samples/SQLServer/Samples.sln" && dotnet build "Source/Samples/SQLServer/Samples.sln" -c Debug
  </verify>
  <done>
    1. `dotnet build` succeeds with 0 errors for all 7 SQL Server projects (both net8.0 and net48).
  </done>
</task>

## Verification

```bash
# 1. No 0.9.10 DotNetWorkQueue references remain
grep -r "DotNetWorkQueue.*0\.9\.10" Source/Samples/SQLServer/
# Expected: no output

# 2. No History.Enabled in any SQL Server file
grep -r "History\.Enabled" Source/Samples/SQLServer/
# Expected: no output

# 3. Full build
dotnet restore "Source/Samples/SQLServer/Samples.sln"
dotnet build "Source/Samples/SQLServer/Samples.sln" -c Debug
# Expected: Build succeeded, 0 errors
```
