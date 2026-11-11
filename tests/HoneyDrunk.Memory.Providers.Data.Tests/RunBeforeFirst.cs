using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace HoneyDrunk.Memory.Providers.Data.Tests;

/// <summary>
/// Runs some work once, just before the first query whose text holds a marker, as another host
/// committing a change between two of a read's statements would.
/// </summary>
/// <param name="marker">Text the query must hold, for example a quoted table or column name.</param>
/// <param name="meanwhile">The other host's work.</param>
internal sealed class RunBeforeFirst(string marker, Func<Task> meanwhile) : DbCommandInterceptor
{
    private int ran;

    /// <summary>Gets or sets a value indicating whether the interceptor is watching.</summary>
    public bool Armed { get; set; }

    /// <summary>Gets a value indicating whether the work ran.</summary>
    public bool Ran => this.ran == 1;

    /// <inheritdoc />
    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        if (this.Armed && command.CommandText.Contains(marker, StringComparison.Ordinal) && Interlocked.Exchange(ref this.ran, 1) == 0)
        {
            await meanwhile().ConfigureAwait(false);
        }

        return result;
    }
}
