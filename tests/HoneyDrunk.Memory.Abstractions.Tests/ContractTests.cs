using HoneyDrunk.Kernel.Abstractions.Identity;
using System.Reflection;
using Xunit;

namespace HoneyDrunk.Memory.Abstractions.Tests;

/// <summary>Validation of the contract records and the shape of the store.</summary>
public sealed class ContractTests
{
    private static readonly DateTimeOffset Expiry = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A purpose that spans lines, is blank or is too long is rejected.</summary>
    /// <param name="purpose">The purpose.</param>
    [Theory]
    [InlineData("line one\nline two")]
    [InlineData("line one\rline two")]
    [InlineData("line one{LS}line two")]
    [InlineData("")]
    [InlineData("   ")]
    public void Operational_request_rejects_multiline_purpose(string purpose)
    {
        var text = purpose.Replace("{LS}", ((char)0x2028).ToString(), StringComparison.Ordinal);

        Assert.ThrowsAny<ArgumentException>(() => new MemoryOperationalReadRequest("operator-1", [Key("agent-a")], text, Expiry, "corr-1"));
    }

    /// <summary>A purpose over 200 characters is rejected; 200 is accepted.</summary>
    [Fact]
    public void Operational_request_rejects_purpose_over_200()
    {
        Assert.ThrowsAny<ArgumentException>(() => new MemoryOperationalReadRequest("operator-1", [Key("agent-a")], new string('p', 201), Expiry, "corr-1"));
        Assert.Equal(200, new MemoryOperationalReadRequest("operator-1", [Key("agent-a")], new string('p', 200), Expiry, "corr-1").Purpose.Length);
    }

    /// <summary>The request needs 1 to 50 keys.</summary>
    [Fact]
    public void Operational_request_rejects_empty_and_over_fifty_scopes()
    {
        var fiftyOne = Enumerable.Range(0, 51).Select(i => Key($"agent-{i}")).ToArray();
        var fifty = fiftyOne.Take(50).ToArray();

        Assert.ThrowsAny<ArgumentException>(() => new MemoryOperationalReadRequest("operator-1", [], "audit", Expiry, "corr-1"));
        Assert.ThrowsAny<ArgumentException>(() => new MemoryOperationalReadRequest("operator-1", fiftyOne, "audit", Expiry, "corr-1"));
        Assert.Equal(50, new MemoryOperationalReadRequest("operator-1", fifty, "audit", Expiry, "corr-1").Scopes.Count);
    }

    /// <summary>Duplicate keys collapse in first order, and an uninitialized key is rejected.</summary>
    [Fact]
    public void Operational_request_deduplicates_and_rejects_default_keys()
    {
        var request = new MemoryOperationalReadRequest("operator-1", [Key("agent-b"), Key("agent-a"), Key("agent-b")], "audit", Expiry, "corr-1");

        Assert.Equal([Key("agent-b"), Key("agent-a")], request.Scopes);
        Assert.ThrowsAny<ArgumentException>(() => new MemoryOperationalReadRequest("operator-1", [default], "audit", Expiry, "corr-1"));
        Assert.ThrowsAny<ArgumentException>(() => new MemoryOperationalReadRequest(" ", [Key("agent-a")], "audit", Expiry, "corr-1"));
        Assert.ThrowsAny<ArgumentException>(() => new MemoryOperationalReadRequest("operator-1", [Key("agent-a")], "audit", Expiry, " "));
    }

    /// <summary>
    /// Every validated list is a read-only copy: casting it back to a writable list cannot change
    /// what was validated (or, for an operational read, what was approved).
    /// </summary>
    [Fact]
    public void Validated_lists_cannot_be_written_through_a_cast()
    {
        var caller = new MemoryCaller("agent:agent-a", ["memory.read"], ["memory-reader"]);
        var summary = new MemorySummaryRequest(["a"], Tags: ["tag"]);
        var writable = new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["MemoryOperationalReadRequest.Scopes"] = CanWrite(new MemoryOperationalReadRequest("operator-1", [Key("agent-a")], "audit", Expiry, "corr-1").Scopes),
            ["MemoryCaller.Scopes"] = CanWrite(caller.Scopes),
            ["MemoryCaller.Roles"] = CanWrite(caller.Roles),
            ["NewMemoryEntry.Tags"] = CanWrite(new NewMemoryEntry("note", "text", ["tag"]).Tags),
            ["MemoryQuery.AllTags"] = CanWrite(new MemoryQuery(AllTags: ["tag"]).AllTags!),
            ["MemorySummaryRequest.EntryIds"] = CanWrite(summary.EntryIds),
            ["MemorySummaryRequest.Tags"] = CanWrite(summary.Tags!),
        };

        Assert.Empty(writable.Where(pair => pair.Value).Select(pair => pair.Key));
    }

    /// <summary>The store offers no similarity search in this release.</summary>
    [Fact]
    public void Store_has_no_similarity_member()
    {
        var names = typeof(IMemoryStore).GetMembers(BindingFlags.Public | BindingFlags.Instance).Select(member => member.Name).ToArray();

        Assert.NotEmpty(names);
        Assert.DoesNotContain(names, name => name.Contains("Similar", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Embedding", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Vector", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>A summary request needs distinct, non-blank ids and a valid kind.</summary>
    [Fact]
    public void Summary_request_validates_ids_and_kind()
    {
        Assert.Equal("summary", new MemorySummaryRequest(["a", "b"]).SummaryKind);
        Assert.ThrowsAny<ArgumentException>(() => new MemorySummaryRequest(["a", "a"]));
        Assert.ThrowsAny<ArgumentException>(() => new MemorySummaryRequest(["a", " "]));
        Assert.ThrowsAny<ArgumentException>(() => new MemorySummaryRequest(["a", "b"], "Bad Kind"));
    }

    /// <summary>An operational result carries a scope exactly when approved.</summary>
    [Fact]
    public void Operational_result_carries_a_scope_only_when_approved()
    {
        Assert.ThrowsAny<ArgumentException>(() => new MemoryOperationalScopeResult(MemoryApprovalStatus.Approved, "memory-read-1", null));
        Assert.Null(new MemoryOperationalScopeResult(MemoryApprovalStatus.Pending, "memory-read-1", null).Scope);
    }

    /// <summary>The denial exception names keys and is an authorization failure.</summary>
    [Fact]
    public void Escalation_denied_exception_names_keys()
    {
        var error = new MemoryEscalationDeniedException("Denied.", [Key("agent-b")]);

        Assert.IsAssignableFrom<UnauthorizedAccessException>(error);
        Assert.Equal([Key("agent-b")], error.DeniedScopes);
        Assert.IsAssignableFrom<InvalidOperationException>(new MemoryScopeException("Read-only."));
    }

    // True when a cast back to IList<T> can overwrite an item of the list.
    private static bool CanWrite<T>(IReadOnlyList<T> list)
    {
        if (list is not IList<T> items || items.Count == 0)
        {
            return false;
        }

        try
        {
            items[0] = items[0];
            return true;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    private static MemoryScopeKey Key(string agentId) =>
        new(new TenantId("01JC0000000000000000000001"), new ProjectId("01JC0000000000000000000002"), agentId);
}
