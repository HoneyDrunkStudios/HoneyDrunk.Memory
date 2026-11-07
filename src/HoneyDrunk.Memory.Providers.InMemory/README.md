# HoneyDrunk.Memory.Providers.InMemory

A deterministic, in-memory `IMemoryStore` for tests and local development. State is held under one lock and lost with the process. Not for production composition.

Compose it behind the runtime so every call passes the scope check:

```csharp
services.AddHoneyDrunkMemory<InMemoryMemoryStore>();
```
