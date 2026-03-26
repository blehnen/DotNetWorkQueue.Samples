---
phase: fix-sampleshared-foundation
plan: "1.1"
wave: 1
dependencies: []
must_haves:
  - Remove IHistoryConfiguration usage from Injectors.SetOptions()
  - SampleShared builds cleanly for both net8.0 and net48
files_touched:
  - Source/Samples/SampleShared/Injectors.cs
tdd: false
---

# Plan 1.1: Remove IHistoryConfiguration from SampleShared

## Context

DotNetWorkQueue 0.9.11 removed `IHistoryConfiguration` from the public API. History
is now a queue-creation option, not a runtime toggle. The SampleShared library
references this deleted interface in `Injectors.SetOptions()`, which causes a build
failure against the 0.9.11 package (already pinned in SampleShared.csproj). Removing
these two lines unblocks SampleShared compilation and, by extension, every downstream
transport sample.

No replacement code is needed. `SharedConfiguration.EnableHistory` is intentionally
retained -- producers will read it in Phase 2 when wiring queue-creation options.

## Dependencies

None. This is the first plan in Phase 1 and has no prerequisites.

## Tasks

<task id="1" files="Source/Samples/SampleShared/Injectors.cs" tdd="false">
  <action>
    In Injectors.cs, delete lines 64-65 of the SetOptions method:

        var history = container.GetInstance<IHistoryConfiguration>();
        history.Enabled = SharedConfiguration.EnableHistory;

    Also delete the blank line (line 63) that precedes them so the method body is
    clean. The resulting SetOptions method should be:

        public static void SetOptions(IContainer container, bool enableChaos)
        {
            var pol = container.GetInstance<IPolicies>();
            pol.EnableChaos = enableChaos;
        }

    Do NOT remove the `using DotNetWorkQueue;` directive on line 5 -- it is still
    required by IPolicies, IContainer, IMetrics, and other types used throughout
    the file.
  </action>
  <verify>
    cd "F:/Git/DotNetWorkQueue.Samples" && dotnet restore "Source/Samples/SampleShared/SampleShared.sln" && dotnet build "Source/Samples/SampleShared/SampleShared.sln" -c Debug
  </verify>
  <done>
    1. SetOptions method contains exactly two statements (GetInstance of IPolicies and assignment to EnableChaos).
    2. No reference to IHistoryConfiguration exists anywhere in Source/Samples/SampleShared/.
    3. `dotnet build` succeeds with exit code 0 for both net8.0 and net48 target frameworks.
  </done>
</task>

## Verification

```bash
# 1. Confirm the offending reference is gone
grep -r "IHistoryConfiguration" Source/Samples/SampleShared/
# Expected: no output (exit code 1)

# 2. Build SampleShared for both targets
dotnet restore "Source/Samples/SampleShared/SampleShared.sln"
dotnet build "Source/Samples/SampleShared/SampleShared.sln" -c Debug
# Expected: Build succeeded with 0 errors for net8.0 and net48

# 3. Confirm output DLLs exist for both frameworks
ls Source/Samples/SampleShared/bin/Debug/net8.0/SampleShared.dll
ls Source/Samples/SampleShared/bin/Debug/net48/SampleShared.dll
# Expected: both files present
```
