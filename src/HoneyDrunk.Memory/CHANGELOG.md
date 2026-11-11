# Changelog

## [0.2.0] - 2026-11-11

### Changed

- No functional change. The package targets `net11.0`.

## [0.1.0] - 2026-11-07

### Added

- `MemoryScopeResolver`: agent scopes, Auth-gated escalation and Operator-gated operational reads.
- `ScopeCheckedMemoryStore`: the default store, which accepts only issued scopes and emits metadata-only telemetry.
- `ChatMemorySummarizer` over `IChatClient`.
- `AddHoneyDrunkMemory<TProvider>()` and `AddChatMemorySummarizer()`.
