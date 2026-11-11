# Changelog

## [0.2.0] - 2026-11-11

### Added

- First release. `DataMemoryStore`: a durable `IMemoryStore` over HoneyDrunk.Data's Entity Framework layer, registered with `AddMemoryDataProvider`. It passes the same store contract as the in-memory provider, on SQLite and on SQL Server.
- A summary and the supersession of its sources are one transaction, with the sources claimed by one conditional update.
- A context configured with sensitive data logging is refused, so memory content cannot reach logs through Entity Framework.

### Schema

- First version of the `memory` schema. The SQL Server scripts are in the package's `sql` folder; the host's database project carries them. No migrations are shipped.
- `memory.Entry`: `Id`, `TenantId`, `ProjectId`, `AgentId`, `Kind`, `Content`, `CreatedAt`, `SupersededByEntryId`. `CreatedAt` holds the instant the contract calls `WrittenAt`: an entry is written once, so its write instant is its creation stamp. Index `IX_Entry_TenantId_ProjectId_AgentId_CreatedAt_Id` on `(TenantId, ProjectId, AgentId, CreatedAt DESC, Id DESC)`, including `Kind` and `SupersededByEntryId`.
- `memory.EntryTag`: `EntryId`, `Ordinal`, `Tag`, `CreatedAt`.
- `memory.EntrySummarySource`: `SummaryEntryId`, `Ordinal`, `SourceEntryId`, `CreatedAt`.
- Every table carries `CreatedAt`, a UTC `datetime2(7)`. On the two child tables it is the creation time of the entry the row was written with.
