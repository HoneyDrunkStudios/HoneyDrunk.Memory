# Changelog

## [0.2.0] - 2026-11-11

### Changed

- No change to the contracts. The package targets `net10.0` and `net11.0`.

## [0.1.0] - 2026-11-07

### Added

- `IMemoryStore`, `IMemoryScope`, `IMemorySummarizer` and `IMemoryScopeResolver` (ADR-0022 D3).
- `MemoryScopeKey`, `MemoryCaller`, `NewMemoryEntry`, `MemoryEntry` (with `EmbeddingModelId`), `MemoryQuery`, `MemoryOperationalReadRequest`, `MemoryOperationalScopeResult`, `MemorySummaryRequest`, `MemoryScopeKind`, `MemoryApprovalStatus`, `MemoryScopeException` and `MemoryEscalationDeniedException`.
- `MemoryEntry` and `NewMemoryEntry` text (`ToString()`) carries metadata only, never content or tags (invariant 141).
- No similarity-search member; it arrives with an embedding provider.
