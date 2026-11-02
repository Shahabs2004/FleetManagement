using FleetManagement.Domain.Locations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// نگاشت مکان‌های مشترک و وضعیت تأیید آن‌ها.
/// </summary>
public sealed class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    /// <summary>تعریف جدول و شاخص جست‌وجوی نام مکان.</summary>
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.ToTable("Locations");
        builder.HasKey(location => location.Id);
        builder.Property(location => location.Name).HasMaxLength(200).IsRequired();
        builder.Property(location => location.Status).HasConversion<int>().IsRequired();
        builder.Property(location => location.ProposedBy).HasMaxLength(64).IsRequired();
        builder.Property(location => location.ReviewedBy).HasMaxLength(64);
        builder.Property(location => location.ReviewReason).HasMaxLength(500);
        builder.Property(location => location.CreatedAtUtc).IsRequired();
        builder.HasIndex(location => location.Name);
        builder.HasIndex(location => new { location.Status, location.CreatedAtUtc });
    }
}
