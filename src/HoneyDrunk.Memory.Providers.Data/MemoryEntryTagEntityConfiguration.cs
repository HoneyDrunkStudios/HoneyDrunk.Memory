using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoneyDrunk.Memory.Providers.Data;

/// <summary>Maps <see cref="MemoryEntryTagEntity"/> to <c>memory.EntryTag</c>.</summary>
internal sealed class MemoryEntryTagEntityConfiguration : IEntityTypeConfiguration<MemoryEntryTagEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MemoryEntryTagEntity> builder)
    {
        builder.ToTable("EntryTag");
        builder.HasKey(row => new { row.EntryId, row.Ordinal }).HasName("PK_EntryTag");
        builder.Property(row => row.EntryId).HasMaxLength(MemoryIds.EntryIdLength).IsFixedLength().IsUnicode(false).IsRequired();
        builder.Property(row => row.Ordinal).IsRequired();
        builder.Property(row => row.Tag).HasMaxLength(64).IsUnicode().IsRequired();
        builder.HasIndex(row => new { row.EntryId, row.Tag }).IsUnique().HasDatabaseName("UQ_EntryTag_EntryId_Tag");
    }
}
