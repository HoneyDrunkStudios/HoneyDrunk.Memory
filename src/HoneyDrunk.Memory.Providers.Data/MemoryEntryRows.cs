using HoneyDrunk.Kernel.Abstractions.Identity;
using HoneyDrunk.Memory.Abstractions;

namespace HoneyDrunk.Memory.Providers.Data;

/// <summary>Converts between the contract's entries and the stored rows.</summary>
internal static class MemoryEntryRows
{
    /// <summary>Builds the row for a new entry, with its tags and summary sources in the given order.</summary>
    /// <param name="id">The entry id.</param>
    /// <param name="home">The key the entry is written under.</param>
    /// <param name="entry">The entry to write.</param>
    /// <param name="writtenAt">The write time.</param>
    /// <param name="summaryOf">The ids of the entries this entry summarizes; empty for an ordinary entry.</param>
    /// <returns>The row.</returns>
    public static MemoryEntryEntity ToEntity(string id, MemoryScopeKey home, NewMemoryEntry entry, DateTimeOffset writtenAt, IReadOnlyList<string> summaryOf)
    {
        var row = new MemoryEntryEntity
        {
            Id = id,
            TenantId = home.TenantId.ToString(),
            ProjectId = home.ProjectId.ToString(),
            AgentId = home.AgentId,
            Kind = entry.Kind,
            Content = entry.Content,
            CreatedAt = writtenAt,
        };
        for (var i = 0; i < entry.Tags.Count; i++)
        {
            row.Tags.Add(new MemoryEntryTagEntity { EntryId = id, Ordinal = (byte)i, Tag = entry.Tags[i], CreatedAt = writtenAt });
        }

        for (var i = 0; i < summaryOf.Count; i++)
        {
            row.SummarySources.Add(new MemoryEntrySummarySourceEntity { SummaryEntryId = id, Ordinal = i, SourceEntryId = summaryOf[i], CreatedAt = writtenAt });
        }

        return row;
    }

    /// <summary>Builds the contract's entry from a row loaded with its tags and summary sources.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The entry. No embedding is stored, so its embedding model is <see langword="null"/>.</returns>
    public static MemoryEntry ToEntry(MemoryEntryEntity row) =>
        new(
            row.Id,
            new MemoryScopeKey(new TenantId(row.TenantId), new ProjectId(row.ProjectId), row.AgentId),
            row.Kind,
            row.Content,
            Array.AsReadOnly(row.Tags.OrderBy(tag => tag.Ordinal).Select(tag => tag.Tag).ToArray()),
            row.CreatedAt,
            EmbeddingModelId: null,
            Array.AsReadOnly(row.SummarySources.OrderBy(source => source.Ordinal).Select(source => source.SourceEntryId).ToArray()),
            row.SupersededByEntryId);
}
