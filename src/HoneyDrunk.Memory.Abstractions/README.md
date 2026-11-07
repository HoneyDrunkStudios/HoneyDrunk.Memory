# HoneyDrunk.Memory.Abstractions

Contracts for HoneyDrunk agent memory (ADR-0022). Downstream nodes compile against this package only.

- `IMemoryStore`: write, get, read by recency and tag, write a summary that supersedes entries, delete one entry or the home key. There is no similarity search yet.
- `IMemoryScope`: a resolved authorization window with one writable home key and a list of readable keys. `EscalateAsync` widens an agent scope through Auth and returns a read-only scope.
- `IMemoryScopeResolver`: resolves agent scopes and requests Operator-approved operational scopes.
- `IMemorySummarizer`: compresses entries into one summary entry.
- `MemoryScopeKey`, `MemoryCaller`, `NewMemoryEntry`, `MemoryEntry` (with `EmbeddingModelId`), `MemoryQuery`, `MemoryOperationalReadRequest`, `MemoryOperationalScopeResult`, `MemorySummaryRequest`, `MemoryScopeException`, `MemoryEscalationDeniedException`.

The only HoneyDrunk dependency is `HoneyDrunk.Kernel.Abstractions`, for `TenantId` and `ProjectId`.
