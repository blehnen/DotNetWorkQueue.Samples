# Documentation Report
**Phase:** 1 — Remove IHistoryConfiguration from SetOptions()
**Date:** 2026-03-26

## Summary
- API/Code docs: 0 files require new documentation (internal-only change)
- Architecture updates: 0 sections affected
- User-facing docs: 2 gaps identified (CLAUDE.md and CHANGELOG.md)

## API Documentation

### `Injectors.SetOptions` (`Source/Samples/SampleShared/Injectors.cs`)
- **Public interfaces:** 1 (`SetOptions`)
- **Documentation status:** No change needed

The method has no doc comment and does not need one — it is a thin internal wiring helper, not a public API surface. The removal of 3 lines is self-evident from the code. No inline comments were added or removed.

## Architecture Updates

None. The removal of `IHistoryConfiguration` from `SetOptions()` does not change any component boundary, data flow, or DI graph that is documented.

## User Documentation

No user-facing feature changed behavior. `EnableHistory` in `App.config` is still read by `SharedConfiguration` and appears in the startup summary line (`AllSettings`). The only change is that the value is no longer forwarded to `IHistoryConfiguration` at queue-creation time, because that interface was removed from DotNetWorkQueue 0.9.11.

## Gaps

### 1. CLAUDE.md — Configuration table is missing `EnableHistory`

**File:** `CLAUDE.md`, section "Configuration", bullet for `App.config`

The current text lists:

> `EnableTrace`, `EnableMetrics`, `EnableCompression`, `EnableEncryption`, `EnableChaos`

`EnableHistory` and `EnableDashboard` are also present in every `App.config` and are read by `SharedConfiguration`, but are omitted from this list. This predates Phase 1 and is not caused by it, but Phase 1 makes the omission more notable: `EnableHistory` is now a setting that exists in config with no runtime effect, which is worth calling out to anyone reading the project for the first time.

Recommended fix — update the `App.config` toggle list in CLAUDE.md to:

> `EnableTrace`, `EnableMetrics`, `EnableCompression`, `EnableEncryption`, `EnableChaos`, `EnableDashboard`, `EnableHistory`

And add a note that `EnableHistory` is present for forward-compatibility but has no effect in 0.9.11+ (the `IHistoryConfiguration` API was removed upstream).

### 2. CHANGELOG.md — 0.9.10 entry documents `EnableHistory` as wired; 0.9.11 should note the reversal

**File:** `CHANGELOG.md`, line 5

The 0.9.10 entry states:

> Add message history support: `EnableHistory` setting in App.config, queue creation options, and consumer/producer configuration

Phase 1 removes the queue creation wiring. A 0.9.11 changelog entry should record this so the history is accurate:

> Remove `IHistoryConfiguration` wiring from `Injectors.SetOptions()` — the interface was dropped in DotNetWorkQueue 0.9.11. The `EnableHistory` key remains in `App.config` and `SharedConfiguration` but has no runtime effect.

## Recommendations

1. Apply the two gap fixes above in the documentation phase of the 0.9.11 milestone, not as part of Phase 1 (which is a code-only change).
2. Consider whether `EnableHistory` should be removed from all 36 `App.config` files and from `SharedConfiguration.cs` in a follow-on phase, to avoid confusing future readers. This is a code decision, not a documentation one — flagging here for the architect's awareness.
