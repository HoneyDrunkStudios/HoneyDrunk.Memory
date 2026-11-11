using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HoneyDrunk.Memory.Providers.Data;

/// <summary>Maps <see cref="MemoryEntryEntity"/> to <c>memory.Entry</c>.</summary>
internal sealed class MemoryEntryEntityConfiguration : IEntityTypeConfiguration<MemoryEntryEntity>
{
    /// <summary>The longest entry content, in UTF-16 code units; the contract refuses longer content.</summary>
    internal const int MaxContentLength = 16_000;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MemoryEntryEntity> builder)
    {
        builder.ToTable("Entry");
        builder.HasKey(row => row.Id).HasName("PK_Entry");
        builder.Property(row => row.Id).HasMaxLength(MemoryIds.EntryIdLength).IsFixedLength().IsUnicode(false).IsRequired();
        builder.Property(row => row.TenantId).HasMaxLength(MemoryIds.UlidLength).IsFixedLength().IsUnicode(false).IsRequired();
        builder.Property(row => row.ProjectId).HasMaxLength(MemoryIds.UlidLength).IsFixedLength().IsUnicode(false).IsRequired();
        builder.Property(row => row.AgentId).HasMaxLength(64).IsUnicode(false).IsRequired();
        builder.Property(row => row.Kind).HasMaxLength(32).IsUnicode(false).IsRequired();
        builder.Property(row => row.Content).HasMaxLength(MaxContentLength).IsUnicode().IsRequired();

        builder.Property(row => row.CreatedAt).StoredAsUtc().IsRequired();
        builder.Property(row => row.SupersededByEntryId).HasMaxLength(MemoryIds.EntryIdLength).IsFixedLength().IsUnicode(false);
        builder.HasIndex(row => new { row.TenantId, row.ProjectId, row.AgentId, row.CreatedAt, row.Id })
            .IsDescending(false, false, false, true, true)
            .HasDatabaseName("IX_Entry_TenantId_ProjectId_AgentId_CreatedAt_Id");
        builder.HasMany(row => row.Tags)
            .WithOne()
            .HasForeignKey(tag => tag.EntryId)
            .HasConstraintName("FK_EntryTag_Entry")
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(row => row.SummarySources)
            .WithOne()
            .HasForeignKey(source => source.SummaryEntryId)
            .HasConstraintName("FK_EntrySummarySource_Entry_Summary")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
