using HoneyDrunk.Memory.Abstractions;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HoneyDrunk.Memory.Providers.Data.Tests;

/// <summary>The read-consistency contract on a SQLite file.</summary>
public sealed class ReadConsistencyTests : ReadConsistencyContract, IDisposable
{
    private readonly SqliteDatabase database = new();

    /// <inheritdoc />
    public void Dispose() => this.database.Dispose();

    /// <inheritdoc />
    protected override IMemoryStore CreateStore(params IInterceptor[] interceptors) => this.database.CreateStore(this.Time, interceptors);
}
