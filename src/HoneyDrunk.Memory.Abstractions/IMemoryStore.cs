namespace HoneyDrunk.Memory.Abstractions;

/// <summary>
/// The only surface through which memory content is written or read (invariant 139). Every call takes
/// a resolved scope, and providers return only entries under the scope's readable keys. Reads are by
/// recency and tag; similarity search arrives with an embedding provider.
/// </summary>
public interface IMemoryStore
{
    /// <summary>Writes an entry to the scope's home key.</summary>
    /// <param name="scope">A writable scope.</param>
    /// <param name="entry">The entry.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The stored entry, with its id and write time.</returns>
    /// <exception cref="MemoryScopeException">The scope cannot write.</exception>
    Task<MemoryEntry> WriteAsync(IMemoryScope scope, NewMemoryEntry entry, CancellationToken cancellationToken = default);

    /// <summary>Gets one entry by id. A missing id and an id outside the scope both return <see langword="null"/>.</summary>
    /// <param name="scope">The scope.</param>
    /// <param name="entryId">The entry id.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The entry, or <see langword="null"/>.</returns>
    Task<MemoryEntry?> GetAsync(IMemoryScope scope, string entryId, CancellationToken cancellationToken = default);

    /// <summary>Reads entries under the scope's readable keys, newest first.</summary>
    /// <param name="scope">The scope.</param>
    /// <param name="query">The filter and limit.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The matching entries.</returns>
    Task<IReadOnlyList<MemoryEntry>> ReadAsync(IMemoryScope scope, MemoryQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a summary entry and supersedes the given entries in one step. Every id must exist in the
    /// home key and not already be superseded, or nothing changes.
    /// </summary>
    /// <param name="scope">A writable scope.</param>
    /// <param name="summary">The summary entry.</param>
    /// <param name="supersededIds">The ids of the entries the summary replaces.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The stored summary entry.</returns>
    /// <exception cref="MemoryScopeException">The scope cannot write.</exception>
    Task<MemoryEntry> WriteSummaryAsync(IMemoryScope scope, NewMemoryEntry summary, IReadOnlyList<string> supersededIds, CancellationToken cancellationToken = default);

    /// <summary>Deletes one entry from the home key.</summary>
    /// <param name="scope">A writable scope.</param>
    /// <param name="entryId">The entry id.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns><see langword="true"/> when an entry in the home key was deleted.</returns>
    /// <exception cref="MemoryScopeException">The scope cannot write.</exception>
    Task<bool> DeleteAsync(IMemoryScope scope, string entryId, CancellationToken cancellationToken = default);

    /// <summary>Deletes every entry in the home key.</summary>
    /// <param name="scope">A writable scope.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The number of entries deleted.</returns>
    /// <exception cref="MemoryScopeException">The scope cannot write.</exception>
    Task<int> DeleteAllAsync(IMemoryScope scope, CancellationToken cancellationToken = default);
}
