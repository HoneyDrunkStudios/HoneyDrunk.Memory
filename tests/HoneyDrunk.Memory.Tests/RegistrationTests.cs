using HoneyDrunk.AI.Abstractions.Chat;
using HoneyDrunk.Auth.Abstractions;
using HoneyDrunk.Kernel.Abstractions.Telemetry;
using HoneyDrunk.Memory.Abstractions;
using HoneyDrunk.Memory.Providers.InMemory;
using HoneyDrunk.Operator.Abstractions;
using HoneyDrunk.Operator.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using static HoneyDrunk.Memory.Tests.TestKeys;

namespace HoneyDrunk.Memory.Tests;

/// <summary>Service registration.</summary>
public sealed class RegistrationTests
{
    /// <summary>The store resolves as the scope-checked wrapper, and the composition validates.</summary>
    [Fact]
    public async Task Store_resolves_as_the_scope_checked_wrapper()
    {
        using var services = Compose().BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = services.CreateScope();

        var store = Assert.IsType<ScopeCheckedMemoryStore>(scope.ServiceProvider.GetRequiredService<IMemoryStore>());
        var resolver = Assert.IsType<MemoryScopeResolver>(scope.ServiceProvider.GetRequiredService<IMemoryScopeResolver>());
        Assert.IsType<ChatMemorySummarizer>(scope.ServiceProvider.GetRequiredService<IMemorySummarizer>());
        Assert.Equal("memory.read:{tenant}:{project}:{agent}", services.GetRequiredService<IOptions<MemoryAuthorizationOptions>>().Value.EscalationScopeTemplate);
        Assert.Equal(7, services.GetRequiredService<IOptions<MemorySummarizerOptions>>().Value.MaxEntriesPerSummary);

        var agent = resolver.ResolveAgentScope(Key(Project1), Caller());
        var written = await store.WriteAsync(agent, new NewMemoryEntry("note", "registered", []));
        Assert.Equal(services.GetRequiredService<TimeProvider>().GetUtcNow(), written.WrittenAt);
        Assert.Equal(written, Assert.Single(await store.ReadAsync(agent, new MemoryQuery())));
    }

    /// <summary>The provider is a concrete singleton only, so nothing reaches it without the scope check.</summary>
    [Fact]
    public void Provider_is_not_registered_as_IMemoryStore()
    {
        var collection = Compose();
        using var services = collection.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        var descriptor = Assert.Single(collection, item => item.ServiceType == typeof(IMemoryStore));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.Contains(collection, item => item.ServiceType == typeof(InMemoryMemoryStore) && item.Lifetime == ServiceLifetime.Singleton);

        using var first = services.CreateScope();
        using var second = services.CreateScope();
        Assert.IsType<ScopeCheckedMemoryStore>(Assert.Single(first.ServiceProvider.GetServices<IMemoryStore>()));
        Assert.Same(first.ServiceProvider.GetRequiredService<InMemoryMemoryStore>(), second.ServiceProvider.GetRequiredService<InMemoryMemoryStore>());
    }

    private static ServiceCollection Compose()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 7, 9, 0, 0, TimeSpan.Zero));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(time);
        services.AddSingleton<IAuthorizationPolicy, FakeAuthorizationPolicy>();
        services.AddSingleton<IApprovalGate>(new InMemoryApprovalGate(time));
        services.AddScoped<ITelemetryActivityFactory, RecordingTelemetryActivityFactory>();
        services.AddSingleton<IChatClient>(new RecordingChatClient("summary"));
        services.AddHoneyDrunkMemory<InMemoryMemoryStore>(options => options.IdentityScheme = "test-scheme");
        services.AddChatMemorySummarizer(options => options.MaxEntriesPerSummary = 7);
        return services;
    }
}
