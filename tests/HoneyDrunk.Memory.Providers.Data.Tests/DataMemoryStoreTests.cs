using HoneyDrunk.Memory.Abstractions;
using HoneyDrunk.Memory.Providers.Contract;

namespace HoneyDrunk.Memory.Providers.Data.Tests;

/// <summary>The store contract against the Data provider on a SQLite file.</summary>
public sealed class DataMemoryStoreTests : MemoryStoreContract, IDisposable
{
    private readonly SqliteDatabase database = new();

    /// <inheritdoc />
    public void Dispose() => this.database.Dispose();

    /// <inheritdoc />
    protected override IMemoryStore CreateStore(TimeProvider timeProvider) => this.database.CreateStore(timeProvider);
}
