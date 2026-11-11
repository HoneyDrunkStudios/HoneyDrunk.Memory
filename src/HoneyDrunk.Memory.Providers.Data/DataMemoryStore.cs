using HoneyDrunk.Memory.Abstractions;

namespace HoneyDrunk.Memory.Providers.Data;

/// <summary>
/// A durable <see cref="IMemoryStore"/> over HoneyDrunk.Data's Entity Framework layer. Memory
/// survives a restart and is shared by every host that uses the same database. It gives the same
/// answers as the in-memory provider.
/// </summary>
/// <remarks>
/// A provider trusts the scope it is given and filters by its keys. Compose it behind the
/// HoneyDrunk.Memory runtime, whose store rejects scopes the runtime did not issue.
/// </remarks>
public sealed class DataMemoryStore : IMemoryStore
{
    private readonly MemoryEntryDataService entries;
    private readonly TimeProvider timeProvider;

    /// <summary>Initializes a new instance of the <see cref="DataMemoryStore"/> class.</summary>
    /// <param name="entries">The data service.</param>
    /// <param name="timeProvider">The clock for ids and write times.</param>
    internal DataMemoryStore(MemoryEntryDataService entries, TimeProvider timeProvider)
    {
        this.entries = entries ?? throw new ArgumentNullException(nameof(entries));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc />
    public async Task<MemoryEntry> WriteAsync(IMemoryScope scope, NewMemoryEntry entry, CancellationToken cancellationToken = default)
    {
        RequireWritable(scope);
        ArgumentNullException.ThrowIfNull(entry);
        cancellationToken.ThrowIfCancellationRequested();
        var writtenAt = this.timeProvider.GetUtcNow();
        var id = MemoryIds.NewEntryId(writtenAt);
        await this.entries.InsertAsync(scope.Home, MemoryEntryRows.ToEntity(id, scope.Home, entry, writtenAt, []), cancellationToken).ConfigureAwait(false);
        return new MemoryEntry(id, scope.Home, entry.Kind, entry.Content, entry.Tags, writtenAt, EmbeddingModelId: null, [], SupersededBy: null);
    }

    /// <inheritdoc />
    public async Task<MemoryEntry?> GetAsync(IMemoryScope scope, string entryId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(entryId);
        cancellationToken.ThrowIfCancellationRequested();
        if (!MemoryIds.IsEntryId(entryId))
        {
            return null;
        }

        var row = await this.entries.FindAsync(scope.Readable, entryId, cancellationToken).ConfigureAwait(false);
        return row is null ? null : MemoryEntryRows.ToEntry(row);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MemoryEntry>> ReadAsync(IMemoryScope scope, MemoryQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        var rows = await this.entries.ReadAsync(scope.Readable, query, cancellationToken).ConfigureAwait(false);
        return rows.Select(MemoryEntryRows.ToEntry).ToArray();
    }

    /// <inheritdoc />
    public async Task<MemoryEntry> WriteSummaryAsync(IMemoryScope scope, NewMemoryEntry summary, IReadOnlyList<string> supersededIds, CancellationToken cancellationToken = default)
    {
        RequireWritable(scope);
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(supersededIds);
        cancellationToken.ThrowIfCancellationRequested();
        if (supersededIds.Count == 0 || supersededIds.Distinct(StringComparer.Ordinal).Count() != supersededIds.Count)
        {
            throw new ArgumentException("A summary supersedes one or more distinct entries.", nameof(supersededIds));
        }

        var sources = supersededIds.ToArray();
        var writtenAt = this.timeProvider.GetUtcNow();
        var id = MemoryIds.NewEntryId(writtenAt);
        var stored = sources.All(MemoryIds.IsEntryId)
            && await this.entries.InsertSummaryAsync(scope.Home, MemoryEntryRows.ToEntity(id, scope.Home, summary, writtenAt, sources), sources, cancellationToken).ConfigureAwait(false);
        return stored
            ? new MemoryEntry(id, scope.Home, summary.Kind, summary.Content, summary.Tags, writtenAt, EmbeddingModelId: null, Array.AsReadOnly(sources), SupersededBy: null)
            : throw new ArgumentException("Every superseded entry must exist in the home scope and not already be superseded.", nameof(supersededIds));
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(IMemoryScope scope, string entryId, CancellationToken cancellationToken = default)
    {
        RequireWritable(scope);
        ArgumentNullException.ThrowIfNull(entryId);
        cancellationToken.ThrowIfCancellationRequested();
        return MemoryIds.IsEntryId(entryId)
            && await this.entries.DeleteAsync(scope.Home, entryId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<int> DeleteAllAsync(IMemoryScope scope, CancellationToken cancellationToken = default)
    {
        RequireWritable(scope);
        cancellationToken.ThrowIfCancellationRequested();
        return this.entries.DeleteAllAsync(scope.Home, cancellationToken);
    }

    private static void RequireWritable(IMemoryScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (!scope.CanWrite)
        {
            throw new MemoryScopeException($"The {scope.Kind} scope for '{scope.Home}' is read-only.");
        }
    }
}
