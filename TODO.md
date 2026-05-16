# TODO

## Add outbox-pattern variants for SQL Server and PostgreSQL samples

DotNetWorkQueue 0.9.36 shipped the transactional outbox pattern on the SqlServer and PostgreSQL transports via the new `IRelationalProducerQueue<T>` capability cast. See:

- [docs/outbox-pattern.md](https://github.com/blehnen/DotNetWorkQueue/blob/master/docs/outbox-pattern.md) — canonical reference (lifecycle contract, retry contract, DB-name validation, schema deployment)
- [Outbox Pattern wiki](https://github.com/blehnen/DotNetWorkQueue/wiki/OutboxPattern) — short-form discovery page

The existing `Source/Samples/SQLServer/Producer` and `Source/Samples/PostgreSQL/Producer` samples use the standard fire-and-forget `IProducerQueue<T>` surface. They need to be extended (or a parallel sample added) so that running them exercises the capability-cast + caller-supplied `DbTransaction` path end-to-end against a real database. Treat this as a **live sanity check** for the 0.9.36 feature, not just a code example.

### Suggested shape

- One additional sample project per transport (e.g. `ProducerOutbox`), or a runtime flag on the existing Producer sample that switches modes.
- The outbox flow should: open a `SqlConnection` / `NpgsqlConnection`, begin a transaction, write a business row to a sample table, capability-cast the producer to `IRelationalProducerQueue<OrderCreatedEvent>`, `Send(msg, transaction)`, then commit. Demonstrate the rollback path too — a transaction.Rollback() after a successful Send must leave the queue empty.
- Add an integration test in `Source/Samples/IntegrationTests` covering both commit and rollback. SQL Server and PostgreSQL tests are local-only here (no external service in CI), so this won't run on GitHub Actions but will catch regressions on the developer's box.
- Update `README.md` (Samples table) and `CHANGELOG.md` when the work lands.

### Why this matters

The library's own integration tests cover the outbox path against real SQL Server and PostgreSQL instances (24 tests in PR #138, Jenkins-validated). The samples repo is the user-facing surface — without a working outbox sample, the only way for a downstream user to validate the feature against their schema is to read the docs and assemble it themselves. The samples close that gap.
