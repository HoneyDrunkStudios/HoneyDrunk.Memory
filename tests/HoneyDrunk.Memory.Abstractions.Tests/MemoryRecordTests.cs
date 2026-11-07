using HoneyDrunk.Kernel.Abstractions.Identity;
using Xunit;

namespace HoneyDrunk.Memory.Abstractions.Tests;

/// <summary>Validation and normalization of the scope key and entry records.</summary>
public sealed class MemoryRecordTests
{
    private const string Tenant = "01JC0000000000000000000001";
    private const string Project = "01JC0000000000000000000002";

    /// <summary>Agent ids that break the pinned format are rejected.</summary>
    /// <param name="agentId">The agent id.</param>
    [Theory]
    [InlineData("")]
    [InlineData("Upper")]
    [InlineData("-lead")]
    [InlineData("agent-a\n")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void ScopeKey_rejects_bad_agent_ids(string agentId)
    {
        Assert.ThrowsAny<ArgumentException>(() => new MemoryScopeKey(new TenantId(Tenant), new ProjectId(Project), agentId));
    }

    /// <summary>A null agent id is rejected.</summary>
    [Fact]
    public void ScopeKey_rejects_null_agent_id()
    {
        Assert.ThrowsAny<ArgumentException>(() => new MemoryScopeKey(new TenantId(Tenant), new ProjectId(Project), null!));
    }

    /// <summary>The key text is tenant, project and agent separated by slashes.</summary>
    [Fact]
    public void ScopeKey_formats_as_tenant_project_agent()
    {
        var key = new MemoryScopeKey(new TenantId(Tenant), new ProjectId(Project), "agent-a.1_b");

        Assert.Equal($"{Tenant}/{Project}/agent-a.1_b", key.ToString());
        Assert.Equal(key, new MemoryScopeKey(new TenantId(Tenant), new ProjectId(Project), "agent-a.1_b"));
    }

    /// <summary>Tags are trimmed, lower-cased and de-duplicated in first order.</summary>
    [Fact]
    public void NewEntry_normalizes_tags()
    {
        var entry = new NewMemoryEntry("note", "Some content.", [" Alice ", "alice", "Project-1", "ALICE"]);

        Assert.Equal(["alice", "project-1"], entry.Tags);
        Assert.Equal("Some content.", entry.Content);
    }

    /// <summary>Seventeen distinct tags exceed the limit.</summary>
    [Fact]
    public void NewEntry_rejects_seventeen_tags()
    {
        var tags = Enumerable.Range(0, 17).Select(i => $"tag-{i}").ToArray();

        Assert.ThrowsAny<ArgumentException>(() => new NewMemoryEntry("note", "content", tags));
    }

    /// <summary>Sixteen distinct tags are accepted.</summary>
    [Fact]
    public void NewEntry_accepts_sixteen_tags()
    {
        var tags = Enumerable.Range(0, 16).Select(i => $"tag-{i}").ToArray();

        Assert.Equal(16, new NewMemoryEntry("note", "content", tags).Tags.Count);
    }

    /// <summary>Tags with inner whitespace, control characters or bad lengths are rejected.</summary>
    /// <param name="tag">The tag.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("two words")]
    [InlineData("tab\tbed")]
    [InlineData("bell\u0007")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void NewEntry_rejects_bad_tags(string tag)
    {
        Assert.ThrowsAny<ArgumentException>(() => new NewMemoryEntry("note", "content", [tag]));
    }

    /// <summary>Empty or whitespace content is rejected.</summary>
    /// <param name="content">The content.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\r\n")]
    public void NewEntry_rejects_blank_content(string content)
    {
        Assert.ThrowsAny<ArgumentException>(() => new NewMemoryEntry("note", content, []));
    }

    /// <summary>Content over 16,000 characters is rejected; 16,000 is accepted verbatim.</summary>
    [Fact]
    public void NewEntry_rejects_content_over_16000()
    {
        Assert.ThrowsAny<ArgumentException>(() => new NewMemoryEntry("note", new string('x', 16_001), []));

        var content = "  " + new string('x', 15_998);
        Assert.Equal(content, new NewMemoryEntry("note", content, []).Content);
    }

    /// <summary>Kinds that break the pinned format are rejected.</summary>
    /// <param name="kind">The kind.</param>
    [Theory]
    [InlineData("")]
    [InlineData("Note")]
    [InlineData("1note")]
    [InlineData("has.dot")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void NewEntry_rejects_bad_kind(string kind)
    {
        Assert.ThrowsAny<ArgumentException>(() => new NewMemoryEntry(kind, "content", []));
    }

    /// <summary>The query limit is 1 to 200.</summary>
    /// <param name="limit">The limit.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    [InlineData(-1)]
    public void Query_rejects_limit_out_of_range(int limit)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MemoryQuery(limit));
    }

    /// <summary>The query defaults to twenty and normalizes its tags like entries do.</summary>
    [Fact]
    public void Query_defaults_and_normalizes_tags()
    {
        var query = new MemoryQuery(AllTags: [" Alice ", "alice"]);

        Assert.Equal(20, query.Limit);
        Assert.Equal(["alice"], query.AllTags);
        Assert.False(query.IncludeSuperseded);
    }

    /// <summary>The caller needs a subject and copies its lists.</summary>
    [Fact]
    public void Caller_rejects_blank_subject_and_copies_lists()
    {
        Assert.ThrowsAny<ArgumentException>(() => new MemoryCaller(" ", [], []));

        var scopes = new List<string> { "memory.read:a" };
        var caller = new MemoryCaller("agent:agent-a", scopes, []);
        scopes.Add("memory.read:b");

        Assert.Equal(["memory.read:a"], caller.Scopes);
    }
}
