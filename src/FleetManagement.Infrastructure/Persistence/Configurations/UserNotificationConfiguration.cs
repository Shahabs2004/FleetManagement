using FleetManagement.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetManagement.Infrastructure.Persistence.Configurations;

/// <summary>نگاشت اعلان‌های داخلی کاربران.</summary>
public sealed class UserNotificationConfiguration : IEntityTypeConfiguration<UserNotification>
{
    /// <summary>تعریف ستون‌ها و شاخص‌های فهرست اعلان.</summary>
    public void Configure(EntityTypeBuilder<UserNotification> builder)
    {
        builder.ToTable("UserNotifications");
        builder.HasKey(notification => notification.Id);
        builder.Property(notification => notification.RecipientUsername).HasMaxLength(64).IsRequired();
        builder.Property(notification => notification.Title).HasMaxLength(200).IsRequired();
        builder.Property(notification => notification.Message).HasMaxLength(1000).IsRequired();
        builder.Property(notification => notification.EventKey).HasMaxLength(80).IsRequired();
        builder.Property(notification => notification.ReferenceType).HasMaxLength(100);
        builder.Property(notification => notification.ReferenceId).HasMaxLength(100);
        builder.Property(notification => notification.CreatedAtUtc).IsRequired();
        builder.HasIndex(notification => new { notification.RecipientUsername, notification.CreatedAtUtc });
        builder.HasIndex(notification => new { notification.RecipientUsername, notification.ReadAtUtc });
    }
}
