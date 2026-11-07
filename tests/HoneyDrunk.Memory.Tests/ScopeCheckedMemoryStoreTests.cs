using HoneyDrunk.Memory.Abstractions;
using Xunit;
using static HoneyDrunk.Memory.Tests.TestKeys;

namespace HoneyDrunk.Memory.Tests;

/// <summary>The default store: it accepts only scopes this runtime issued and enforces write and expiry rules.</summary>
public sealed class ScopeCheckedMemoryStoreTests
{
    private readonly MemoryHarness harness = new();

    /// <summary>A hand-written scope is rejected on every operation, even a writable one.</summary>
    [Fact]
    public async Task Rejects_a_scope_it_did_not_issue()
    {
        var forged = new ForgedScope(Key(Project1));
        var store = this.harness.Store;

        var errors = new[]
        {
            await Assert.ThrowsAsync<ArgumentException>(() => store.WriteAsync(forged, Note("x"))),
            await Assert.ThrowsAsync<ArgumentException>(() => store.GetAsync(forged, "id")),
            await Assert.ThrowsAsync<ArgumentException>(() => store.ReadAsync(forged, new MemoryQuery())),
            await Assert.ThrowsAsync<ArgumentException>(() => store.WriteSummaryAsync(forged, Note("x"), ["id"])),
            await Assert.ThrowsAsync<ArgumentException>(() => store.DeleteAsync(forged, "id")),
            await Assert.ThrowsAsync<ArgumentException>(() => store.DeleteAllAsync(forged)),
        };

        Assert.All(errors, error => Assert.Contains("The scope was not issued by HoneyDrunk.Memory", error.Message, StringComparison.Ordinal));
        Assert.Empty(await this.harness.Provider.ReadAsync(forged, new MemoryQuery()));
    }

    /// <summary>Escalated and operational scopes can read but never write, summarize or delete.</summary>
    [Fact]
    public async Task Rejects_writes_through_escalated_and_operational_scopes()
    {
        var other = Key(Project2);
        var agent = this.harness.Agent(Key(Project1), ReadScope(other));
        var entry = await this.harness.Store.WriteAsync(agent, Note("kept"));
        var escalated = await agent.EscalateAsync([other]);
        var operational = (await this.harness.Resolver.RequestOperationalScopeAsync(
            new MemoryOperationalReadRequest("operator-1", [Key(Project1)], "audit", this.harness.Time.GetUtcNow().AddHours(1), "corr-1"))).Scope!;

        foreach (var scope in new[] { escalated, operational })
        {
            await Assert.ThrowsAsync<MemoryScopeException>(() => this.harness.Store.WriteAsync(scope, Note("no")));
            await Assert.ThrowsAsync<MemoryScopeException>(() => this.harness.Store.WriteSummaryAsync(scope, Note("no"), [entry.Id]));
            await Assert.ThrowsAsync<MemoryScopeException>(() => this.harness.Store.DeleteAsync(scope, entry.Id));
            await Assert.ThrowsAsync<MemoryScopeException>(() => this.harness.Store.DeleteAllAsync(scope));
            Assert.Equal(entry, await this.harness.Store.GetAsync(scope, entry.Id));
        }
    }

    /// <summary>An operational scope is refused once its expiry passes.</summary>
    [Fact]
    public async Task Rejects_an_expired_operational_scope()
    {
        var home = Key(Project1);
        await this.harness.Store.WriteAsync(this.harness.Agent(home), Note("audited"));
        var expiry = this.harness.Time.GetUtcNow().AddMinutes(10);
        var scope = (await this.harness.Resolver.RequestOperationalScopeAsync(new MemoryOperationalReadRequest("operator-1", [home], "audit", expiry, "corr-1"))).Scope!;
        Assert.Single(await this.harness.Store.ReadAsync(scope, new MemoryQuery()));

        this.harness.Time.Advance(TimeSpan.FromMinutes(10));

        await Assert.ThrowsAsync<MemoryScopeException>(() => this.harness.Store.ReadAsync(scope, new MemoryQuery()));
        await Assert.ThrowsAsync<MemoryScopeException>(() => this.harness.Store.GetAsync(scope, "id"));
    }

    /// <summary>
    /// Issued scopes read through to the provider: an agent sees only its own key among keys that
    /// differ only in tenant, project or agent, and a widened scope sees exactly what it was granted.
    /// </summary>
    [Fact]
    public async Task Delegates_reads_through_issued_scopes()
    {
        var home = Key(Project1);
        var others = new[] { Key(Project1, tenant: TenantB), Key(Project2), Key(Project1, "agent-b") };
        var mine = await this.harness.Store.WriteAsync(this.harness.Agent(home), Note("mine"));
        var theirs = new List<MemoryEntry>();
        foreach (var key in others)
        {
            theirs.Add(await this.harness.Store.WriteAsync(this.harness.Agent(key), Note("theirs")));
        }

        var agent = this.harness.Agent(home, ReadScope(others[1]));
        var widened = await agent.EscalateAsync([others[1]]);

        Assert.Equal([mine.Id], (await this.harness.Store.ReadAsync(agent, new MemoryQuery(Limit: 200))).Select(entry => entry.Id));
        foreach (var entry in theirs)
        {
            Assert.Null(await this.harness.Store.GetAsync(agent, entry.Id));
        }

        Assert.Null(await this.harness.Store.GetAsync(agent, "0123456789abcdef0123456789abcdef"));
        Assert.Equal(new[] { mine.Id, theirs[1].Id }.Order(StringComparer.Ordinal), (await this.harness.Store.ReadAsync(widened, new MemoryQuery())).Select(entry => entry.Id).Order(StringComparer.Ordinal));
        Assert.Null(await this.harness.Store.GetAsync(widened, theirs[0].Id));
        Assert.Null(await this.harness.Store.GetAsync(widened, theirs[2].Id));
    }

    private static NewMemoryEntry Note(string content) => new("note", content, []);

    private sealed class ForgedScope(MemoryScopeKey home) : IMemoryScope
    {
        public MemoryScopeKey Home { get; } = home;

        public IReadOnlyList<MemoryScopeKey> Readable => [this.Home];

        public MemoryScopeKind Kind => MemoryScopeKind.Agent;

        public bool CanWrite => true;

        public DateTimeOffset? ExpiresAt => null;

        public Task<IMemoryScope> EscalateAsync(IReadOnlyList<MemoryScopeKey> additionalScopes, CancellationToken cancellationToken = default) =>
            Task.FromResult<IMemoryScope>(this);
    }
}
