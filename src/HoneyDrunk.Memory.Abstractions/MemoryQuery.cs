namespace HoneyDrunk.Memory.Abstractions;

/// <summary>
/// A recency and tag read. Results come newest first (by <see cref="MemoryEntry.WrittenAt"/>, then by
/// id descending, ordinal).
/// </summary>
/// <param name="Limit">The most entries to return: 1 to 200.</param>
/// <param name="Kind">Only entries of this kind, when set.</param>
/// <param name="AllTags">Only entries holding every one of these tags, when set. Normalized like entry tags.</param>
/// <param name="WrittenBefore">Only entries written strictly before this instant, when set.</param>
/// <param name="IncludeSuperseded">Whether superseded entries are returned.</param>
public sealed record MemoryQuery(
    int Limit = 20,
    string? Kind = null,
    IReadOnlyList<string>? AllTags = null,
    DateTimeOffset? WrittenBefore = null,
    bool IncludeSuperseded = false)
{
    /// <summary>The largest allowed <see cref="Limit"/>.</summary>
    public const int MaxLimit = 200;

    /// <summary>Gets the validated limit.</summary>
    public int Limit { get; } = Limit is >= 1 and <= MaxLimit
        ? Limit
        : throw new ArgumentOutOfRangeException(nameof(Limit), Limit, "The limit must be 1 to 200.");

    /// <summary>Gets the validated kind filter.</summary>
    public string? Kind { get; } = Kind is null ? null : MemoryValidation.Kind(Kind, nameof(Kind));

    /// <summary>Gets the normalized tag filter.</summary>
    public IReadOnlyList<string>? AllTags { get; } = AllTags is null ? null : MemoryValidation.Tags(AllTags, nameof(AllTags));
}
