using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace HoneyDrunk.Memory.Providers.Data.Tests;

/// <summary>Records the text of every command a context sends.</summary>
internal sealed class CommandLog : DbCommandInterceptor
{
    private readonly List<string> commands = [];

    /// <summary>Gets the recorded command texts, in order.</summary>
    public IReadOnlyList<string> Commands
    {
        get
        {
            lock (this.commands)
            {
                return [.. this.commands];
            }
        }
    }

    /// <summary>Forgets what was recorded so far.</summary>
    public void Clear()
    {
        lock (this.commands)
        {
            this.commands.Clear();
        }
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        this.Record(command);
        return ValueTask.FromResult(result);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        this.Record(command);
        return ValueTask.FromResult(result);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
    {
        this.Record(command);
        return ValueTask.FromResult(result);
    }

    private void Record(DbCommand command)
    {
        lock (this.commands)
        {
            this.commands.Add(command.CommandText);
        }
    }
}
