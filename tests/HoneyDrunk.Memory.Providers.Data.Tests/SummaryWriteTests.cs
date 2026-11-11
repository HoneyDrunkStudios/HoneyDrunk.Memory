using HoneyDrunk.Memory.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using static HoneyDrunk.Memory.Providers.Contract.TestScopes;

namespace HoneyDrunk.Memory.Providers.Data.Tests;

/// <summary>A summary write is kept whole or not at all, whatever interrupts it.</summary>
public sealed class SummaryWriteTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 11, 7, 9, 0, 0, TimeSpan.Zero);
    private static readonly MemoryQuery Everything = new(Limit: 200, IncludeSuperseded: true);

    private readonly SqliteDatabase database = new();
    private readonly FakeTimeProvider time = new(Start);

    /// <summary>A caller that cancels after the sources are claimed leaves no claim and no summary.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Summary_cancelled_after_the_claim_changes_nothing()
    {
        var scope = Writable(Key());
        var sources = await this.WriteAsync(scope, 2);
        using var cancel = new CancelAfterFirstUpdate();
        var store = this.database.CreateStore(this.time, cancel);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.WriteSummaryAsync(scope, Summary(), Ids(sources), cancel.Cancellation.Token));

        await this.AssertUnchangedAsync(scope, sources);
    }

    /// <summary>A database failure while the summary is stored surfaces, and leaves no claim.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Summary_whose_insert_fails_changes_nothing()
    {
        var scope = Writable(Key());
        var sources = await this.WriteAsync(scope, 2);
        var fail = new FailInsertInto("EntrySummarySource") { Armed = true };
        var store = this.database.CreateStore(this.time, fail);

        var error = await Assert.ThrowsAnyAsync<Exception>(() => store.WriteSummaryAsync(scope, Summary(), Ids(sources)));

        Assert.IsNotType<ArgumentException>(error);
        Assert.Contains("insert into EntrySummarySource failed", Messages(error), StringComparison.Ordinal);
        await this.AssertUnchangedAsync(scope, sources);
    }

    /// <summary>
    /// One bad id among five refuses the summary and leaves the four good sources current. This fails
    /// if the claim on the good sources is committed.
    /// </summary>
    /// <param name="bad">The kind of bad id.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("missing")]
    [InlineData("foreign")]
    [InlineData("superseded")]
    public async Task Summary_with_one_bad_id_among_five_changes_nothing(string bad)
    {
        var scope = Writable(Key());
        var store = this.database.CreateStore(this.time);
        var sources = await this.WriteAsync(scope, 4);
        var badId = bad switch
        {
            "missing" => "0123456789abcdef0123456789abcdef",
            "foreign" => (await store.WriteAsync(Writable(Key("agent-b")), new NewMemoryEntry("note", "theirs", []))).Id,
            _ => await this.WriteSupersededAsync(scope),
        };
        var before = await store.ReadAsync(scope, Everything);
        var ids = new[] { sources[0].Id, sources[1].Id, badId, sources[2].Id, sources[3].Id };

        var refused = await Assert.ThrowsAsync<ArgumentException>(() => store.WriteSummaryAsync(scope, Summary(), ids));

        Assert.Equal("supersededIds", refused.ParamName);
        var after = await this.database.CreateStore(this.time).ReadAsync(scope, Everything);
        Assert.Equal(before.Select(Describe), after.Select(Describe));
        Assert.All(sources, source => Assert.Null(after.Single(entry => entry.Id == source.Id).SupersededBy));
    }

    /// <summary>Two hosts that summarize overlapping sources at once store one summary; the other is refused whole.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Concurrent_summaries_over_the_same_sources_store_one()
    {
        var scope = Writable(Key());
        for (var round = 0; round < 10; round++)
        {
            var sources = await this.WriteAsync(scope, 3);
            var first = this.database.CreateStore(this.time);
            var second = this.database.CreateStore(this.time);

            var outcomes = await Task.WhenAll(
                Task.Run(() => TryAsync(first, scope, [sources[0].Id, sources[1].Id])),
                Task.Run(() => TryAsync(second, scope, [sources[1].Id, sources[2].Id])));

            var winner = Assert.Single(outcomes, outcome => outcome is not null)!;
            var loserOnly = winner.SummaryOf.Contains(sources[0].Id) ? sources[2] : sources[0];
            var reader = this.database.CreateStore(this.time);
            foreach (var id in winner.SummaryOf)
            {
                Assert.Equal(winner.Id, (await reader.GetAsync(scope, id))!.SupersededBy);
            }

            Assert.Null((await reader.GetAsync(scope, loserOnly.Id))!.SupersededBy);
            Assert.Single(await reader.ReadAsync(scope, new MemoryQuery(Limit: 200, Kind: "summary", WrittenBefore: null, IncludeSuperseded: true)), entry => entry.SummaryOf.Contains(sources[1].Id));
        }
    }

    /// <summary>A write whose commit succeeded but whose reply was lost is not applied twice.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Lost_commit_reply_is_not_replayed()
    {
        var scope = Writable(Key());
        var lost = new LostCommitReply();
        var store = this.database.CreateStore(this.time, options =>
        {
            options.UseSqlite(this.database.ConnectionString, sqlite => sqlite.ExecutionStrategy(dependencies => new RetryOnLostReply(dependencies)));
            options.AddInterceptors(lost);
        });

        lost.Armed = true;
        var first = await store.WriteAsync(scope, new NewMemoryEntry("note", "one", ["kept"]));
        var second = await store.WriteAsync(scope, new NewMemoryEntry("note", "two", []));
        lost.Armed = true;
        var summary = await store.WriteSummaryAsync(scope, Summary(), [first.Id, second.Id]);

        Assert.False(lost.Armed);
        var all = await this.database.CreateStore(this.time).ReadAsync(scope, Everything);
        Assert.Equal(new[] { first.Id, second.Id, summary.Id }.Order(StringComparer.Ordinal), all.Select(entry => entry.Id).Order(StringComparer.Ordinal));
        Assert.Equal(summary.Id, all.Single(entry => entry.Id == first.Id).SupersededBy);
        Assert.Equal(summary.Id, all.Single(entry => entry.Id == second.Id).SupersededBy);
        Assert.Equal([first.Id, second.Id], all.Single(entry => entry.Id == summary.Id).SummaryOf);
        Assert.Equal(["kept"], all.Single(entry => entry.Id == first.Id).Tags);
    }

    /// <inheritdoc />
    public void Dispose() => this.database.Dispose();

    private static NewMemoryEntry Summary() => new("summary", "short", ["digest"]);

    private static string[] Ids(IEnumerable<MemoryEntry> entries) => entries.Select(entry => entry.Id).ToArray();

    private static string Messages(Exception error)
    {
        var messages = new List<string>();
        for (var current = error; current is not null; current = current.InnerException)
        {
            messages.Add(current.Message);
        }

        return string.Join(" | ", messages);
    }

    private static string Describe(MemoryEntry entry) => $"{entry.Id} superseded-by={entry.SupersededBy} summary-of={string.Join(',', entry.SummaryOf)}";

    private static async Task<MemoryEntry?> TryAsync(IMemoryStore store, IMemoryScope scope, IReadOnlyList<string> ids)
    {
        try
        {
            return await store.WriteSummaryAsync(scope, Summary(), ids);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private async Task<MemoryEntry[]> WriteAsync(IMemoryScope scope, int count)
    {
        var store = this.database.CreateStore(this.time);
        var written = new MemoryEntry[count];
        for (var i = 0; i < count; i++)
        {
            written[i] = await store.WriteAsync(scope, new NewMemoryEntry("note", $"source {i}", []));
        }

        return written;
    }

    private async Task<string> WriteSupersededAsync(IMemoryScope scope)
    {
        var store = this.database.CreateStore(this.time);
        var entry = await store.WriteAsync(scope, new NewMemoryEntry("note", "already summarized", []));
        await store.WriteSummaryAsync(scope, Summary(), [entry.Id]);
        return entry.Id;
    }

    private async Task AssertUnchangedAsync(IMemoryScope scope, MemoryEntry[] sources)
    {
        var store = this.database.CreateStore(this.time);
        var current = await store.ReadAsync(scope, new MemoryQuery(Limit: 200));
        var all = await store.ReadAsync(scope, Everything);

        Assert.Equal(Ids(sources).Order(StringComparer.Ordinal), current.Select(entry => entry.Id).Order(StringComparer.Ordinal));
        Assert.Equal(sources.Length, all.Count);
        Assert.All(all, entry => Assert.Null(entry.SupersededBy));
    }
}
