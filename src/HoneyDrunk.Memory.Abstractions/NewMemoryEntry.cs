namespace HoneyDrunk.Memory.Abstractions;

/// <summary>An entry to be written. The store assigns its id, time and scope key.</summary>
/// <param name="Kind">The kind: 1 to 32 characters of <c>a-z</c>, <c>0-9</c>, <c>_</c> or <c>-</c>, starting with a letter.</param>
/// <param name="Content">The content: 1 to 16,000 characters, not only whitespace, stored verbatim.</param>
/// <param name="Tags">
/// The tags: trimmed and lower-cased (invariant culture), 1 to 64 characters each with no whitespace
/// or control characters, duplicates removed with the first order kept, at most 16.
/// </param>
public sealed record NewMemoryEntry(string Kind, string Content, IReadOnlyList<string> Tags)
{
    /// <summary>Gets the validated kind.</summary>
    public string Kind { get; } = MemoryValidation.Kind(Kind, nameof(Kind));

    /// <summary>Gets the validated content, unchanged.</summary>
    public string Content { get; } = MemoryValidation.Content(Content, nameof(Content));

    /// <summary>Gets the normalized tags.</summary>
    public IReadOnlyList<string> Tags { get; } = MemoryValidation.Tags(Tags, nameof(Tags));
}
