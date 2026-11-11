using HoneyDrunk.Memory.Abstractions;
using HoneyDrunk.Memory.Providers.Contract;
using Xunit;
using static HoneyDrunk.Memory.Providers.Contract.TestScopes;

namespace HoneyDrunk.Memory.Providers.Data.SqlServerTests;

/// <summary>The store contract against the Data provider on SQL Server, over the shipped table scripts.</summary>
public sealed class SqlServerDataMemoryStoreTests : MemoryStoreContract, IDisposable
{
    private static readonly MemoryQuery Everything = new(Limit: 200, IncludeSuperseded: true);

    private readonly SqlServerDatabase database = new();

    /// <summary>Entries, tags, summaries and the read order are the same from a new store over the same database.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Restart_keeps_entries_tags_summaries_and_order()
    {
        var scope = Writable(Key());
        var store = this.CreateStore(this.Time);
        var first = await store.WriteAsync(scope, new NewMemoryEntry("note", "first\r\nline ", ["zeta", "alpha"]));
        await store.WriteAsync(scope, new NewMemoryEntry("note", "same instant", []));
        this.Time.Advance(TimeSpan.FromTicks(1_234_567));
        var third = await store.WriteAsync(scope, new NewMemoryEntry("decision", "third", ["alpha"]));
        this.Time.Advance(TimeSpan.FromMinutes(1));
        await store.WriteSummaryAsync(scope, new NewMemoryEntry("summary", "short", ["digest"]), [third.Id, first.Id]);
        var shown = await store.ReadAsync(scope, new MemoryQuery(Limit: 200));
        var all = await store.ReadAsync(scope, Everything);

        var restarted = this.CreateStore(this.Time);

        Assert.Equal(2, shown.Count);
        Assert.Equal(4, all.Count);
        AssertSameEntries(shown, await restarted.ReadAsync(scope, new MemoryQuery(Limit: 200)));
        AssertSameEntries(all, await restarted.ReadAsync(scope, Everything));
    }

    /// <summary>Two stores that summarize overlapping sources at once store one summary; the other is refused whole.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Concurrent_summaries_over_the_same_sources_store_one()
    {
        var scope = Writable(Key());
        var writer = this.CreateStore(this.Time);
        for (var round = 0; round < 10; round++)
        {
            var a = await writer.WriteAsync(scope, new NewMemoryEntry("note", "a", []));
            var b = await writer.WriteAsync(scope, new NewMemoryEntry("note", "b", []));
            var c = await writer.WriteAsync(scope, new NewMemoryEntry("note", "c", []));
            var first = this.CreateStore(this.Time);
            var second = this.CreateStore(this.Time);

            var outcomes = await Task.WhenAll(
                Task.Run(() => TryAsync(first, scope, [a.Id, b.Id])),
                Task.Run(() => TryAsync(second, scope, [b.Id, c.Id])));

            var winner = Assert.Single(outcomes, outcome => outcome is not null)!;
            var loserOnly = winner.SummaryOf.Contains(a.Id) ? c : a;
            foreach (var id in winner.SummaryOf)
            {
                Assert.Equal(winner.Id, (await writer.GetAsync(scope, id))!.SupersededBy);
            }

            Assert.Null((await writer.GetAsync(scope, loserOnly.Id))!.SupersededBy);
        }
    }

    /// <inheritdoc />
    public void Dispose() => this.database.Dispose();

    /// <inheritdoc />
    protected override IMemoryStore CreateStore(TimeProvider timeProvider) => this.database.CreateStore(timeProvider);

    private static async Task<MemoryEntry?> TryAsync(IMemoryStore store, IMemoryScope scope, IReadOnlyList<string> ids)
    {
        try
        {
            return await store.WriteSummaryAsync(scope, new NewMemoryEntry("summary", "short", []), ids);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
