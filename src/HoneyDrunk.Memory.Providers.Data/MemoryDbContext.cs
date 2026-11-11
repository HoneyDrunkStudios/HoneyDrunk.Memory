using HoneyDrunk.Data.EntityFramework.Context;
using Microsoft.EntityFrameworkCore;

namespace HoneyDrunk.Memory.Providers.Data;

/// <summary>
/// The memory tables in the <c>memory</c> schema. The context is internal: memory content leaves
/// this package only through <c>IMemoryStore</c>.
/// </summary>
/// <param name="options">The context options.</param>
internal sealed class MemoryDbContext(DbContextOptions<MemoryDbContext> options) : HoneyDrunkDbContext(options)
{
    /// <summary>Gets the entries.</summary>
    public DbSet<MemoryEntryEntity> Entries => this.Set<MemoryEntryEntity>();

    /// <inheritdoc />
    protected override string? Schema => "memory";

    /// <inheritdoc />
    protected override void ApplyConfigurations(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfiguration(new MemoryEntryEntityConfiguration());
        modelBuilder.ApplyConfiguration(new MemoryEntryTagEntityConfiguration());
        modelBuilder.ApplyConfiguration(new MemoryEntrySummarySourceEntityConfiguration());
    }
}
