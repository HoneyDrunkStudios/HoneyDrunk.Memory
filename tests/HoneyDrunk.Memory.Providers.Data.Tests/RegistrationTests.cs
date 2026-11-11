using HoneyDrunk.Auth.Abstractions;
using HoneyDrunk.Kernel.Abstractions.Telemetry;
using HoneyDrunk.Memory.Abstractions;
using HoneyDrunk.Memory.Canary;
using HoneyDrunk.Memory.Tests;
using HoneyDrunk.Operator.Abstractions;
using HoneyDrunk.Operator.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using static HoneyDrunk.Memory.Providers.Contract.TestScopes;

namespace HoneyDrunk.Memory.Providers.Data.Tests;

/// <summary>How a host composes the provider, and what survives a restart.</summary>
public sealed class RegistrationTests : IDisposable
{
    private static readonly MemoryQuery Everything = new(Limit: 200, IncludeSuperseded: true);

    private readonly SqliteDatabase database = new();
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 11, 7, 9, 0, 0, TimeSpan.Zero));

    /// <summary>The runtime's scope-checked store runs over the Data provider whichever call comes first.</summary>
    /// <param name="providerFirst">Whether the provider is registered before the runtime.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Runtime_resolves_the_data_provider_in_either_order(bool providerFirst)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(this.time);
        services.AddSingleton<IAuthorizationPolicy>(new FakeAuthorizationPolicy());
        services.AddSingleton<IApprovalGate>(new InMemoryApprovalGate(this.time));
        services.AddScoped<ITelemetryActivityFactory>(_ => new NullTelemetryActivityFactory());
        if (providerFirst)
        {
            services.AddMemoryDataProvider(options => this.database.Configure(options));
            services.AddHoneyDrunkMemory<DataMemoryStore>();
        }
        else
        {
            services.AddHoneyDrunkMemory<DataMemoryStore>();
            services.AddMemoryDataProvider(options => this.database.Configure(options));
        }

        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        var store = Assert.IsType<ScopeCheckedMemoryStore>(scope.ServiceProvider.GetRequiredService<IMemoryStore>());
        var agent = scope.ServiceProvider.GetRequiredService<IMemoryScopeResolver>().ResolveAgentScope(Key(), new MemoryCaller("agent:agent-a", [], ["memory-reader"]));

        var written = await store.WriteAsync(agent, new NewMemoryEntry("note", "composed", ["host"]));

        Assert.Equal(written.Id, Assert.Single(await store.ReadAsync(agent, new MemoryQuery())).Id);
        Assert.Equal("composed", (await this.database.CreateStore(this.time).GetAsync(Writable(Key()), written.Id))!.Content);
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(DataMemoryStore));
    }

    /// <summary>A host that turns on sensitive data logging is refused, because EF would write content into logs.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Sensitive_data_logging_is_refused()
    {
        await using var provider = this.Host(options => options.EnableSensitiveDataLogging());

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.GetRequiredService<DataMemoryStore>().WriteAsync(Writable(Key()), new NewMemoryEntry("note", "never logged", [])));

        Assert.Contains("sensitive data logging", refused.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await this.database.CreateStore(this.time).ReadAsync(Writable(Key()), new MemoryQuery()));
    }

    /// <summary>Entries, tags, summaries and the read order are the same from a new host over the same database.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Restart_keeps_entries_tags_summaries_and_order()
    {
        var scope = Writable(Key());
        string[] shownBefore;
        string[] allBefore;
        await using (var before = this.Host())
        {
            var store = before.GetRequiredService<DataMemoryStore>();
            var first = await store.WriteAsync(scope, new NewMemoryEntry("note", "first\r\nline ", ["zeta", "alpha"]));
            var same = await store.WriteAsync(scope, new NewMemoryEntry("note", "same instant", []));
            this.time.Advance(TimeSpan.FromTicks(1_234_567));
            var third = await store.WriteAsync(scope, new NewMemoryEntry("decision", "third", ["alpha"]));
            this.time.Advance(TimeSpan.FromMinutes(1));
            await store.WriteSummaryAsync(scope, new NewMemoryEntry("summary", "short", ["digest"]), [third.Id, first.Id]);
            await store.WriteAsync(Writable(Key("agent-b")), new NewMemoryEntry("note", "theirs", []));
            Assert.NotNull(same);
            shownBefore = (await store.ReadAsync(scope, new MemoryQuery(Limit: 200))).Select(Describe).ToArray();
            allBefore = (await store.ReadAsync(scope, Everything)).Select(Describe).ToArray();
        }

        await using var after = this.Host();
        var restarted = after.GetRequiredService<DataMemoryStore>();

        Assert.Equal(2, shownBefore.Length);
        Assert.Equal(4, allBefore.Length);
        Assert.Equal(shownBefore, (await restarted.ReadAsync(scope, new MemoryQuery(Limit: 200))).Select(Describe));
        Assert.Equal(allBefore, (await restarted.ReadAsync(scope, Everything)).Select(Describe));
    }

    /// <summary>Two hosts over one database read each other's writes at once.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Two_stores_on_one_database_see_each_others_writes()
    {
        var scope = Writable(Key());
        await using var hostA = this.Host();
        await using var hostB = this.Host();
        var a = hostA.GetRequiredService<DataMemoryStore>();
        var b = hostB.GetRequiredService<DataMemoryStore>();

        var fromA = await a.WriteAsync(scope, new NewMemoryEntry("note", "from a", []));
        this.time.Advance(TimeSpan.FromSeconds(1));
        var fromB = await b.WriteAsync(scope, new NewMemoryEntry("note", "from b", []));
        var summary = await a.WriteSummaryAsync(scope, new NewMemoryEntry("summary", "both", []), [fromA.Id, fromB.Id]);

        Assert.Equal([summary.Id], (await b.ReadAsync(scope, new MemoryQuery())).Select(entry => entry.Id));
        Assert.True(await b.DeleteAsync(scope, fromA.Id));
        Assert.Null(await a.GetAsync(scope, fromA.Id));
    }

    /// <inheritdoc />
    public void Dispose() => this.database.Dispose();

    private static string Describe(MemoryEntry entry) =>
        $"{entry.Id}|{entry.Scope}|{entry.Kind}|{entry.Content}|{string.Join(',', entry.Tags)}|{entry.WrittenAt.UtcTicks}|{entry.EmbeddingModelId}|{string.Join(',', entry.SummaryOf)}|{entry.SupersededBy}";

    private ServiceProvider Host(Action<DbContextOptionsBuilder>? extra = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(this.time);
        services.AddMemoryDataProvider(options =>
        {
            this.database.Configure(options);
            extra?.Invoke(options);
        });
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
}
