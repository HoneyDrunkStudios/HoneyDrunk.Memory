namespace HoneyDrunk.Memory.Abstractions;

/// <summary>Compresses entries into one summary entry that supersedes them. This is a system operation, not an agent one.</summary>
public interface IMemorySummarizer
{
    /// <summary>Summarizes entries in the scope's home key and supersedes them atomically.</summary>
    /// <param name="scope">A writable scope whose home holds every entry.</param>
    /// <param name="request">The entries and the summary's kind and tags.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The written summary entry.</returns>
    Task<MemoryEntry> SummarizeAsync(IMemoryScope scope, MemorySummaryRequest request, CancellationToken cancellationToken = default);
}
