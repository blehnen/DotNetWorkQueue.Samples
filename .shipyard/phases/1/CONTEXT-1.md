# Phase 1 Context: Fix SampleShared

## Decisions

- **Scope:** Remove only the 2 IHistoryConfiguration lines from Injectors.SetOptions() (lines 64-65). No other SampleShared changes needed for 0.9.11.
- **Approach:** Delete the lines, don't replace them. History is now a queue-creation option, not a runtime toggle.
- **SharedConfiguration.EnableHistory:** Retained — still used by producers in Phase 2. Only its consumption via the deleted interface changes.
- **SampleShared.csproj:** Already at 0.9.11, no version change needed.
