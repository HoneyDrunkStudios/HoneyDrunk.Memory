namespace HoneyDrunk.Memory.Providers.Data;

/// <summary>One entry that a summary entry replaced, in the order the summary listed it.</summary>
internal sealed class MemoryEntrySummarySourceEntity
{
    /// <summary>Gets or sets the summary entry.</summary>
    public string SummaryEntryId { get; set; } = string.Empty;

    /// <summary>Gets or sets the source's position in the summary's list, from 0.</summary>
    public int Ordinal { get; set; }

    /// <summary>Gets or sets the id of the replaced entry. The id is kept when that entry is deleted.</summary>
    public string SourceEntryId { get; set; } = string.Empty;

    /// <summary>Gets or sets the instant the row was written, with its summary entry; stored in UTC.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
