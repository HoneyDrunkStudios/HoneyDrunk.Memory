using Microsoft.EntityFrameworkCore;

namespace HoneyDrunk.Memory.Providers.Data;

/// <summary>
/// Runs a change in one database transaction, under the provider's execution strategy so a host that
/// enables retries on failure still works. Each attempt starts with an empty change tracker. The
/// transaction commits when the work returns and rolls back when it throws, so work that must not be
/// kept throws. When a commit's outcome is unknown (the reply was lost), the change is not replayed
/// if the caller's check finds it already stored.
/// </summary>
internal static class MemoryUnitOfWork
{
    /// <summary>Runs <paramref name="work"/> in a transaction and commits it.</summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="context">The context.</param>
    /// <param name="work">The change; it must save its own changes.</param>
    /// <param name="committed">Tells whether the change is already stored, asked only when a commit's outcome is unknown.</param>
    /// <param name="cancellationToken">Cancels the transaction; nothing is kept when it is cancelled before the commit.</param>
    /// <returns>The work's result.</returns>
    public static Task<T> RunAsync<T>(MemoryDbContext context, Func<CancellationToken, Task<T>> work, Func<CancellationToken, Task<bool>> committed, CancellationToken cancellationToken)
    {
        var strategy = context.Database.CreateExecutionStrategy();
        return strategy.ExecuteInTransactionAsync(
            async token =>
            {
                context.ChangeTracker.Clear();
                return await work(token).ConfigureAwait(false);
            },
            committed,
            cancellationToken);
    }
}
