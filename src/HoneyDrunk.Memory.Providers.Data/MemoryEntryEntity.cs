namespace HoneyDrunk.Memory.Providers.Data;

/// <summary>One memory entry written under one scope key.</summary>
internal sealed class MemoryEntryEntity
{
    /// <summary>Gets or sets the entry id: a version 7 GUID as 32 lower-case hexadecimal digits.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the tenant the entry belongs to, as ULID text.</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Gets or sets the project within the tenant, as ULID text.</summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>Gets or sets the agent that owns the entry.</summary>
    public string AgentId { get; set; } = string.Empty;

    /// <summary>Gets or sets the entry kind.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Gets or sets the entry text, verbatim.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the instant the entry was written, which the contract calls <c>WrittenAt</c>;
    /// stored in UTC. An entry is written once, so this is also the row's creation stamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the id of the summary entry that replaced this one, or <see langword="null"/> while
    /// the entry is current. The id is kept when that summary is deleted.
    /// </summary>
    public string? SupersededByEntryId { get; set; }

    /// <summary>Gets the entry's tags.</summary>
    public List<MemoryEntryTagEntity> Tags { get; } = [];

    /// <summary>Gets the entries this entry summarizes; empty for an ordinary entry.</summary>
    public List<MemoryEntrySummarySourceEntity> SummarySources { get; } = [];
}
