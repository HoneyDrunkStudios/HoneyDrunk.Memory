# HoneyDrunk.Memory

Runtime composition for HoneyDrunk agent memory (ADR-0022).

- `MemoryScopeResolver`: agent scopes with no authorization question; escalation through HoneyDrunk.Auth's `IAuthorizationPolicy`; operational reads through HoneyDrunk.Operator's `IApprovalGate`.
- `ScopeCheckedMemoryStore`: the default `IMemoryStore`. It wraps a provider, accepts only scopes this runtime issued, enforces the write and expiry rules and emits metadata-only telemetry.
- `ChatMemorySummarizer`: summarizes entries through HoneyDrunk.AI's `IChatClient` and supersedes them.
- `AddHoneyDrunkMemory<TProvider>()` and `AddChatMemorySummarizer()`.

The host supplies `IAuthorizationPolicy`, `IApprovalGate`, `ITelemetryActivityFactory`, `IChatClient` and logging. See the repository README for the rules.
