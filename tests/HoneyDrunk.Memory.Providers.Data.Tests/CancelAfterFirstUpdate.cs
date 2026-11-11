using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace HoneyDrunk.Memory.Providers.Data.Tests;

/// <summary>Cancels a token right after an update has changed a row, as a caller cancelling at that moment would.</summary>
internal sealed class CancelAfterFirstUpdate : DbCommandInterceptor, IDisposable
{
    /// <summary>Gets the source of the token to pass to the call under test.</summary>
    public CancellationTokenSource Cancellation { get; } = new();

    /// <inheritdoc />
    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (result > 0 && command.CommandText.Contains("UPDATE", StringComparison.OrdinalIgnoreCase))
        {
            this.Cancellation.Cancel();
        }

        return ValueTask.FromResult(result);
    }

    /// <inheritdoc />
    public void Dispose() => this.Cancellation.Dispose();
}
