using HoneyDrunk.Memory.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using static HoneyDrunk.Memory.Providers.Contract.TestScopes;

namespace HoneyDrunk.Memory.Providers.Contract;

/// <summary>
/// The behavior every <see cref="IMemoryStore"/> provider must show. The in-memory provider is the
/// reference: a provider passes when it gives the same answers.
/// </summary>
public abstract class MemoryStoreContract
{
    /// <summary>The instant the test clock starts at.</summary>
    protected static readonly DateTimeOffset Start = new(2026, 11, 7, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Gets the test clock.</summary>
    protected FakeTimeProvider Time { get; } = new(Start);

    /// <summary>A write gets an id, the store's time, the home key and no embedding.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Write_assigns_id_time_and_home_scope()
    {
        var store = this.CreateStore(this.Time);
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
        AssertSameEntry(entry, await store.GetAsync(Writable(home), entry.Id));
    }

    /// <summary>Reads come newest first; entries written at the same instant order by id descending.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Read_returns_newest_first_with_id_tiebreak()
    {
        var store = this.CreateStore(this.Time);
        var scope = Writable(Key());
        var older = await store.WriteAsync(scope, Note("older"));
        this.Time.Advance(TimeSpan.FromMinutes(1));
        var same1 = await store.WriteAsync(scope, Note("same-1"));
        var same2 = await store.WriteAsync(scope, Note("same-2"));

        var read = await store.ReadAsync(scope, new MemoryQuery());

        var tied = new[] { same1, same2 }.OrderByDescending(entry => entry.Id, StringComparer.Ordinal).ToArray();
        Assert.Equal([tied[0].Id, tied[1].Id, older.Id], read.Select(entry => entry.Id));
    }

    /// <summary>The tag filter needs every tag, and the kind filter matches exactly.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Read_filters_by_all_tags_and_kind()
    {
        var store = this.CreateStore(this.Time);
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
    /// <returns>A task.</returns>
    [Fact]
    public async Task Read_respects_written_before_and_limit()
    {
        var store = this.CreateStore(this.Time);
        var scope = Writable(Key());
        var written = new List<MemoryEntry>();
        for (var i = 0; i < 5; i++)
        {
            written.Add(await store.WriteAsync(scope, Note($"entry {i}")));
            this.Time.Advance(TimeSpan.FromMinutes(1));
        }

        var before = await store.ReadAsync(scope, new MemoryQuery(WrittenBefore: written[3].WrittenAt));
        var limited = await store.ReadAsync(scope, new MemoryQuery(Limit: 2));

        Assert.Equal([written[2].Id, written[1].Id, written[0].Id], before.Select(entry => entry.Id));
        Assert.Equal([written[4].Id, written[3].Id], limited.Select(entry => entry.Id));
    }

    /// <summary>Keys that differ only in tenant, project or agent never see each other's entries.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Scope_isolation_across_tenant_project_and_agent()
    {
        var store = this.CreateStore(this.Time);
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
    /// <returns>A task.</returns>
    [Fact]
    public async Task Get_of_foreign_id_returns_null()
    {
        var store = this.CreateStore(this.Time);
        var theirs = await store.WriteAsync(Writable(Key("agent-b")), Note("theirs"));

        Assert.Null(await store.GetAsync(Writable(Key()), theirs.Id));
        Assert.Null(await store.GetAsync(Writable(Key()), "0123456789abcdef0123456789abcdef"));
    }

    /// <summary>A delete through another key's scope removes nothing.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Delete_of_foreign_id_returns_false_and_keeps_it()
    {
        var store = this.CreateStore(this.Time);
        var other = Writable(Key("agent-b"));
        var theirs = await store.WriteAsync(other, Note("theirs", "kept"));

        Assert.False(await store.DeleteAsync(Writable(Key()), theirs.Id));

        AssertSameEntry(theirs, await store.GetAsync(other, theirs.Id));
    }

    /// <summary>A scope with several readable keys sees them merged in the pinned order.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Readable_scopes_merge_in_order()
    {
        var store = this.CreateStore(this.Time);
        var home = Key();
        var other = Key("agent-b");
        var hidden = Key("agent-c");
        var a1 = await store.WriteAsync(Writable(home), Note("a1"));
        this.Time.Advance(TimeSpan.FromSeconds(1));
        var b1 = await store.WriteAsync(Writable(other), Note("b1"));
        this.Time.Advance(TimeSpan.FromSeconds(1));
        var a2 = await store.WriteAsync(Writable(home), Note("a2"));
        await store.WriteAsync(Writable(hidden), Note("c1"));

        var read = await store.ReadAsync(ReadOnly(home, other), new MemoryQuery());

        Assert.Equal([a2.Id, b1.Id, a1.Id], read.Select(entry => entry.Id));
        AssertSameEntry(b1, await store.GetAsync(ReadOnly(home, other), b1.Id));
    }

    /// <summary>A read across several keys returns the newest entries of all of them, each once.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Read_across_keys_returns_the_newest_of_all()
    {
        var store = this.CreateStore(this.Time);
        var keys = new[] { Key(), Key("agent-b"), Key("agent-c") };
        var written = new List<MemoryEntry>();
        for (var round = 0; round < 2; round++)
        {
            foreach (var key in keys)
            {
                written.Add(await store.WriteAsync(Writable(key), Note($"{key.AgentId} {round}")));
                this.Time.Advance(TimeSpan.FromSeconds(1));
            }
        }

        var newest = await store.ReadAsync(ReadOnly(keys[0], keys[1], keys[2]), new MemoryQuery(Limit: 2));
        var repeated = await store.ReadAsync(ReadOnly(keys[0], keys[0], keys[1]), new MemoryQuery(Limit: 200));

        Assert.Equal([written[5].Id, written[4].Id], newest.Select(entry => entry.Id));
        Assert.Equal([written[4].Id, written[3].Id, written[1].Id, written[0].Id], repeated.Select(entry => entry.Id));
    }

    /// <summary>A summary links to its sources and each source is marked superseded, in one step.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Summary_supersedes_atomically()
    {
        var store = this.CreateStore(this.Time);
        var scope = Writable(Key());
        var first = await store.WriteAsync(scope, Note("first"));
        var second = await store.WriteAsync(scope, Note("second"));
        this.Time.Advance(TimeSpan.FromMinutes(1));

        var summary = await store.WriteSummaryAsync(scope, new NewMemoryEntry("summary", "both", []), [first.Id, second.Id]);

        Assert.Equal([first.Id, second.Id], summary.SummaryOf);
        Assert.Null(summary.EmbeddingModelId);
        Assert.Equal(summary.Id, (await store.GetAsync(scope, first.Id))!.SupersededBy);
        Assert.Equal(summary.Id, (await store.GetAsync(scope, second.Id))!.SupersededBy);
    }

    /// <summary>A summary keeps its sources in the order they were given.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Summary_keeps_the_order_of_its_sources()
    {
        var store = this.CreateStore(this.Time);
        var scope = Writable(Key());
        var a = await store.WriteAsync(scope, Note("a"));
        var b = await store.WriteAsync(scope, Note("b"));
        var c = await store.WriteAsync(scope, Note("c"));

        var summary = await store.WriteSummaryAsync(scope, new NewMemoryEntry("summary", "all", ["digest"]), [c.Id, a.Id, b.Id]);

        Assert.Equal([c.Id, a.Id, b.Id], summary.SummaryOf);
        AssertSameEntry(summary, await store.GetAsync(scope, summary.Id));
    }

    /// <summary>An empty list, a repeated id and a null id are refused by name.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Summary_arguments_are_checked()
    {
        var store = this.CreateStore(this.Time);
        var scope = Writable(Key());
        var entry = await store.WriteAsync(scope, Note("kept"));
        var summary = new NewMemoryEntry("summary", "x", []);

        var empty = await Assert.ThrowsAsync<ArgumentException>(() => store.WriteSummaryAsync(scope, summary, []));
        var repeated = await Assert.ThrowsAsync<ArgumentException>(() => store.WriteSummaryAsync(scope, summary, [entry.Id, entry.Id]));
        var withNull = await Assert.ThrowsAsync<ArgumentException>(() => store.WriteSummaryAsync(scope, summary, [entry.Id, null!]));

        Assert.Equal("supersededIds", empty.ParamName);
        Assert.Equal("supersededIds", repeated.ParamName);
        Assert.Equal("supersededIds", withNull.ParamName);
        Assert.Null((await store.GetAsync(scope, entry.Id))!.SupersededBy);
    }

    /// <summary>A foreign, missing or already superseded id fails the summary and changes nothing.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Summary_with_foreign_or_superseded_id_changes_nothing()
    {
        var store = this.CreateStore(this.Time);
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

        AssertSameEntries(before, await store.ReadAsync(scope, new MemoryQuery(Limit: 200, IncludeSuperseded: true)));
        Assert.Null((await store.GetAsync(Writable(Key("agent-b")), theirs.Id))!.SupersededBy);
    }

    /// <summary>Superseded entries are hidden from reads unless asked for.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Superseded_entries_hidden_unless_requested()
    {
        var store = this.CreateStore(this.Time);
        var scope = Writable(Key());
        var original = await store.WriteAsync(scope, Note("original"));
        var summary = await store.WriteSummaryAsync(scope, new NewMemoryEntry("summary", "short", []), [original.Id]);

        Assert.Equal([summary.Id], (await store.ReadAsync(scope, new MemoryQuery())).Select(entry => entry.Id));
        Assert.Equal(2, (await store.ReadAsync(scope, new MemoryQuery(IncludeSuperseded: true))).Count);
    }

    /// <summary>Deleting a summary does not bring its sources back: they keep its id and stay hidden.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Deleting_a_summary_leaves_its_sources_superseded()
    {
        var store = this.CreateStore(this.Time);
        var scope = Writable(Key());
        var source = await store.WriteAsync(scope, Note("source"));
        var summary = await store.WriteSummaryAsync(scope, new NewMemoryEntry("summary", "short", []), [source.Id]);

        Assert.True(await store.DeleteAsync(scope, summary.Id));

        Assert.Equal(summary.Id, (await store.GetAsync(scope, source.Id))!.SupersededBy);
        Assert.Empty(await store.ReadAsync(scope, new MemoryQuery()));
        Assert.Equal([source.Id], (await store.ReadAsync(scope, new MemoryQuery(IncludeSuperseded: true))).Select(entry => entry.Id));
    }

    /// <summary>Deleting a summarized entry leaves the summary and its list of sources as written.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Deleting_a_source_keeps_the_summary_and_its_list()
    {
        var store = this.CreateStore(this.Time);
        var scope = Writable(Key());
        var a = await store.WriteAsync(scope, Note("a"));
        var b = await store.WriteAsync(scope, Note("b"));
        var summary = await store.WriteSummaryAsync(scope, new NewMemoryEntry("summary", "both", []), [a.Id, b.Id]);

        Assert.True(await store.DeleteAsync(scope, a.Id));

        Assert.Equal([a.Id, b.Id], (await store.GetAsync(scope, summary.Id))!.SummaryOf);
        Assert.Equal(summary.Id, (await store.GetAsync(scope, b.Id))!.SupersededBy);
    }

    /// <summary>A read-only scope cannot write, summarize or delete.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Read_only_scope_cannot_write_or_delete()
    {
        var store = this.CreateStore(this.Time);
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
    /// <returns>A task.</returns>
    [Fact]
    public async Task Delete_all_clears_only_home()
    {
        var store = this.CreateStore(this.Time);
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
    /// <returns>A task.</returns>
    [Fact]
    public async Task Parallel_writes_all_land()
    {
        var store = this.CreateStore(this.Time);
        var scope = Writable(Key());

        var written = await Task.WhenAll(Enumerable.Range(0, 200).Select(i => Task.Run(() => store.WriteAsync(scope, Note($"entry {i}")))));

        Assert.Equal(200, written.Select(entry => entry.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(200, (await store.ReadAsync(scope, new MemoryQuery(Limit: 200))).Count);
    }

    /// <summary>A cancelled token is observed before any change.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Cancellation_is_observed()
    {
        var store = this.CreateStore(this.Time);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.WriteAsync(Writable(Key()), Note("no"), cancelled.Token));
        Assert.Empty(await store.ReadAsync(Writable(Key()), new MemoryQuery()));
    }

    /// <summary>Content at the length limit comes back character for character.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Content_round_trips_verbatim_at_the_limit()
    {
        const string head = "\nfirst line\r\nsecond \U0001F600 line\t";
        const string tail = "   ";
        var content = head + new string('x', 16_000 - head.Length - tail.Length) + tail;
        var store = this.CreateStore(this.Time);
        var scope = Writable(Key());

        var written = await store.WriteAsync(scope, Note(content));

        Assert.Equal(16_000, content.Length);
        Assert.Equal(content, written.Content);
        Assert.Equal(content, (await store.GetAsync(scope, written.Id))!.Content);
        Assert.Equal(content, Assert.Single(await store.ReadAsync(scope, new MemoryQuery())).Content);
    }

    /// <summary>Tags match by exact value, with no folding of accents or letters, and keep their order.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Tags_match_exactly()
    {
        var store = this.CreateStore(this.Time);
        var scope = Writable(Key());
        var tags = new[] { "straße", "strasse", "é", "e" };
        var byTag = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var tag in tags)
        {
            byTag[tag] = (await store.WriteAsync(scope, Note("tagged", tag))).Id;
        }

        var ordered = await store.WriteAsync(scope, Note("ordered", "zeta", "alpha", "mid"));

        foreach (var tag in tags)
        {
            var found = await store.ReadAsync(scope, new MemoryQuery(AllTags: [tag]));
            Assert.Equal([byTag[tag]], found.Select(entry => entry.Id));
        }

        Assert.Equal(["zeta", "alpha", "mid"], (await store.GetAsync(scope, ordered.Id))!.Tags);
    }

    /// <summary>An id that no entry can have matches nothing, however close it is to a real one.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Ids_that_cannot_exist_match_nothing()
    {
        var store = this.CreateStore(this.Time);
        var scope = Writable(Key());
        var real = await store.WriteAsync(scope, Note("real"));
        var impossible = new[] { real.Id.ToUpperInvariant(), real.Id + "0", real.Id[..31], string.Empty, "%", new string('a', 5000) }
            .Where(id => !string.Equals(id, real.Id, StringComparison.Ordinal));

        foreach (var id in impossible)
        {
            Assert.Null(await store.GetAsync(scope, id));
            Assert.False(await store.DeleteAsync(scope, id));
            var refused = await Assert.ThrowsAsync<ArgumentException>(() => store.WriteSummaryAsync(scope, new NewMemoryEntry("summary", "x", []), [id]));
            Assert.Equal("supersededIds", refused.ParamName);
        }

        AssertSameEntry(real, await store.GetAsync(scope, real.Id));
    }

    /// <summary>An empty tag filter filters nothing.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Empty_tag_filter_matches_everything()
    {
        var store = this.CreateStore(this.Time);
        var scope = Writable(Key());
        await store.WriteAsync(scope, Note("tagged", "alice"));
        await store.WriteAsync(scope, Note("plain"));

        Assert.Equal(2, (await store.ReadAsync(scope, new MemoryQuery(AllTags: []))).Count);
    }

    /// <summary>The written-before filter compares instants, whatever offset the caller wrote them with.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Written_before_ignores_the_offset()
    {
        var store = this.CreateStore(this.Time);
        var scope = Writable(Key());
        var first = await store.WriteAsync(scope, Note("first"));
        this.Time.Advance(TimeSpan.FromMinutes(1));
        var second = await store.WriteAsync(scope, Note("second"));

        var read = await store.ReadAsync(scope, new MemoryQuery(WrittenBefore: second.WrittenAt.ToOffset(TimeSpan.FromHours(2))));

        Assert.Equal([first.Id], read.Select(entry => entry.Id));
    }

    /// <summary>Every null argument is refused before any work.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Null_arguments_are_refused()
    {
        var store = this.CreateStore(this.Time);
        var scope = Writable(Key());
        var entry = await store.WriteAsync(scope, Note("kept"));

        await Assert.ThrowsAsync<ArgumentNullException>(() => store.WriteAsync(null!, Note("x")));
        await Assert.ThrowsAsync<ArgumentNullException>(() => store.WriteAsync(scope, null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => store.GetAsync(null!, entry.Id));
        await Assert.ThrowsAsync<ArgumentNullException>(() => store.GetAsync(scope, null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => store.ReadAsync(null!, new MemoryQuery()));
        await Assert.ThrowsAsync<ArgumentNullException>(() => store.ReadAsync(scope, null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => store.WriteSummaryAsync(null!, Note("x"), [entry.Id]));
        await Assert.ThrowsAsync<ArgumentNullException>(() => store.WriteSummaryAsync(scope, null!, [entry.Id]));
        await Assert.ThrowsAsync<ArgumentNullException>(() => store.WriteSummaryAsync(scope, Note("x"), null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => store.DeleteAsync(null!, entry.Id));
        await Assert.ThrowsAsync<ArgumentNullException>(() => store.DeleteAsync(scope, null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => store.DeleteAllAsync(null!));
        Assert.Single(await store.ReadAsync(scope, new MemoryQuery()));
    }

    /// <summary>Asserts that two entries hold the same values, comparing their lists item by item.</summary>
    /// <param name="expected">The expected entry.</param>
    /// <param name="actual">The actual entry.</param>
    protected static void AssertSameEntry(MemoryEntry expected, MemoryEntry? actual)
    {
        ArgumentNullException.ThrowIfNull(expected);
        Assert.NotNull(actual);
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Scope, actual.Scope);
        Assert.Equal(expected.Kind, actual.Kind);
        Assert.Equal(expected.Content, actual.Content);
        Assert.Equal(expected.Tags, actual.Tags);
        Assert.Equal(expected.WrittenAt, actual.WrittenAt);
        Assert.Equal(expected.EmbeddingModelId, actual.EmbeddingModelId);
        Assert.Equal(expected.SummaryOf, actual.SummaryOf);
        Assert.Equal(expected.SupersededBy, actual.SupersededBy);
    }

    /// <summary>Asserts that two lists hold the same entries in the same order.</summary>
    /// <param name="expected">The expected entries.</param>
    /// <param name="actual">The actual entries.</param>
    protected static void AssertSameEntries(IReadOnlyList<MemoryEntry> expected, IReadOnlyList<MemoryEntry> actual)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);
        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            AssertSameEntry(expected[i], actual[i]);
        }
    }

    /// <summary>Creates a note entry.</summary>
    /// <param name="content">The content.</param>
    /// <param name="tags">The tags.</param>
    /// <returns>The entry to write.</returns>
    protected static NewMemoryEntry Note(string content, params string[] tags) => new("note", content, tags);

    /// <summary>
    /// Creates a store over this test's storage. For a durable provider, a second call returns a
    /// second store over the same database.
    /// </summary>
    /// <param name="timeProvider">The clock the store reads.</param>
    /// <returns>The store.</returns>
    protected abstract IMemoryStore CreateStore(TimeProvider timeProvider);
}
