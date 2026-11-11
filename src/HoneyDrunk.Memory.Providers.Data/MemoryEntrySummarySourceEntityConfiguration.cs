using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoneyDrunk.Memory.Providers.Data;

/// <summary>Maps <see cref="MemoryEntrySummarySourceEntity"/> to <c>memory.EntrySummarySource</c>.</summary>
internal sealed class MemoryEntrySummarySourceEntityConfiguration : IEntityTypeConfiguration<MemoryEntrySummarySourceEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MemoryEntrySummarySourceEntity> builder)
    {
        builder.ToTable("EntrySummarySource");
        builder.HasKey(row => new { row.SummaryEntryId, row.Ordinal }).HasName("PK_EntrySummarySource");
        builder.Property(row => row.SummaryEntryId).HasMaxLength(MemoryIds.EntryIdLength).IsFixedLength().IsUnicode(false).IsRequired();
        builder.Property(row => row.Ordinal).IsRequired();
        builder.Property(row => row.SourceEntryId).HasMaxLength(MemoryIds.EntryIdLength).IsFixedLength().IsUnicode(false).IsRequired();
        builder.Property(row => row.CreatedAt).StoredAsUtc().IsRequired();
        builder.HasIndex(row => new { row.SummaryEntryId, row.SourceEntryId }).IsUnique().HasDatabaseName("UQ_EntrySummarySource_SummaryEntryId_SourceEntryId");
    }
}
