using HoneyDrunk.Memory.Abstractions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using static HoneyDrunk.Memory.Providers.Contract.TestScopes;

namespace HoneyDrunk.Memory.Providers.Data.Tests;

/// <summary>
/// A read takes several statements. Whatever another host commits between them, an entry comes back
/// whole or not at all: never without its tags or its list of sources. The race is real on SQL
/// Server, where each statement sees the latest committed rows; SQLite reads one snapshot.
/// </summary>
public abstract class ReadConsistencyContract
{
    private static readonly string[] Tags = ["alice", "project-1"];

    /// <summary>Gets the test clock.</summary>
    protected FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 11, 7, 9, 0, 0, TimeSpan.Zero));

    /// <summary>A read returns each entry whole or not at all when another host changes it between two statements.</summary>
    /// <param name="marker">Text of the read's statement that the other host's change lands just before.</param>
    /// <param name="change">What the other host does to the entry.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("EntryTag", "supersede")]
    [InlineData("EntryTag", "delete")]
    [InlineData("Content", "supersede")]
    [InlineData("Content", "delete")]
    public async Task Read_returns_an_entry_whole_or_not_at_all(string marker, string change)
    {
        var scope = Writable(Key());
        var (target, summary) = await this.SeedAsync(scope);
        var race = new RunBeforeFirst(marker, () => this.ChangeAsync(scope, target, change)) { Armed = true };
        var store = this.CreateStore(race);

        var read = await store.ReadAsync(scope, new MemoryQuery(Limit: 50));

        Assert.True(race.Ran);
        Assert.NotEmpty(read);
        foreach (var entry in read)
        {
            Assert.Equal(entry.Id == summary.Id ? summary.Tags : Tags, entry.Tags);
            Assert.Equal(entry.Id == summary.Id ? summary.SummaryOf : [], entry.SummaryOf);
            Assert.Null(entry.SupersededBy);
        }
    }

    /// <summary>A get returns the entry whole or not at all when another host deletes it between two statements.</summary>
    /// <param name="marker">Text of the get's statement that the delete lands just before.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("EntryTag")]
    [InlineData("EntrySummarySource")]
    [InlineData("Content")]
    public async Task Get_returns_an_entry_whole_or_not_at_all(string marker)
    {
        var scope = Writable(Key());
        var (_, summary) = await this.SeedAsync(scope);
        var race = new RunBeforeFirst(marker, () => this.ChangeAsync(scope, summary, "delete")) { Armed = true };
        var store = this.CreateStore(race);

        var found = await store.GetAsync(scope, summary.Id);

        Assert.True(race.Ran);
        if (found is not null)
        {
            Assert.Equal(summary.SummaryOf, found.SummaryOf);
            Assert.Equal(summary.Tags, found.Tags);
        }
    }

    /// <summary>Creates a store over this test's database.</summary>
    /// <param name="interceptors">Interceptors to attach to every context the store opens.</param>
    /// <returns>The store.</returns>
    protected abstract IMemoryStore CreateStore(params IInterceptor[] interceptors);

    private async Task<(MemoryEntry target, MemoryEntry summary)> SeedAsync(IMemoryScope scope)
    {
        var store = this.CreateStore();
        var old = await store.WriteAsync(scope, new NewMemoryEntry("note", "old", Tags));
        var summary = await store.WriteSummaryAsync(scope, new NewMemoryEntry("summary", "short", ["digest"]), [old.Id]);
        this.Time.Advance(TimeSpan.FromSeconds(1));
        var target = await store.WriteAsync(scope, new NewMemoryEntry("note", "target", Tags));
        this.Time.Advance(TimeSpan.FromSeconds(1));
        await store.WriteAsync(scope, new NewMemoryEntry("note", "bystander", Tags));
        return (target, summary);
    }

    private async Task ChangeAsync(IMemoryScope scope, MemoryEntry entry, string change)
    {
        var other = this.CreateStore();
        if (change == "delete")
        {
            Assert.True(await other.DeleteAsync(scope, entry.Id));
        }
        else
        {
            await other.WriteSummaryAsync(scope, new NewMemoryEntry("summary", "raced", ["digest"]), [entry.Id]);
        }
    }
}
