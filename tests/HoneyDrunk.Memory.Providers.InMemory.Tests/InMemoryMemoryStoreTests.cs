using HoneyDrunk.Memory.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using static HoneyDrunk.Memory.Providers.InMemory.Tests.TestScopes;

namespace HoneyDrunk.Memory.Providers.InMemory.Tests;

/// <summary>Behavior of the in-memory store.</summary>
public sealed class InMemoryMemoryStoreTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 7, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider time = new(Start);

    /// <summary>A write gets an id, the store's time, the home key and no embedding.</summary>
    [Fact]
    public async Task Write_assigns_id_time_and_home_scope()
    {
        var store = new InMemoryMemoryStore(this.time);
        var home = Key();

        var entry = await store.WriteAsync(Writable(home), Note("first", "alice"));

        Assert.Equal(32, entry.Id.Length);
        Assert.Equal(home, entry.Scope);
        Assert.Equal(Start, entry.WrittenAt);
        Assert.Equal("first", entry.Content);
        Assert.Equal(["alice"], entry.Tags);
        Assert.Null(entry.EmbeddingModelId);
        Assert.Empty(entry.SummaryOf);
        Assert.Null(entry.SupersededBy);
        Assert.Equal(entry, await store.GetAsync(Writable(home), entry.Id));
    }

    /// <summary>Reads come newest first; entries written at the same instant order by id descending.</summary>
    [Fact]
    public async Task Read_returns_newest_first_with_id_tiebreak()
    {
        var store = new InMemoryMemoryStore(this.time);
        var scope = Writable(Key());
        var older = await store.WriteAsync(scope, Note("older"));
        this.time.Advance(TimeSpan.FromMinutes(1));
        var same1 = await store.WriteAsync(scope, Note("same-1"));
        var same2 = await store.WriteAsync(scope, Note("same-2"));

        var read = await store.ReadAsync(scope, new MemoryQuery());

        var tied = new[] { same1, same2 }.OrderByDescending(entry => entry.Id, StringComparer.Ordinal).ToArray();
        Assert.Equal([tied[0].Id, tied[1].Id, older.Id], read.Select(entry => entry.Id));
    }

    /// <summary>The tag filter needs every tag, and the kind filter matches exactly.</summary>
    [Fact]
    public async Task Read_filters_by_all_tags_and_kind()
    {
        var store = new InMemoryMemoryStore(this.time);
        var scope = Writable(Key());
        var both = await store.WriteAsync(scope, Note("both", "alice", "project-1"));
        await store.WriteAsync(scope, Note("alice only", "alice"));
        var decision = await store.WriteAsync(scope, new NewMemoryEntry("decision", "a decision", ["alice", "project-1"]));

        var tagged = await store.ReadAsync(scope, new MemoryQuery(AllTags: ["ALICE", "project-1"]));
        var notes = await store.ReadAsync(scope, new MemoryQuery(Kind: "note", AllTags: ["project-1"]));

        Assert.Equal(new[] { both.Id, decision.Id }.Order(StringComparer.Ordinal), tagged.Select(entry => entry.Id).Order(StringComparer.Ordinal));
        Assert.Equal([both.Id], notes.Select(entry => entry.Id));
    }

    /// <summary>The written-before filter is strict and the limit caps the result.</summary>
    [Fact]
    public async Task Read_respects_written_before_and_limit()
    {
        var store = new InMemoryMemoryStore(this.time);
        var scope = Writable(Key());
        var written = new List<MemoryEntry>();
        for (var i = 0; i < 5; i++)
        {
            written.Add(await store.WriteAsync(scope, Note($"entry {i}")));
            this.time.Advance(TimeSpan.FromMinutes(1));
        }

        var before = await store.ReadAsync(scope, new MemoryQuery(WrittenBefore: written[3].WrittenAt));
        var limited = await store.ReadAsync(scope, new MemoryQuery(Limit: 2));

        Assert.Equal([written[2].Id, written[1].Id, written[0].Id], before.Select(entry => entry.Id));
        Assert.Equal([written[4].Id, written[3].Id], limited.Select(entry => entry.Id));
    }

    /// <summary>Keys that differ only in tenant, project or agent never see each other's entries.</summary>
    [Fact]
    public async Task Scope_isolation_across_tenant_project_and_agent()
    {
        var store = new InMemoryMemoryStore(this.time);
        var home = Key();
        var otherTenant = Key(tenant: TenantB);
        var otherProject = Key(project: ProjectB);
        var otherAgent = Key("agent-b");
        var mine = await store.WriteAsync(Writable(home), Note("mine"));
        foreach (var key in new[] { otherTenant, otherProject, otherAgent })
        {
            await store.WriteAsync(Writable(key), Note("theirs " + key.AgentId));
        }

        var read = await store.ReadAsync(Writable(home), new MemoryQuery(Limit: 200, IncludeSuperseded: true));

        Assert.Equal([mine.Id], read.Select(entry => entry.Id));
        foreach (var key in new[] { otherTenant, otherProject, otherAgent })
        {
            Assert.Single(await store.ReadAsync(Writable(key), new MemoryQuery()));
        }
    }

    /// <summary>A foreign id and a missing id both read as null.</summary>
    [Fact]
    public async Task Get_of_foreign_id_returns_null()
    {
        var store = new InMemoryMemoryStore(this.time);
        var theirs = await store.WriteAsync(Writable(Key("agent-b")), Note("theirs"));

        Assert.Null(await store.GetAsync(Writable(Key()), theirs.Id));
        Assert.Null(await store.GetAsync(Writable(Key()), "0123456789abcdef0123456789abcdef"));
    }

    /// <summary>A scope with several readable keys sees them merged in the pinned order.</summary>
    [Fact]
    public async Task Readable_scopes_merge_in_order()
    {
        var store = new InMemoryMemoryStore(this.time);
        var home = Key();
        var other = Key("agent-b");
        var hidden = Key("agent-c");
        var a1 = await store.WriteAsync(Writable(home), Note("a1"));
        this.time.Advance(TimeSpan.FromSeconds(1));
        var b1 = await store.WriteAsync(Writable(other), Note("b1"));
        this.time.Advance(TimeSpan.FromSeconds(1));
        var a2 = await store.WriteAsync(Writable(home), Note("a2"));
        await store.WriteAsync(Writable(hidden), Note("c1"));

        var read = await store.ReadAsync(ReadOnly(home, other), new MemoryQuery());

        Assert.Equal([a2.Id, b1.Id, a1.Id], read.Select(entry => entry.Id));
        Assert.Equal(b1, await store.GetAsync(ReadOnly(home, other), b1.Id));
    }

    /// <summary>A summary links to its sources and each source is marked superseded, in one step.</summary>
    [Fact]
    public async Task Summary_supersedes_atomically()
    {
        var store = new InMemoryMemoryStore(this.time);
        var scope = Writable(Key());
        var first = await store.WriteAsync(scope, Note("first"));
        var second = await store.WriteAsync(scope, Note("second"));
        this.time.Advance(TimeSpan.FromMinutes(1));

        var summary = await store.WriteSummaryAsync(scope, new NewMemoryEntry("summary", "both", []), [first.Id, second.Id]);

        Assert.Equal([first.Id, second.Id], summary.SummaryOf);
        Assert.Null(summary.EmbeddingModelId);
        Assert.Equal(summary.Id, (await store.GetAsync(scope, first.Id))!.SupersededBy);
        Assert.Equal(summary.Id, (await store.GetAsync(scope, second.Id))!.SupersededBy);
    }

    /// <summary>A foreign, missing or already superseded id fails the summary and changes nothing.</summary>
    [Fact]
    public async Task Summary_with_foreign_or_superseded_id_changes_nothing()
    {
        var store = new InMemoryMemoryStore(this.time);
        var scope = Writable(Key());
        var mine = await store.WriteAsync(scope, Note("mine"));
        var theirs = await store.WriteAsync(Writable(Key("agent-b")), Note("theirs"));
        var done = await store.WriteAsync(scope, Note("done"));
        await store.WriteSummaryAsync(scope, new NewMemoryEntry("summary", "done", []), [done.Id]);
        var before = await store.ReadAsync(scope, new MemoryQuery(Limit: 200, IncludeSuperseded: true));

        await Assert.ThrowsAsync<ArgumentException>(() => store.WriteSummaryAsync(scope, new NewMemoryEntry("summary", "x", []), [mine.Id, theirs.Id]));
        await Assert.ThrowsAsync<ArgumentException>(() => store.WriteSummaryAsync(scope, new NewMemoryEntry("summary", "x", []), [mine.Id, "missing"]));
        await Assert.ThrowsAsync<ArgumentException>(() => store.WriteSummaryAsync(scope, new NewMemoryEntry("summary", "x", []), [mine.Id, done.Id]));
        await Assert.ThrowsAsync<ArgumentException>(() => store.WriteSummaryAsync(scope, new NewMemoryEntry("summary", "x", []), [mine.Id, mine.Id]));
        await Assert.ThrowsAsync<ArgumentException>(() => store.WriteSummaryAsync(scope, new NewMemoryEntry("summary", "x", []), []));

        Assert.Equal(before, await store.ReadAsync(scope, new MemoryQuery(Limit: 200, IncludeSuperseded: true)));
        Assert.Null((await store.GetAsync(Writable(Key("agent-b")), theirs.Id))!.SupersededBy);
    }

    /// <summary>Superseded entries are hidden from reads unless asked for.</summary>
    [Fact]
    public async Task Superseded_entries_hidden_unless_requested()
    {
        var store = new InMemoryMemoryStore(this.time);
        var scope = Writable(Key());
        var original = await store.WriteAsync(scope, Note("original"));
        var summary = await store.WriteSummaryAsync(scope, new NewMemoryEntry("summary", "short", []), [original.Id]);

        Assert.Equal([summary.Id], (await store.ReadAsync(scope, new MemoryQuery())).Select(entry => entry.Id));
        Assert.Equal(2, (await store.ReadAsync(scope, new MemoryQuery(IncludeSuperseded: true))).Count);
    }

    /// <summary>A read-only scope cannot write, summarize or delete.</summary>
    [Fact]
    public async Task Read_only_scope_cannot_write_or_delete()
    {
        var store = new InMemoryMemoryStore(this.time);
        var home = Key();
        var entry = await store.WriteAsync(Writable(home), Note("kept"));
        var readOnly = ReadOnly(home);

        await Assert.ThrowsAsync<MemoryScopeException>(() => store.WriteAsync(readOnly, Note("no")));
        await Assert.ThrowsAsync<MemoryScopeException>(() => store.WriteSummaryAsync(readOnly, Note("no"), [entry.Id]));
        await Assert.ThrowsAsync<MemoryScopeException>(() => store.DeleteAsync(readOnly, entry.Id));
        await Assert.ThrowsAsync<MemoryScopeException>(() => store.DeleteAllAsync(readOnly));
        Assert.NotNull(await store.GetAsync(readOnly, entry.Id));
    }

    /// <summary>Deletes act on the home key only.</summary>
    [Fact]
    public async Task Delete_all_clears_only_home()
    {
        var store = new InMemoryMemoryStore(this.time);
        var home = Writable(Key());
        var other = Writable(Key("agent-b"));
        var first = await store.WriteAsync(home, Note("one"));
        await store.WriteAsync(home, Note("two"));
        var theirs = await store.WriteAsync(other, Note("theirs"));

        Assert.False(await store.DeleteAsync(home, theirs.Id));
        Assert.True(await store.DeleteAsync(home, first.Id));
        Assert.False(await store.DeleteAsync(home, first.Id));
        Assert.Equal(1, await store.DeleteAllAsync(home));
        Assert.Empty(await store.ReadAsync(home, new MemoryQuery()));
        Assert.NotNull(await store.GetAsync(other, theirs.Id));
    }

    /// <summary>Two hundred parallel writes all land with distinct ids.</summary>
    [Fact]
    public async Task Parallel_writes_all_land()
    {
        var store = new InMemoryMemoryStore(this.time);
        var scope = Writable(Key());

        var written = await Task.WhenAll(Enumerable.Range(0, 200).Select(i => Task.Run(() => store.WriteAsync(scope, Note($"entry {i}")))));

        Assert.Equal(200, written.Select(entry => entry.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(200, (await store.ReadAsync(scope, new MemoryQuery(Limit: 200))).Count);
    }

    /// <summary>A cancelled token is observed before any change.</summary>
    [Fact]
    public async Task Cancellation_is_observed()
    {
        var store = new InMemoryMemoryStore(this.time);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.WriteAsync(Writable(Key()), Note("no"), cancelled.Token));
        Assert.Empty(await store.ReadAsync(Writable(Key()), new MemoryQuery()));
    }

    private static NewMemoryEntry Note(string content, params string[] tags) => new("note", content, tags);
}
