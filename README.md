# HoneyDrunk.Memory

The agent-memory substrate of the HoneyDrunk Grid (ADR-0022). It stores what an agent learned or decided across its executions, scopes that memory by tenant, project and agent so an agent sees only what it is authorized to see, and summarizes entries through HoneyDrunk.AI.

Execution-scoped state (the turns and tool calls of one running execution) is not stored here; it lives with the agent runtime and is disposed with the execution.

## SDK requirement

Build and test with the .NET 11 SDK selected by `global.json`. Runtime packages target `net11.0`; consuming applications must also target .NET 11 or later. The contracts-only Abstractions package targets both `net10.0` and `net11.0` to preserve existing contract consumers.

## Packages

| Package | Purpose |
|---|---|
| `HoneyDrunk.Memory.Abstractions` | The contracts: `IMemoryStore`, `IMemoryScope`, `IMemorySummarizer`, `IMemoryScopeResolver`, `MemoryEntry` and the request and key records. Downstream nodes depend on this package only. It references only `HoneyDrunk.Kernel.Abstractions`. |
| `HoneyDrunk.Memory` | The runtime: scope resolution gated by Auth and Operator, the scope-checked default store, the chat summarizer and DI registration. |
| `HoneyDrunk.Memory.Providers.InMemory` | A deterministic in-memory store for tests and local development. Not for production composition. |
| `HoneyDrunk.Memory.Providers.Data` | A durable store over HoneyDrunk.Data's Entity Framework layer: memory survives a restart and is shared by every host on one database. It ships the SQL Server scripts for its three tables. |

## Scopes

Every store call takes a resolved `IMemoryScope`. A scope has one home key, `{tenant}/{project}/{agent}`, which is the only key it can write to, and a list of keys it may read.

- **Agent scope.** `IMemoryScopeResolver.ResolveAgentScope(home, caller)` reads and writes the home key only. No authorization question is raised.
- **Escalated scope.** `scope.EscalateAsync(keys)` asks HoneyDrunk.Auth's `IAuthorizationPolicy` once per added key: action `memory.escalate`, resource `memory/{tenant}/{project}/{agent}`, required scope `memory.read:{tenant}:{project}:{agent}` (configurable through `MemoryAuthorizationOptions.EscalationScopeTemplate`). A key in another tenant is always denied without asking Auth. Any denial, or a policy failure, denies every key with `MemoryEscalationDeniedException`. The result is read-only.
- **Operational scope.** `RequestOperationalScopeAsync(request)` asks HoneyDrunk.Operator's `IApprovalGate` for a read of 1 to 50 keys, for example an audit. The approval id is derived from the request, so a decision binds exactly that request. It does not wait: it returns `Pending`, `Approved`, `Denied` or `Expired`, and a read-only scope only when approved. The scope is refused after its expiry. Operator's default gate cannot record a human decision yet, so in production these reads stay `Pending` until Operator gains a decision surface.

The default `IMemoryStore` rejects any scope this runtime did not issue, and refuses writes, summaries and deletes through read-only scopes.

## Reads, writes and summaries

- Entries have a kind, up to 16,000 characters of content stored verbatim, and up to 16 normalized tags.
- Reads are by recency and tag: newest first, filtered by kind, by tags (all must match) and by write time, 1 to 200 at a time. Superseded entries are hidden unless asked for.
- `ChatMemorySummarizer` summarizes 2 to 50 entries in the home key through `IChatClient`, writes one summary entry and supersedes the sources in one step.
- **Similarity search is not available yet.** It needs an embedding provider. `MemoryEntry.EmbeddingModelId` is part of the contract now; when similarity search arrives, entries embedded by a different model than the query are never scored (invariant 140).
- Deletion is per entry and per home key. Bulk deletion and right to erasure are deferred (ADR-0022 D11).

## Telemetry

Every store call runs in one activity from Kernel's `ITelemetryActivityFactory`. Activities carry only metadata: scope coordinates, scope kind, entry id, kind and size, tag and result counts, the embedding model, the provider, the outcome and the approval status. Memory content, entry tags and query tags never appear in telemetry or logs (invariant 141).

## Composition

```csharp
services.AddHoneyDrunkMemory<InMemoryMemoryStore>();
services.AddChatMemorySummarizer();
```

The host supplies `IAuthorizationPolicy`, `IApprovalGate`, `ITelemetryActivityFactory`, `IChatClient` and logging. The provider is registered as its concrete type only; `IMemoryStore` always resolves to the scope-checked wrapper.

For memory that survives a restart, compose the durable provider instead. It holds no database driver; the host chooses the database, and the host's database project carries the tables (see the [provider's README](src/HoneyDrunk.Memory.Providers.Data/README.md)):

```csharp
services.AddMemoryDataProvider(options => options.UseSqlServer(connectionString));
services.AddHoneyDrunkMemory<DataMemoryStore>();
```

The in-memory provider stays the choice for tests and local development. Both providers pass the same store contract (`tests/Shared/MemoryStoreContract.cs`).

## Contract-shape canary

`api/HoneyDrunk.Memory.Abstractions.contract-shape.txt` is a snapshot of the frozen surfaces (`IMemoryStore`, `IMemoryScope`, `IMemorySummarizer`, `MemoryEntry`, `IMemoryScopeResolver`) and every type their signatures reach. The canary test fails when the shape drifts, and regeneration is refused until `Version` in `Directory.Build.props` rises (ADR-0022 D12, invariant 143).

## Build and test

```bash
dotnet build HoneyDrunk.Memory.slnx -c Release
dotnet test HoneyDrunk.Memory.slnx -c Release
```
