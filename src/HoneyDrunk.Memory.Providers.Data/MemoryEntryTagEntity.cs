namespace HoneyDrunk.Memory.Providers.Data;

/// <summary>One tag on one entry.</summary>
internal sealed class MemoryEntryTagEntity
{
    /// <summary>Gets or sets the entry the tag is on.</summary>
    public string EntryId { get; set; } = string.Empty;

    /// <summary>Gets or sets the tag's position on the entry, from 0.</summary>
    public byte Ordinal { get; set; }

    /// <summary>Gets or sets the tag, as normalized by the contract.</summary>
    public string Tag { get; set; } = string.Empty;
}
