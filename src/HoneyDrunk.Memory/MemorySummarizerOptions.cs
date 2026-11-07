namespace HoneyDrunk.Memory;

/// <summary>Limits for <see cref="ChatMemorySummarizer"/>.</summary>
public sealed class MemorySummarizerOptions
{
    /// <summary>Gets or sets the most entries one summary may cover: 2 to 50.</summary>
    public int MaxEntriesPerSummary { get; set; } = 50;

    /// <summary>Gets or sets the most tokens the model may produce for one summary.</summary>
    public int MaxSummaryTokens { get; set; } = 1024;
}
