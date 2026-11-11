# Changelog

## [0.2.0] - 2026-11-11

### Added

- First release. `DataMemoryStore`: a durable `IMemoryStore` over HoneyDrunk.Data's Entity Framework layer, registered with `AddMemoryDataProvider`. It passes the same store contract as the in-memory provider, on SQLite and on SQL Server.
- A summary and the supersession of its sources are one transaction, with the sources claimed by one conditional update.
- A context configured with sensitive data logging is refused, so memory content cannot reach logs through Entity Framework.

### Schema

- First version of the `memory` schema: `memory.Entry`, `memory.EntryTag` and `memory.EntrySummarySource`. The SQL Server scripts are in the package's `sql` folder; the host's database project carries them. No migrations are shipped.
