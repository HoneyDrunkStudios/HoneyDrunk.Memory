using HoneyDrunk.Memory.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using static HoneyDrunk.Memory.Providers.Contract.TestScopes;

namespace HoneyDrunk.Memory.Providers.Data.Tests;

/// <summary>What the provider sends to the database: always scoped, never more than asked for, never content in an error.</summary>
public sealed class IsolationTests : IDisposable
{
    private static readonly string[] ScopeColumns = ["\"TenantId\"", "\"ProjectId\"", "\"AgentId\""];

    private readonly SqliteDatabase database = new();
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 11, 7, 9, 0, 0, TimeSpan.Zero));

    /// <summary>Every statement that reads, changes or removes rows names the tenant, the project and the agent.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Every_statement_names_tenant_project_and_agent()
    {
        var log = new CommandLog();
        var store = this.database.CreateStore(this.time, log);
        var home = Key();
        var other = Key("agent-b");
        var scope = Writable(home);

        var first = await store.WriteAsync(scope, new NewMemoryEntry("note", "one", ["alice", "project-1"]));
        var second = await store.WriteAsync(scope, new NewMemoryEntry("note", "two", ["alice"]));
        await store.WriteAsync(Writable(other), new NewMemoryEntry("note", "theirs", []));
        await store.GetAsync(ReadOnly(home, other), first.Id);
        await store.ReadAsync(ReadOnly(home, other), new MemoryQuery(Limit: 5, Kind: "note", AllTags: ["alice", "project-1"], WrittenBefore: this.time.GetUtcNow().AddHours(1), IncludeSuperseded: false));
        var summary = await store.WriteSummaryAsync(scope, new NewMemoryEntry("summary", "both", ["digest"]), [first.Id, second.Id]);
        await store.ReadAsync(scope, new MemoryQuery(IncludeSuperseded: true));
        await store.DeleteAsync(scope, summary.Id);
        await store.DeleteAllAsync(scope);

        var statements = log.Commands.Where(text => !text.Contains("INSERT INTO", StringComparison.OrdinalIgnoreCase)).ToArray();
        Assert.Contains(statements, text => text.Contains("SELECT", StringComparison.Ordinal));
        Assert.Contains(statements, text => text.Contains("UPDATE", StringComparison.Ordinal));
        Assert.Contains(statements, text => text.Contains("DELETE", StringComparison.Ordinal));
        Assert.Contains(statements, text => text.Contains("\"EntryTag\"", StringComparison.Ordinal));
        Assert.Contains(statements, text => text.Contains("\"EntrySummarySource\"", StringComparison.Ordinal));

        // A SELECT that loads entries lists these columns anyway, so the proof is the comparison with a parameter.
        Assert.All(statements, text => Assert.All(ScopeColumns, column => Assert.Contains(column + " = @", text, StringComparison.Ordinal)));
    }

    /// <summary>A read across several keys loads content only for the entries it returns.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Read_loads_no_more_than_the_limit()
    {
        var keys = new[] { Key(), Key("agent-b"), Key("agent-c") };
        var writer = this.database.CreateStore(this.time);
        foreach (var key in keys)
        {
            for (var i = 0; i < 10; i++)
            {
                await writer.WriteAsync(Writable(key), new NewMemoryEntry("note", $"{key.AgentId} {i}", []));
                this.time.Advance(TimeSpan.FromSeconds(1));
            }
        }

        var log = new CommandLog();
        var store = this.database.CreateStore(this.time, log);

        var read = await store.ReadAsync(ReadOnly(keys[0], keys[1], keys[2]), new MemoryQuery(Limit: 5));

        Assert.Equal(5, read.Count);
        Assert.All(read, entry => Assert.Equal("agent-c", entry.Scope.AgentId));
        var withContent = log.Commands.Where(text => text.Contains("\"Content\"", StringComparison.Ordinal)).ToArray();
        Assert.NotEmpty(withContent);
        Assert.All(withContent, text => Assert.Contains(" IN (", text, StringComparison.Ordinal));
        Assert.All(log.Commands.Where(text => text.Contains("LIMIT", StringComparison.Ordinal)), text => Assert.DoesNotContain("\"Content\"", text, StringComparison.Ordinal));
    }

    /// <summary>
    /// A write or a summary that the database refuses says so without the content or a tag: not in its
    /// text, and not as the entities Entity Framework attaches to the error.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_failed_write_carries_no_content()
    {
        var scope = Writable(Key());
        var source = await this.database.CreateStore(this.time).WriteAsync(scope, new NewMemoryEntry("note", "source", []));
        var fail = new FailInsertInto("Entry") { Armed = true };
        var store = this.database.CreateStore(this.time, fail);

        var write = await Assert.ThrowsAsync<DbUpdateException>(() => store.WriteAsync(scope, new NewMemoryEntry("note", "SECRET-CONTENT-4471", ["secret-tag-4471"])));
        var summary = await Assert.ThrowsAsync<DbUpdateException>(() => store.WriteSummaryAsync(scope, new NewMemoryEntry("summary", "SECRET-CONTENT-4471", ["secret-tag-4471"]), [source.Id]));

        foreach (var error in new[] { write, summary })
        {
            Assert.Empty(error.Entries);
            Assert.Contains("insert into Entry failed", error.InnerException!.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("SECRET-CONTENT-4471", error.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret-tag-4471", error.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        Assert.Equal([source.Id], (await this.database.CreateStore(this.time).ReadAsync(scope, new MemoryQuery())).Select(entry => entry.Id));
    }

    /// <inheritdoc />
    public void Dispose() => this.database.Dispose();
}
