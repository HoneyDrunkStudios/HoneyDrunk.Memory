using HoneyDrunk.Memory.Abstractions;
using Xunit;
using static HoneyDrunk.Memory.Tests.TestKeys;

namespace HoneyDrunk.Memory.Tests;

/// <summary>Memory content, entry tags and query tags never reach telemetry or logs (invariant 141).</summary>
public sealed class TelemetryContentTests
{
    private const string SentinelContent = "SENTINEL-CONTENT-4410";
    private const string SentinelTag = "sentinel-tag-4411";
    private const string SentinelQuery = "sentinel-query-4412";

    private static readonly HashSet<string> AllowedTags = new(StringComparer.Ordinal)
    {
        "memory.operation",
        "memory.tenant_id",
        "memory.project_id",
        "memory.agent_id",
        "memory.scope_kind",
        "memory.readable_scope_count",
        "memory.entry_id",
        "memory.entry_kind",
        "memory.entry_size",
        "memory.tag_count",
        "memory.result_count",
        "memory.embedding_model_id",
        "memory.provider",
        "memory.outcome",
        "memory.approval_status",
    };

    /// <summary>Sentinels pass through every operation, and none appears in any tag value or log line.</summary>
    [Fact]
    public async Task No_content_or_tag_value_in_telemetry_or_logs()
    {
        var harness = new MemoryHarness("Summary mentioning " + SentinelContent);
        var other = Key(Project2);
        var scope = harness.Agent(Key(Project1), ReadScope(other));
        var first = await harness.Store.WriteAsync(scope, new NewMemoryEntry("note", "Note " + SentinelContent, [SentinelTag, SentinelQuery]));
        var second = await harness.Store.WriteAsync(scope, new NewMemoryEntry("note", SentinelContent + " again", [SentinelTag]));
        await harness.Store.ReadAsync(scope, new MemoryQuery(AllTags: [SentinelQuery]));
        await harness.Store.GetAsync(scope, first.Id);
        await harness.Summarizer.SummarizeAsync(scope, new MemorySummaryRequest([first.Id, second.Id], Tags: [SentinelTag]));
        var widened = await scope.EscalateAsync([other]);
        await harness.Store.ReadAsync(widened, new MemoryQuery(AllTags: [SentinelQuery], IncludeSuperseded: true));
        await Assert.ThrowsAsync<MemoryEscalationDeniedException>(() => scope.EscalateAsync([Key(Project3)]));
        await Assert.ThrowsAsync<MemoryScopeException>(() => harness.Store.WriteAsync(widened, new NewMemoryEntry("note", SentinelContent, [SentinelTag])));
        await harness.Store.DeleteAsync(scope, first.Id);

        var tags = harness.Telemetry.AllTags;
        Assert.NotEmpty(tags);
        Assert.Equal(
            ["memory.delete", "memory.escalate", "memory.get", "memory.read", "memory.summarize", "memory.write"],
            harness.Telemetry.Activities.Select(activity => activity.OperationName).Distinct().Order(StringComparer.Ordinal));
        Assert.All(tags, tag => Assert.Contains(tag.Key, AllowedTags));
        foreach (var sentinel in new[] { SentinelContent, SentinelTag, SentinelQuery })
        {
            Assert.DoesNotContain(tags, tag => (tag.Value?.ToString() ?? string.Empty).Contains(sentinel, StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(harness.LogLines, line => line.Contains(sentinel, StringComparison.OrdinalIgnoreCase));
        }

        Assert.Contains(tags, tag => tag.Key == "memory.tag_count");
        Assert.Contains(tags, tag => tag.Key == "memory.entry_size");
    }

    /// <summary>Operational reads carry only allowlisted tags too.</summary>
    [Fact]
    public async Task Operational_read_tags_are_allowlisted()
    {
        var harness = new MemoryHarness();

        await harness.Resolver.RequestOperationalScopeAsync(new MemoryOperationalReadRequest("operator-1", [Key(Project1)], "audit", harness.Time.GetUtcNow().AddHours(1), "corr-1"));

        var activity = Assert.Single(harness.Telemetry.Activities);
        Assert.Equal("memory.operational_read", activity.OperationName);
        Assert.All(activity.TagObjects, tag => Assert.Contains(tag.Key, AllowedTags));
        Assert.Contains(activity.TagObjects, tag => tag.Key == "memory.approval_status" && Equals(tag.Value, "Approved"));
    }
}
