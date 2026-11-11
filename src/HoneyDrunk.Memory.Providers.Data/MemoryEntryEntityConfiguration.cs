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

        // A UTC DateTime compares and orders in SQL on every provider; DateTimeOffset does not on SQLite.
        builder.Property(row => row.WrittenAt)
            .HasConversion(value => value.UtcDateTime, value => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)))
            .IsRequired();
        builder.Property(row => row.SupersededByEntryId).HasMaxLength(MemoryIds.EntryIdLength).IsFixedLength().IsUnicode(false);
        builder.HasIndex(row => new { row.TenantId, row.ProjectId, row.AgentId, row.WrittenAt, row.Id })
            .IsDescending(false, false, false, true, true)
            .HasDatabaseName("IX_Entry_TenantId_ProjectId_AgentId_WrittenAt_Id");
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
