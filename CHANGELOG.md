# Changelog

## [0.2.0] - 2026-11-11

### Added

- `HoneyDrunk.Memory.Providers.Data`: a durable `IMemoryStore` over HoneyDrunk.Data's Entity Framework layer, so memory survives a restart and is shared by every host on one database. It ships the SQL Server scripts for its three tables in a `memory` schema; the host's database project carries them.
- One store contract test suite, run against the in-memory provider, the Data provider on SQLite and the Data provider on SQL Server.

### Changed

- The runtime and the providers target `net11.0`. `HoneyDrunk.Memory.Abstractions` targets `net10.0` and `net11.0`.
- The contracts are unchanged: the contract-shape baseline is the one released with 0.1.0.

## [0.1.0] - 2026-11-07

### Added

- `HoneyDrunk.Memory.Abstractions`: `IMemoryStore`, `IMemoryScope`, `IMemorySummarizer` and `IMemoryScopeResolver`, with `MemoryScopeKey`, `MemoryCaller`, `NewMemoryEntry`, `MemoryEntry`, `MemoryQuery` and the operational-read and summary records (ADR-0022 D3). `MemoryEntry` carries the embedding-model identifier (invariant 140).
- `HoneyDrunk.Memory`: scope resolution with Auth-gated escalation and Operator-gated operational reads (invariant 142), the scope-checked default store, the chat summarizer and DI registration.
- `HoneyDrunk.Memory.Providers.InMemory`: the in-memory store.
- The contract-shape canary for the frozen Abstractions surfaces (invariant 143).

### Notes

- Similarity search waits for an embedding provider; reads are by recency and tag.
- Operator's default approval gate cannot record a decision yet, so operational reads stay pending in production.
- Bulk deletion and right to erasure are deferred (ADR-0022 D11).
