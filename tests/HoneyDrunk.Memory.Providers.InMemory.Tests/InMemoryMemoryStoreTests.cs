using HoneyDrunk.Memory.Abstractions;
using HoneyDrunk.Memory.Providers.Contract;

namespace HoneyDrunk.Memory.Providers.InMemory.Tests;

/// <summary>The store contract against the in-memory provider, which is its reference.</summary>
public sealed class InMemoryMemoryStoreTests : MemoryStoreContract
{
    /// <inheritdoc />
    protected override IMemoryStore CreateStore(TimeProvider timeProvider) => new InMemoryMemoryStore(timeProvider);
}
