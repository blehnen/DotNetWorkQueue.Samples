# Design: Outbox & Inbox transactional sample variants (SQL Server + PostgreSQL)

**Date:** 2026-05-28
**Source:** `TODO.md` — "Add outbox-pattern variants" and "Add inbox-pattern variants"
**Status:** Validated, ready for Shipyard init/plan

## Summary

DotNetWorkQueue 0.9.36 shipped the transactional **outbox** pattern (`IRelationalProducerQueue<T>`,
producer-side) and 0.9.37 shipped the **inbox** pattern (`IRelationalWorkerNotification`,
consumer-side) on the SqlServer and PostgreSQL relational transports. The samples repo is the
user-facing surface; without working samples a downstream user can only assemble these patterns
from the docs. This work adds dedicated sample projects + integration tests that exercise the
capability-cast + caller-supplied/library-supplied `DbTransaction` paths end-to-end against a real
database, serving as live regression checks on the user-facing API contract.

Both APIs are confirmed published in **0.9.37** (NuGet versions top out at 0.9.37).

## Decisions

- **Process:** Full Shipyard (run `/shipyard:init` to map this brownfield repo + scaffold
  `.shipyard/`, then `/shipyard:plan`). This design doc is the bridge until init runs.
- **Scope:** Both patterns this effort, **outbox first**, then inbox.
- **Sample shape:** New dedicated projects per transport (matches the existing
  7-projects-per-transport convention), not a runtime flag on existing Producer/Consumer.
- **Message type:** New `OrderCreatedEvent` message + `Orders` / `OrdersProjection` business tables.
- **Target version:** Bump all `DotNetWorkQueue.*` from 0.9.35 → **0.9.37** (latest published).

## 1. Version bump (repo-wide)

SampleShared is referenced by compiled-DLL HintPath while each project pins its own
`DotNetWorkQueue.*` packages. If SampleShared moves to 0.9.37 but a transport project stays on
0.9.35, the DLL/package types diverge and restore breaks. Therefore this is a **whole-repo** bump:
all 5 transports + SampleShared + IntegrationTests + Dashboard.Api, matching prior
"upgrade DNWQ + align transitive pins" commits.

Triggers release discipline (per CLAUDE.md):
- Dated entry in `CHANGELOG.md` (same session).
- Version strings in `CLAUDE.md` — Project Overview `v0.9.xx` and Architecture → Key Dependencies.

## 2. SampleShared additions (kept transport-agnostic)

- `OrderCreatedEvent` POCO: `OrderId (Guid)`, `Customer (string)`, `Amount (decimal)`,
  `CreatedUtc (DateTime)`, plus a `ForceRollback (bool)` flag to drive the inbox failure demo.
- A shared inbox handler that casts `IWorkerNotification` → `IRelationalWorkerNotification` and runs
  a parameterized `INSERT INTO OrdersProjection (...) VALUES (@...)` on the exposed transaction.
  `@`-named parameters work for both `Microsoft.Data.SqlClient` and `Npgsql`.
- **No** SqlClient/Npgsql dependency added to SampleShared. Business-table DDL and
  `SqlConnection`/`NpgsqlConnection` handling live in the new per-transport projects, which already
  own their transport client lib.

## 3. New projects (mirror existing per-transport layout)

### Outbox (producer-side)
- `SQLServerProducerOutbox`, `PostgreSQLProducerOutbox`
- Create queue (same options as existing Producer), then a **focused 3-item menu**:
  (a) commit path, (b) rollback path, (c) quit.
- Each path: open connection → begin `DbTransaction` → write a business `Orders` row on that tx →
  cast `IProducerQueue<OrderCreatedEvent>` → `IRelationalProducerQueue<OrderCreatedEvent>` →
  `Send(msg, transaction)` → commit (commit path) or rollback (rollback path).
- Log queue count before/after via the admin API so the rollback path visibly leaves the queue empty.

### Inbox (consumer-side)
- `SQLServerConsumerInbox`, `PostgreSQLConsumerInbox`
- Consumer configured with `EnableHoldTransactionUntilMessageCommitted = true`.
- Handler casts `IWorkerNotification` → `IRelationalWorkerNotification` and writes an
  `OrdersProjection` row on the library-supplied dequeue transaction. The queue commit and the
  business write commit atomically when the handler returns.
- `ForceRollback` messages throw inside the handler → both the dequeue and the business write roll back.

### Per-project housekeeping
- Each new project: own `App.config` (Database/QueueName), `tracesettings.json`,
  `metricsettings.json` copied to output.
- Add each `.csproj` to the transport's `Samples.sln`.
- Business tables (`Orders`, `OrdersProjection`) auto-created idempotently on startup with
  transport-specific DDL (IDENTITY/GETUTCDATE vs SERIAL/now(), bracket vs quoted identifiers).

## 4. Integration tests (`[TestCategory("LocalOnly")]` — SqlServer + PostgreSQL need a real DB, not CI)

New `SqlServerOutboxInboxTests.cs` / `PostgreSqlOutboxInboxTests.cs` plus a small DB-aware helper
(the existing `ProduceConsumeTestHelper` is generic produce/consume and does not touch business tables):

- **Outbox commit** → queue has the message AND the `Orders` row exists.
- **Outbox rollback** → queue empty AND `Orders` row absent.
- **Inbox commit** → `OrdersProjection` row present after handler completes.
- **Inbox rollback** → handler throws → `OrdersProjection` row absent (verified from a separate connection).

## 5. Docs

- `README.md` — add ProducerOutbox / ConsumerInbox rows to the Samples table for SqlServer + PostgreSQL.
- `CHANGELOG.md` — dated entry: 0.9.37 bump + new outbox/inbox samples.
- `CLAUDE.md` — version strings 0.9.35 → 0.9.37; note new project naming.
- `TODO.md` — check off / remove both completed items.

## Open items to confirm during planning (verify against restored 0.9.37 assemblies)

- Exact location where `EnableHoldTransactionUntilMessageCommitted` is set (transport options via
  `Injectors.SetOptions`, or consumer `queue.Configuration`?).
- Precise signatures: `IRelationalProducerQueue<T>.Send(msg, DbTransaction)` and
  `IRelationalWorkerNotification.Transaction` / `.Transaction.Connection`.
- Whether 0.9.37 introduces transitive pin changes that need aligning (as in the 0.9.35 bump).
