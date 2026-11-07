using HoneyDrunk.Memory.Abstractions;

namespace HoneyDrunk.Memory.Providers.InMemory;

/// <summary>
/// An in-memory <see cref="IMemoryStore"/> for tests and local development. Zero network and
/// deterministic given its clock; all state is held under one lock and lost with the process.
/// </summary>
/// <remarks>
/// A provider trusts the scope it is given and filters by its keys. Compose it behind the
/// HoneyDrunk.Memory runtime, whose store rejects scopes the runtime did not issue.
/// </remarks>
/// <param name="timeProvider">The clock for ids and write times; defaults to <see cref="TimeProvider.System"/>.</param>
public sealed class InMemoryMemoryStore(TimeProvider? timeProvider = null) : IMemoryStore
{
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;
    private readonly Lock gate = new();
    private readonly Dictionary<string, MemoryEntry> entries = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task<MemoryEntry> WriteAsync(IMemoryScope scope, NewMemoryEntry entry, CancellationToken cancellationToken = default)
    {
        RequireWritable(scope);
        ArgumentNullException.ThrowIfNull(entry);
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.gate)
        {
            var stored = this.Create(scope.Home, entry, []);
            this.entries.Add(stored.Id, stored);
            return Task.FromResult(stored);
        }
    }

    /// <inheritdoc />
    public Task<MemoryEntry?> GetAsync(IMemoryScope scope, string entryId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(entryId);
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.gate)
        {
            var found = this.entries.TryGetValue(entryId, out var entry) && scope.Readable.Contains(entry.Scope) ? entry : null;
            return Task.FromResult(found);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<MemoryEntry>> ReadAsync(IMemoryScope scope, MemoryQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        var readable = scope.Readable.ToHashSet();
        lock (this.gate)
        {
            IReadOnlyList<MemoryEntry> result = this.entries.Values
                .Where(entry => readable.Contains(entry.Scope))
                .Where(entry => query.IncludeSuperseded || entry.SupersededBy is null)
                .Where(entry => query.Kind is null || string.Equals(entry.Kind, query.Kind, StringComparison.Ordinal))
                .Where(entry => query.AllTags is null || query.AllTags.All(tag => entry.Tags.Contains(tag, StringComparer.Ordinal)))
                .Where(entry => query.WrittenBefore is null || entry.WrittenAt < query.WrittenBefore.Value)
                .OrderByDescending(entry => entry.WrittenAt)
                .ThenByDescending(entry => entry.Id, StringComparer.Ordinal)
                .Take(query.Limit)
                .ToArray();
            return Task.FromResult(result);
        }
    }

    /// <inheritdoc />
    public Task<MemoryEntry> WriteSummaryAsync(IMemoryScope scope, NewMemoryEntry summary, IReadOnlyList<string> supersededIds, CancellationToken cancellationToken = default)
    {
        RequireWritable(scope);
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(supersededIds);
        cancellationToken.ThrowIfCancellationRequested();
        if (supersededIds.Count == 0 || supersededIds.Distinct(StringComparer.Ordinal).Count() != supersededIds.Count)
        {
            throw new ArgumentException("A summary supersedes one or more distinct entries.", nameof(supersededIds));
        }

        lock (this.gate)
        {
            var sources = new List<MemoryEntry>(supersededIds.Count);
            foreach (var id in supersededIds)
            {
                if (id is null || !this.entries.TryGetValue(id, out var source) || source.Scope != scope.Home || source.SupersededBy is not null)
                {
                    throw new ArgumentException("Every superseded entry must exist in the home scope and not already be superseded.", nameof(supersededIds));
                }

                sources.Add(source);
            }

            var stored = this.Create(scope.Home, summary, [.. supersededIds]);
            this.entries.Add(stored.Id, stored);
            foreach (var source in sources)
            {
                this.entries[source.Id] = source with { SupersededBy = stored.Id };
            }

            return Task.FromResult(stored);
        }
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(IMemoryScope scope, string entryId, CancellationToken cancellationToken = default)
    {
        RequireWritable(scope);
        ArgumentNullException.ThrowIfNull(entryId);
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.gate)
        {
            var deleted = this.entries.TryGetValue(entryId, out var entry) && entry.Scope == scope.Home && this.entries.Remove(entryId);
            return Task.FromResult(deleted);
        }
    }

    /// <inheritdoc />
    public Task<int> DeleteAllAsync(IMemoryScope scope, CancellationToken cancellationToken = default)
    {
        RequireWritable(scope);
        cancellationToken.ThrowIfCancellationRequested();
        lock (this.gate)
        {
            var ids = this.entries.Values.Where(entry => entry.Scope == scope.Home).Select(entry => entry.Id).ToArray();
            foreach (var id in ids)
            {
                this.entries.Remove(id);
            }

            return Task.FromResult(ids.Length);
        }
    }

    private static void RequireWritable(IMemoryScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (!scope.CanWrite)
        {
            throw new MemoryScopeException($"The {scope.Kind} scope for '{scope.Home}' is read-only.");
        }
    }

    private MemoryEntry Create(MemoryScopeKey home, NewMemoryEntry entry, IReadOnlyList<string> summaryOf)
    {
        var now = this.timeProvider.GetUtcNow();
        var id = Guid.CreateVersion7(now).ToString("N");
        return new MemoryEntry(id, home, entry.Kind, entry.Content, entry.Tags, now, EmbeddingModelId: null, summaryOf, SupersededBy: null);
    }
}
