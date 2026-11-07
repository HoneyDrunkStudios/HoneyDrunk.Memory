# Changelog

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
