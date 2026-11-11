using HoneyDrunk.Memory.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using static HoneyDrunk.Memory.Providers.Contract.TestScopes;

namespace HoneyDrunk.Memory.Providers.Data.Tests;

/// <summary>The shape of the three tables, read from the model.</summary>
public sealed class MappingTests : IDisposable
{
    private readonly SqliteDatabase database = new();

    /// <summary>There are exactly three tables, singular, in the memory schema.</summary>
    [Fact]
    public void Model_maps_three_singular_tables_in_the_memory_schema()
    {
        using var context = this.database.CreateContext();

        var tables = Design(context).GetEntityTypes().Select(type => $"{type.GetSchema()}.{type.GetTableName()}").Order(StringComparer.Ordinal);

        Assert.Equal(["memory.Entry", "memory.EntrySummarySource", "memory.EntryTag"], tables);
    }

    /// <summary>Every column has the pinned length, width, character set and nullability.</summary>
    [Fact]
    public void Columns_have_the_pinned_lengths()
    {
        using var context = this.database.CreateContext();

        var columns = Design(context).GetEntityTypes()
            .SelectMany(type => type.GetProperties().Select(property => Describe(type, property)))
            .Order(StringComparer.Ordinal);

        Assert.Equal(
            [
                "Entry.AgentId length=64 fixed=False unicode=False null=False",
                "Entry.Content length=16000 fixed=False unicode=True null=False",
                "Entry.CreatedAt length= fixed=False unicode= null=False",
                "Entry.Id length=32 fixed=True unicode=False null=False",
                "Entry.Kind length=32 fixed=False unicode=False null=False",
                "Entry.ProjectId length=26 fixed=True unicode=False null=False",
                "Entry.SupersededByEntryId length=32 fixed=True unicode=False null=True",
                "Entry.TenantId length=26 fixed=True unicode=False null=False",
                "EntrySummarySource.CreatedAt length= fixed=False unicode= null=False",
                "EntrySummarySource.Ordinal length= fixed=False unicode= null=False",
                "EntrySummarySource.SourceEntryId length=32 fixed=True unicode=False null=False",
                "EntrySummarySource.SummaryEntryId length=32 fixed=True unicode=False null=False",
                "EntryTag.CreatedAt length= fixed=False unicode= null=False",
                "EntryTag.EntryId length=32 fixed=True unicode=False null=False",
                "EntryTag.Ordinal length= fixed=False unicode= null=False",
                "EntryTag.Tag length=64 fixed=False unicode=True null=False",
            ],
            columns);
    }

    /// <summary>The one index on entries serves reads by key, newest first.</summary>
    [Fact]
    public void Scope_index_orders_newest_first()
    {
        using var context = this.database.CreateContext();

        var index = Assert.Single(Entity(context, "Entry").GetIndexes());

        Assert.Equal(["TenantId", "ProjectId", "AgentId", "CreatedAt", "Id"], index.Properties.Select(property => property.Name));
        Assert.Equal([false, false, false, true, true], index.IsDescending);
        Assert.False(index.IsUnique);
    }

    /// <summary>Tags and summary sources are deleted with the entry that owns them.</summary>
    [Fact]
    public void Children_are_deleted_with_their_entry()
    {
        using var context = this.database.CreateContext();

        var tag = Assert.Single(Entity(context, "EntryTag").GetForeignKeys());
        var source = Assert.Single(Entity(context, "EntrySummarySource").GetForeignKeys());

        Assert.Equal(["EntryId"], tag.Properties.Select(property => property.Name));
        Assert.Equal(DeleteBehavior.Cascade, tag.DeleteBehavior);
        Assert.Equal(["SummaryEntryId"], source.Properties.Select(property => property.Name));
        Assert.Equal(DeleteBehavior.Cascade, source.DeleteBehavior);
    }

    /// <summary>The ids that record lineage are plain values, so a delete never breaks or erases them.</summary>
    [Fact]
    public void Lineage_ids_have_no_foreign_key()
    {
        using var context = this.database.CreateContext();

        Assert.Empty(Entity(context, "Entry").GetForeignKeys());
        Assert.DoesNotContain(Entity(context, "EntrySummarySource").GetForeignKeys(), key => key.Properties.Any(property => property.Name == "SourceEntryId"));
    }

    /// <summary>A tag appears once on an entry, and an entry once in a summary's list.</summary>
    [Fact]
    public void Child_rows_are_unique_per_entry()
    {
        using var context = this.database.CreateContext();

        var tag = Assert.Single(Entity(context, "EntryTag").GetIndexes(), index => index.IsUnique);
        var source = Assert.Single(Entity(context, "EntrySummarySource").GetIndexes(), index => index.IsUnique);

        Assert.Equal(["EntryId", "Tag"], tag.Properties.Select(property => property.Name));
        Assert.Equal(["SummaryEntryId", "SourceEntryId"], source.Properties.Select(property => property.Name));
        Assert.Equal(["EntryId", "Ordinal"], Entity(context, "EntryTag").FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Equal(["SummaryEntryId", "Ordinal"], Entity(context, "EntrySummarySource").FindPrimaryKey()!.Properties.Select(property => property.Name));
    }

    /// <summary>A write time is stored and read back with all seven fractional digits, in UTC.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Written_at_keeps_full_precision()
    {
        var writtenAt = new DateTimeOffset(2026, 11, 7, 9, 0, 0, TimeSpan.Zero).AddTicks(1_234_567);
        await using (var write = this.database.CreateContext())
        {
            write.Entries.Add(new MemoryEntryEntity
            {
                Id = "0123456789abcdef0123456789abcdef",
                TenantId = "01JC0000000000000000000001",
                ProjectId = "01JC0000000000000000000002",
                AgentId = "agent-a",
                Kind = "note",
                Content = "precise",
                CreatedAt = writtenAt.ToOffset(TimeSpan.FromHours(-5)),
            });
            await write.SaveChangesAsync();
        }

        await using var read = this.database.CreateContext();
        var stored = await read.Entries.AsNoTracking().SingleAsync();

        Assert.Equal(writtenAt, stored.CreatedAt);
        Assert.Equal(TimeSpan.Zero, stored.CreatedAt.Offset);
        Assert.Equal(writtenAt.UtcTicks, stored.CreatedAt.UtcTicks);
    }

    /// <summary>Every table carries a creation stamp: an entry's write time, repeated on the rows written with it.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Child_rows_carry_their_entry_creation_time()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 7, 9, 0, 0, TimeSpan.Zero).AddTicks(1_234_567));
        var store = this.database.CreateStore(time);
        var scope = Writable(Key());
        var source = await store.WriteAsync(scope, new NewMemoryEntry("note", "source", ["alice", "project-1"]));
        time.Advance(TimeSpan.FromMinutes(1));
        var summary = await store.WriteSummaryAsync(scope, new NewMemoryEntry("summary", "short", ["digest"]), [source.Id]);

        await using var context = this.database.CreateContext();
        var entries = await context.Entries.AsNoTracking().ToDictionaryAsync(row => row.Id, row => row.CreatedAt);
        var tags = await context.Set<MemoryEntryTagEntity>().AsNoTracking().ToListAsync();
        var sources = await context.Set<MemoryEntrySummarySourceEntity>().AsNoTracking().ToListAsync();

        Assert.Equal(source.WrittenAt, entries[source.Id]);
        Assert.Equal(summary.WrittenAt, entries[summary.Id]);
        Assert.Equal(3, tags.Count);
        Assert.All(tags, tag => Assert.Equal(entries[tag.EntryId], tag.CreatedAt));
        Assert.Equal(summary.WrittenAt, Assert.Single(sources).CreatedAt);
    }

    /// <inheritdoc />
    public void Dispose() => this.database.Dispose();

    private static IModel Design(MemoryDbContext context) => context.GetService<IDesignTimeModel>().Model;

    private static IEntityType Entity(MemoryDbContext context, string table) =>
        Design(context).GetEntityTypes().Single(type => type.GetTableName() == table);

    private static string Describe(IEntityType type, IProperty property) =>
        $"{type.GetTableName()}.{property.GetColumnName()} length={property.GetMaxLength()} fixed={property.IsFixedLength() == true} unicode={property.IsUnicode()} null={property.IsNullable}";
}
