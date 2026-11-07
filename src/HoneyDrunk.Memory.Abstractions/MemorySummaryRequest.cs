namespace HoneyDrunk.Memory.Abstractions;

/// <summary>A request to summarize entries in a scope's home key and supersede them.</summary>
/// <param name="EntryIds">The distinct ids of the entries to summarize.</param>
/// <param name="SummaryKind">The kind of the summary entry.</param>
/// <param name="Tags">The summary's tags; when <see langword="null"/>, the union of the source tags, capped at 16.</param>
public sealed record MemorySummaryRequest(IReadOnlyList<string> EntryIds, string SummaryKind = "summary", IReadOnlyList<string>? Tags = null)
{
    /// <summary>Gets a copy of the entry ids.</summary>
    public IReadOnlyList<string> EntryIds { get; } = ValidateIds(EntryIds, nameof(EntryIds));

    /// <summary>Gets the validated summary kind.</summary>
    public string SummaryKind { get; } = MemoryValidation.Kind(SummaryKind, nameof(SummaryKind));

    /// <summary>Gets the normalized summary tags, or <see langword="null"/>.</summary>
    public IReadOnlyList<string>? Tags { get; } = Tags is null ? null : MemoryValidation.Tags(Tags, nameof(Tags));

    private static IReadOnlyList<string> ValidateIds(IReadOnlyList<string> ids, string paramName)
    {
        var copy = MemoryValidation.NonBlankList(ids, paramName);
        return copy.Distinct(StringComparer.Ordinal).Count() == copy.Count
            ? copy
            : throw new ArgumentException("Entry ids must be distinct.", paramName);
    }
}
