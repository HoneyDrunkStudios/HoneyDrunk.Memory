namespace HoneyDrunk.Memory.Abstractions;

/// <summary>A stored memory entry, as returned by <c>IMemoryStore</c>.</summary>
/// <param name="Id">The entry id assigned by the store.</param>
/// <param name="Scope">The key the entry was written under.</param>
/// <param name="Kind">The entry kind.</param>
/// <param name="Content">The content, verbatim.</param>
/// <param name="Tags">The normalized tags.</param>
/// <param name="WrittenAt">The instant the store wrote the entry.</param>
/// <param name="EmbeddingModelId">
/// The identifier of the embedding model that produced the embedding stored for this entry
/// (invariant 140). <see langword="null"/> means no embedding is stored for the entry. Similarity
/// retrieval never scores an entry whose identifier differs from the query model; re-embedding an
/// entry under another model is a supersession, not an in-place update.
/// </param>
/// <param name="SummaryOf">The ids of the entries this entry summarizes; empty for an ordinary entry.</param>
/// <param name="SupersededBy">The id of the entry that supersedes this one, or <see langword="null"/>.</param>
public sealed record MemoryEntry(
    string Id,
    MemoryScopeKey Scope,
    string Kind,
    string Content,
    IReadOnlyList<string> Tags,
    DateTimeOffset WrittenAt,
    string? EmbeddingModelId,
    IReadOnlyList<string> SummaryOf,
    string? SupersededBy);
