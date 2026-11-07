# Changelog

## [0.1.0] - 2026-11-07

### Added

- `IMemoryStore`, `IMemoryScope`, `IMemorySummarizer` and `IMemoryScopeResolver` (ADR-0022 D3).
- `MemoryScopeKey`, `MemoryCaller`, `NewMemoryEntry`, `MemoryEntry` (with `EmbeddingModelId`), `MemoryQuery`, `MemoryOperationalReadRequest`, `MemoryOperationalScopeResult`, `MemorySummaryRequest`, `MemoryScopeKind`, `MemoryApprovalStatus`, `MemoryScopeException` and `MemoryEscalationDeniedException`.
- No similarity-search member; it arrives with an embedding provider.
