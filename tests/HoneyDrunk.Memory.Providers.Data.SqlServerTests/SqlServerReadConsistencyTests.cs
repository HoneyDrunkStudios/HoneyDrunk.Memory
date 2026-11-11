using HoneyDrunk.Memory.Abstractions;
using HoneyDrunk.Memory.Providers.Data.Tests;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HoneyDrunk.Memory.Providers.Data.SqlServerTests;

/// <summary>The read-consistency contract on SQL Server, where each statement of a read sees the latest committed rows.</summary>
public sealed class SqlServerReadConsistencyTests : ReadConsistencyContract, IDisposable
{
    private readonly SqlServerDatabase database = new();

    /// <inheritdoc />
    public void Dispose() => this.database.Dispose();

    /// <inheritdoc />
    protected override IMemoryStore CreateStore(params IInterceptor[] interceptors) => this.database.CreateStore(this.Time, interceptors);
}
