using FleetManagement.Domain.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// نگاشت جدول رویدادهای ممیزی و شاخص‌های جست‌وجو.
/// </summary>
public sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    /// <summary>
    /// تعریف ستون‌ها و شاخص‌های جدول ممیزی.
    /// </summary>
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("AuditEntries");
        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.ActorUsername).HasMaxLength(64).IsRequired();
        builder.Property(entry => entry.Action).HasMaxLength(80).IsRequired();
        builder.Property(entry => entry.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(entry => entry.EntityId).HasMaxLength(100);
        builder.Property(entry => entry.OccurredAtUtc).IsRequired();
        builder.Property(entry => entry.Outcome).HasMaxLength(40).IsRequired();
        builder.Property(entry => entry.Reason).HasMaxLength(1000);
        builder.Property(entry => entry.BeforeJson).HasMaxLength(12000);
        builder.Property(entry => entry.AfterJson).HasMaxLength(12000);
        builder.Property(entry => entry.ClientIpAddress).HasMaxLength(45);

        builder.HasIndex(entry => entry.OccurredAtUtc);
        builder.HasIndex(entry => new { entry.EntityType, entry.EntityId });
        builder.HasIndex(entry => new { entry.ActorUsername, entry.OccurredAtUtc });
        builder.HasIndex(entry => new { entry.Action, entry.Outcome });
    }
}
