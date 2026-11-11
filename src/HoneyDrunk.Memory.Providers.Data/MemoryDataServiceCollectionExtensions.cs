using HoneyDrunk.Data.EntityFramework.Registration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HoneyDrunk.Memory.Providers.Data;

/// <summary>Registers the durable memory provider.</summary>
public static class MemoryDataServiceCollectionExtensions
{
    /// <summary>
    /// Registers the memory tables' context through HoneyDrunk.Data and <see cref="DataMemoryStore"/> as
    /// a singleton. Compose it behind the runtime with <c>AddHoneyDrunkMemory&lt;DataMemoryStore&gt;()</c>,
    /// called before or after this method. The host's database project carries the tables; this
    /// package ships their scripts and no migrations. <see cref="TimeProvider.System"/> is added
    /// unless the host registered a clock.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureDbContext">
    /// Configures the database provider, for example <c>UseSqlServer</c>. Sensitive data logging must
    /// stay off: with it on, Entity Framework writes parameter values, which are memory content, into
    /// logs, so the context refuses to start.
    /// </param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddMemoryDataProvider(this IServiceCollection services, Action<DbContextOptionsBuilder> configureDbContext)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureDbContext);
        services.AddHoneyDrunkDataEntityFramework<MemoryDbContext>(options =>
        {
            configureDbContext(options);
            RefuseSensitiveDataLogging(options);
        });
        KeepOptionsConfigurationSingleton(services);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<MemoryEntryDataService>();

        // The runtime registers the provider type with TryAdd and resolves it by that type, so this
        // registration must be the only one whichever method the host calls first.
        services.RemoveAll<DataMemoryStore>();
        services.AddSingleton(provider => new DataMemoryStore(
            provider.GetRequiredService<MemoryEntryDataService>(),
            provider.GetRequiredService<TimeProvider>()));
        return services;
    }

    /// <summary>
    /// The context is registered both as a singleton factory and as a scoped context, which leaves
    /// the singleton options depending on a scoped options configuration. A host that validates
    /// scopes (the default in the Development environment) then cannot create a context from the
    /// factory. The configuration holds only the host's delegate, so it is safe as a singleton.
    /// </summary>
    private static void KeepOptionsConfigurationSingleton(IServiceCollection services)
    {
        for (var i = 0; i < services.Count; i++)
        {
            var descriptor = services[i];
            if (descriptor.ServiceType == typeof(IDbContextOptionsConfiguration<MemoryDbContext>)
                && descriptor.Lifetime != ServiceLifetime.Singleton
                && descriptor.ImplementationFactory is { } factory)
            {
                services[i] = ServiceDescriptor.Singleton(descriptor.ServiceType, factory);
            }
        }
    }

    private static void RefuseSensitiveDataLogging(DbContextOptionsBuilder options)
    {
        if (options.Options.FindExtension<CoreOptionsExtension>()?.IsSensitiveDataLoggingEnabled == true)
        {
            throw new InvalidOperationException(
                "Sensitive data logging is on for the memory provider's context. It would write memory content into logs. Remove EnableSensitiveDataLogging from the options passed to AddMemoryDataProvider.");
        }
    }
}
