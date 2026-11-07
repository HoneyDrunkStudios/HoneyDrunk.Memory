using HoneyDrunk.Kernel.Abstractions.Telemetry;
using HoneyDrunk.Memory.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace HoneyDrunk.Memory;

/// <summary>Registers the HoneyDrunk.Memory runtime.</summary>
public static class MemoryServiceCollectionExtensions
{
    /// <summary>
    /// Adds the memory runtime over a provider. The provider is registered as its concrete type only,
    /// never as <see cref="IMemoryStore"/>: <see cref="IMemoryStore"/> resolves to the scoped
    /// <see cref="ScopeCheckedMemoryStore"/> that wraps it, so no consumer reaches the provider
    /// without the scope check. The host supplies <c>IAuthorizationPolicy</c>, <c>IApprovalGate</c>,
    /// <see cref="ITelemetryActivityFactory"/> and logging; <see cref="TimeProvider.System"/> is added
    /// unless the host registered a clock.
    /// </summary>
    /// <typeparam name="TProvider">The provider type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optionally configures escalation.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddHoneyDrunkMemory<TProvider>(this IServiceCollection services, Action<MemoryAuthorizationOptions>? configure = null)
        where TProvider : class, IMemoryStore
    {
        ArgumentNullException.ThrowIfNull(services);
        var options = services.AddOptions<MemoryAuthorizationOptions>();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<TProvider>();
        services.AddScoped<IMemoryStore>(provider => new ScopeCheckedMemoryStore(
            provider.GetRequiredService<TProvider>(),
            typeof(TProvider).Name,
            provider.GetRequiredService<ITelemetryActivityFactory>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILogger<ScopeCheckedMemoryStore>>()));
        services.AddScoped<IMemoryScopeResolver, MemoryScopeResolver>();
        return services;
    }

    /// <summary>
    /// Adds <see cref="ChatMemorySummarizer"/> as the scoped <see cref="IMemorySummarizer"/>. The host
    /// supplies the <c>IChatClient</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optionally configures the summarizer limits.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddChatMemorySummarizer(this IServiceCollection services, Action<MemorySummarizerOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        var options = services.AddOptions<MemorySummarizerOptions>();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        services.AddScoped<IMemorySummarizer, ChatMemorySummarizer>();
        return services;
    }
}
