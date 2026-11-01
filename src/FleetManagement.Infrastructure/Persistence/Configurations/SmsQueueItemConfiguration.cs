using FleetManagement.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// نگاشت صف پیامک و شاخص‌های پردازش، گزارش و استعلام تحویل.
/// </summary>
public sealed class SmsQueueItemConfiguration : IEntityTypeConfiguration<SmsQueueItem>
{
    /// <summary>تعریف ستون‌ها و شاخص‌های صف پیامک.</summary>
    public void Configure(EntityTypeBuilder<SmsQueueItem> builder)
    {
        builder.ToTable("SmsQueueItems");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.EventType).IsRequired();
        builder.Property(item => item.RecipientName).HasMaxLength(200).IsRequired();
        builder.Property(item => item.RecipientPhone).HasMaxLength(20).IsRequired();
        builder.Property(item => item.Body).HasMaxLength(2000).IsRequired();
        builder.Property(item => item.MissionId).IsRequired();
        builder.Property(item => item.ScheduledAtUtc).IsRequired();
        builder.Property(item => item.CreatedAtUtc).IsRequired();
        builder.Property(item => item.Status).IsRequired();
        builder.Property(item => item.AttemptCount).IsRequired();
        builder.Property(item => item.MaximumRetryCount).IsRequired();
        builder.Property(item => item.RetryIntervalSeconds).IsRequired();
        builder.Property(item => item.ProviderMessageId).HasMaxLength(100);
        builder.Property(item => item.ProviderDeliveryStatus).HasMaxLength(100);
        builder.Property(item => item.ErrorReason).HasMaxLength(1000);

        builder.HasIndex(item => new { item.Status, item.ScheduledAtUtc });
        builder.HasIndex(item => new { item.Status, item.NextAttemptAtUtc });
        builder.HasIndex(item => new { item.MissionId, item.CreatedAtUtc });
        builder.HasIndex(item => item.ProviderMessageId);
        builder.HasIndex(item => new { item.Status, item.NextStatusPollAtUtc });
    }
}
