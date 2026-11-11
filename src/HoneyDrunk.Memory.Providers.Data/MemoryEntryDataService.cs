using HoneyDrunk.Memory.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace HoneyDrunk.Memory.Providers.Data;

/// <summary>
/// Reads and writes memory entries. Each call uses its own context from the factory, so the service
/// is safe as a singleton. Every query starts from <see cref="InKey"/>: there is no other way to
/// reach an entry, so no statement runs without the tenant, the project and the agent.
/// </summary>
/// <param name="contexts">Creates a context per call.</param>
internal sealed class MemoryEntryDataService(IDbContextFactory<MemoryDbContext> contexts)
{
    private readonly IDbContextFactory<MemoryDbContext> contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));

    /// <summary>Stores one entry with its tags, in one transaction.</summary>
    /// <param name="home">The key the entry is written under.</param>
    /// <param name="entry">The row, with its tags.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task.</returns>
    public async Task InsertAsync(MemoryScopeKey home, MemoryEntryEntity entry, CancellationToken cancellationToken)
    {
        await using var context = await this.contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await MemoryUnitOfWork.RunAsync(
            context,
            async token =>
            {
                context.Entries.Add(entry);
                await context.SaveChangesAsync(token).ConfigureAwait(false);
                return true;
            },
            token => InKey(context.Entries.AsNoTracking(), home).AnyAsync(row => row.Id == entry.Id, token),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Finds one entry by id under any of the readable keys.</summary>
    /// <param name="readable">The keys the caller may read.</param>
    /// <param name="entryId">A well-formed entry id.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The row with its tags and summary sources, or <see langword="null"/>.</returns>
    public async Task<MemoryEntryEntity?> FindAsync(IReadOnlyList<MemoryScopeKey> readable, string entryId, CancellationToken cancellationToken)
    {
        await using var context = await this.contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        foreach (var key in readable.Distinct())
        {
            var found = await WithChildren(InKey(context.Entries, key).Where(row => row.Id == entryId))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            if (found.Count > 0)
            {
                return found[0];
            }
        }

        return null;
    }

    /// <summary>
    /// Reads the newest matching entries under the readable keys. Each key gives its newest ids from
    /// the scope index; only the winners across all keys are then loaded with their content, so no
    /// more than <see cref="MemoryQuery.Limit"/> entries are ever materialized.
    /// </summary>
    /// <param name="readable">The keys the caller may read.</param>
    /// <param name="query">The filter and limit.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The rows, newest first, then by id descending.</returns>
    public async Task<IReadOnlyList<MemoryEntryEntity>> ReadAsync(IReadOnlyList<MemoryScopeKey> readable, MemoryQuery query, CancellationToken cancellationToken)
    {
        await using var context = await this.contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var keys = readable.Distinct().ToArray();
        var candidates = new List<(int key, string id, DateTimeOffset writtenAt)>();
        for (var i = 0; i < keys.Length; i++)
        {
            var newest = await Filtered(context, keys[i], query)
                .OrderByDescending(row => row.WrittenAt)
                .ThenByDescending(row => row.Id)
                .Take(query.Limit)
                .Select(row => new { row.Id, row.WrittenAt })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            candidates.AddRange(newest.Select(row => (key: i, id: row.Id, writtenAt: row.WrittenAt)));
        }

        var winners = candidates
            .OrderByDescending(candidate => candidate.writtenAt)
            .ThenByDescending(candidate => candidate.id, StringComparer.Ordinal)
            .Take(query.Limit)
            .ToArray();
        var rows = new Dictionary<string, MemoryEntryEntity>(StringComparer.Ordinal);
        foreach (var group in winners.GroupBy(winner => winner.key))
        {
            var ids = group.Select(winner => winner.id).ToArray();
            var loaded = await WithChildren(Filtered(context, keys[group.Key], query).Where(row => ids.Contains(row.Id)))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            foreach (var row in loaded)
            {
                rows[row.Id] = row;
            }
        }

        // An entry deleted or superseded between the two steps is simply not returned.
        return winners.Where(winner => rows.ContainsKey(winner.id)).Select(winner => rows[winner.id]).ToArray();
    }

    /// <summary>Deletes one entry from the home key; its tags and summary sources go with it.</summary>
    /// <param name="home">The home key.</param>
    /// <param name="entryId">A well-formed entry id.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns><see langword="true"/> when an entry in the home key was deleted.</returns>
    public async Task<bool> DeleteAsync(MemoryScopeKey home, string entryId, CancellationToken cancellationToken)
    {
        await using var context = await this.contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var deleted = await InKey(context.Entries, home)
            .Where(row => row.Id == entryId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
        return deleted > 0;
    }

    /// <summary>Deletes every entry in the home key.</summary>
    /// <param name="home">The home key.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The number of entries deleted.</returns>
    public async Task<int> DeleteAllAsync(MemoryScopeKey home, CancellationToken cancellationToken)
    {
        await using var context = await this.contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await InKey(context.Entries, home).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
    }

    private static IQueryable<MemoryEntryEntity> InKey(IQueryable<MemoryEntryEntity> source, MemoryScopeKey key)
    {
        var tenantId = key.TenantId.ToString();
        var projectId = key.ProjectId.ToString();
        var agentId = key.AgentId;
        return source.Where(row => row.TenantId == tenantId && row.ProjectId == projectId && row.AgentId == agentId);
    }

    private static IQueryable<MemoryEntryEntity> Filtered(MemoryDbContext context, MemoryScopeKey key, MemoryQuery query)
    {
        var source = InKey(context.Entries, key);
        if (!query.IncludeSuperseded)
        {
            source = source.Where(row => row.SupersededByEntryId == null);
        }

        if (query.Kind is { } kind)
        {
            source = source.Where(row => row.Kind == kind);
        }

        if (query.WrittenBefore is { } writtenBefore)
        {
            var before = writtenBefore.ToUniversalTime();
            source = source.Where(row => row.WrittenAt < before);
        }

        foreach (var tag in query.AllTags ?? [])
        {
            source = source.Where(row => row.Tags.Any(held => held.Tag == tag));
        }

        return source;
    }

    private static IQueryable<MemoryEntryEntity> WithChildren(IQueryable<MemoryEntryEntity> source) =>
        source.Include(row => row.Tags).Include(row => row.SummarySources).AsSplitQuery().AsNoTracking();
}
