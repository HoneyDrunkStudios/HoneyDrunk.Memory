using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace HoneyDrunk.Memory.Providers.Data.Tests;

/// <summary>Fails any command that inserts into one table, as a database failing partway through a write would.</summary>
/// <param name="table">The table name, for example <c>EntrySummarySource</c>.</param>
internal sealed class FailInsertInto(string table) : DbCommandInterceptor
{
    /// <summary>Gets or sets a value indicating whether inserts fail.</summary>
    public bool Armed { get; set; }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        this.Check(command);
        return ValueTask.FromResult(result);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        this.Check(command);
        return ValueTask.FromResult(result);
    }

    private void Check(DbCommand command)
    {
        if (this.Armed && command.CommandText.Contains("INSERT INTO", StringComparison.OrdinalIgnoreCase) && command.CommandText.Contains($"\"{table}\"", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"insert into {table} failed");
        }
    }
}
