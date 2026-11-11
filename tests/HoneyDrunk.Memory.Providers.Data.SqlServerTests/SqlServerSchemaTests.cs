using HoneyDrunk.Memory.Abstractions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using static HoneyDrunk.Memory.Providers.Contract.TestScopes;

namespace HoneyDrunk.Memory.Providers.Data.SqlServerTests;

/// <summary>The shipped table scripts, deployed to SQL Server, against the mapping and the table rules.</summary>
public sealed class SqlServerSchemaTests : IDisposable
{
    private const string EntryId = "0123456789abcdef0123456789abcdef";
    private const string OtherId = "fedcba9876543210fedcba9876543210";
    private const string InsertEntry = "INSERT INTO memory.Entry (Id, TenantId, ProjectId, AgentId, Kind, Content, WrittenAt, SupersededByEntryId) VALUES ";
    private const string Coordinates = "'01JC0000000000000000000001', '01JC0000000000000000000002', 'agent-a', 'note'";

    private readonly SqlServerDatabase database = new();
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 11, 7, 9, 0, 0, TimeSpan.Zero));

    /// <summary>Every mapped column is deployed with the type, length and nullability the mapping expects, and no other column exists.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Deployed_columns_match_the_mapping()
    {
        await using var context = this.database.CreateContext();
        var mapped = context.GetService<IDesignTimeModel>().Model.GetEntityTypes()
            .SelectMany(type => type.GetProperties().Select(property =>
                $"{type.GetSchema()}.{type.GetTableName()}.{property.GetColumnName()} {property.GetColumnType()} null={property.IsNullable}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        var deployed = await this.QueryAsync(
            """
            SELECT TABLE_SCHEMA + '.' + TABLE_NAME + '.' + COLUMN_NAME + ' ' + DATA_TYPE
                + CASE WHEN CHARACTER_MAXIMUM_LENGTH = -1 THEN '(max)' WHEN CHARACTER_MAXIMUM_LENGTH IS NOT NULL THEN '(' + CAST(CHARACTER_MAXIMUM_LENGTH AS varchar(10)) + ')' ELSE '' END
                + ' null=' + CASE IS_NULLABLE WHEN 'YES' THEN 'True' ELSE 'False' END
            FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'memory'
            """);

        Assert.Equal(14, mapped.Length);
        Assert.Equal(mapped, deployed.Order(StringComparer.Ordinal));
        Assert.Equal(["7"], await this.QueryAsync("SELECT CAST(DATETIME_PRECISION AS varchar(3)) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'memory' AND COLUMN_NAME = 'WrittenAt'"));
    }

    /// <summary>All three tables and all fourteen columns carry a description that says more than the name.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Every_table_and_column_has_a_description()
    {
        var tables = await this.QueryAsync(
            """
            SELECT t.name + '=' + ISNULL(CAST(p.value AS nvarchar(max)), '')
            FROM sys.tables t
            LEFT JOIN sys.extended_properties p ON p.class = 1 AND p.major_id = t.object_id AND p.minor_id = 0 AND p.name = 'MS_Description'
            WHERE SCHEMA_NAME(t.schema_id) = 'memory'
            """);
        var columns = await this.QueryAsync(
            """
            SELECT c.name + '=' + ISNULL(CAST(p.value AS nvarchar(max)), '')
            FROM sys.tables t
            JOIN sys.columns c ON c.object_id = t.object_id
            LEFT JOIN sys.extended_properties p ON p.class = 1 AND p.major_id = t.object_id AND p.minor_id = c.column_id AND p.name = 'MS_Description'
            WHERE SCHEMA_NAME(t.schema_id) = 'memory'
            """);

        Assert.Equal(3, tables.Count);
        Assert.Equal(14, columns.Count);
        Assert.All(tables, described => Assert.Contains("Classification: Confidential.", described, StringComparison.Ordinal));
        Assert.All(tables.Concat(columns), described =>
        {
            var parts = described.Split('=', 2);
            Assert.True(parts[1].Length >= 20, $"'{parts[0]}' has no real description.");
            Assert.NotEqual(parts[0], parts[1]);
        });
    }

    /// <summary>Every text column that is compared holds a binary collation, so text matches by exact value.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Text_key_columns_use_a_binary_collation()
    {
        var collations = await this.QueryAsync(
            "SELECT TABLE_NAME + '.' + COLUMN_NAME + '=' + COLLATION_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'memory' AND COLLATION_NAME IS NOT NULL AND COLUMN_NAME <> 'Content'");

        Assert.Equal(10, collations.Count);
        Assert.All(collations, column => Assert.EndsWith("=Latin1_General_100_BIN2", column, StringComparison.Ordinal));
    }

    /// <summary>The database itself refuses rows the contract forbids, each by a named constraint.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Database_refuses_rows_the_contract_forbids()
    {
        await this.ExecuteAsync($"{InsertEntry}('{EntryId}', {Coordinates}, N'kept', SYSUTCDATETIME(), NULL)");
        await this.ExecuteAsync($"INSERT INTO memory.EntryTag (EntryId, Ordinal, Tag) VALUES ('{EntryId}', 0, N'alice')");

        await this.AssertRefusedAsync("CK_Entry_Content_Length", $"{InsertEntry}('{OtherId}', {Coordinates}, REPLICATE(CAST(N'x' AS nvarchar(max)), 16001), SYSUTCDATETIME(), NULL)");
        await this.AssertRefusedAsync("CK_Entry_Content_Length", $"{InsertEntry}('{OtherId}', {Coordinates}, N'', SYSUTCDATETIME(), NULL)");
        await this.AssertRefusedAsync("CK_Entry_Id_Format", $"{InsertEntry}('{OtherId.ToUpperInvariant()}', {Coordinates}, N'x', SYSUTCDATETIME(), NULL)");
        await this.AssertRefusedAsync("CK_Entry_Id_Format", $"{InsertEntry}('{OtherId[..31]}', {Coordinates}, N'x', SYSUTCDATETIME(), NULL)");
        await this.AssertRefusedAsync("CK_Entry_SupersededByEntryId_NotSelf", $"{InsertEntry}('{OtherId}', {Coordinates}, N'x', SYSUTCDATETIME(), '{OtherId}')");
        await this.AssertRefusedAsync("PK_Entry", $"{InsertEntry}('{EntryId}', {Coordinates}, N'x', SYSUTCDATETIME(), NULL)");
        await this.AssertRefusedAsync("CK_EntryTag_Ordinal", $"INSERT INTO memory.EntryTag (EntryId, Ordinal, Tag) VALUES ('{EntryId}', 16, N'late')");
        await this.AssertRefusedAsync("UQ_EntryTag_EntryId_Tag", $"INSERT INTO memory.EntryTag (EntryId, Ordinal, Tag) VALUES ('{EntryId}', 1, N'alice')");
        await this.AssertRefusedAsync("CK_EntryTag_Tag_Length", $"INSERT INTO memory.EntryTag (EntryId, Ordinal, Tag) VALUES ('{EntryId}', 1, N'')");
        await this.AssertRefusedAsync("FK_EntryTag_Entry", $"INSERT INTO memory.EntryTag (EntryId, Ordinal, Tag) VALUES ('{OtherId}', 0, N'orphan')");
        await this.AssertRefusedAsync("FK_EntrySummarySource_Entry_Summary", $"INSERT INTO memory.EntrySummarySource (SummaryEntryId, Ordinal, SourceEntryId) VALUES ('{OtherId}', 0, '{EntryId}')");
        await this.AssertRefusedAsync("CK_EntrySummarySource_Ordinal", $"INSERT INTO memory.EntrySummarySource (SummaryEntryId, Ordinal, SourceEntryId) VALUES ('{EntryId}', -1, '{OtherId}')");

        Assert.Equal(["1"], await this.QueryAsync("SELECT CAST(COUNT(*) AS varchar(10)) FROM memory.Entry"));
    }

    /// <summary>Deleting an entry removes its tags and its list of sources with it.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Deleting_an_entry_removes_its_tags_and_sources()
    {
        var store = this.database.CreateStore(this.time);
        var scope = Writable(Key());
        var source = await store.WriteAsync(scope, new NewMemoryEntry("note", "source", ["alice", "project-1"]));
        var summary = await store.WriteSummaryAsync(scope, new NewMemoryEntry("summary", "short", ["digest"]), [source.Id]);

        Assert.True(await store.DeleteAsync(scope, summary.Id));
        Assert.Equal(1, await store.DeleteAllAsync(scope));

        Assert.Equal(["0"], await this.QueryAsync("SELECT CAST((SELECT COUNT(*) FROM memory.Entry) + (SELECT COUNT(*) FROM memory.EntryTag) + (SELECT COUNT(*) FROM memory.EntrySummarySource) AS varchar(10))"));
    }

    /// <inheritdoc />
    public void Dispose() => this.database.Dispose();

    private async Task AssertRefusedAsync(string constraint, string sql)
    {
        var refused = await Assert.ThrowsAsync<SqlException>(() => this.ExecuteAsync(sql));
        Assert.Contains(constraint, refused.Message, StringComparison.Ordinal);
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var context = this.database.CreateContext();
        await context.Database.ExecuteSqlRawAsync(sql);
    }

    private async Task<IReadOnlyList<string>> QueryAsync(string sql)
    {
        await using var context = this.database.CreateContext();
        return await context.Database.SqlQueryRaw<string>(sql).ToListAsync();
    }
}
