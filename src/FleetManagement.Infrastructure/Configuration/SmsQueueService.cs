using FleetManagement.Domain.Notifications;
using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// مدیریت صف پایدار پیامک، گزارش وضعیت و تلاش مجدد دستی.
/// </summary>
public sealed class SmsQueueService(
    IDbContextFactory<FleetDbContext> dbContextFactory,
    AuditLogService auditLog)
{
    /// <summary>ثبت پیام آماده ارسال برای یک گیرنده.</summary>
    public async Task<Guid> EnqueueAsync(
        SmsEventType eventType,
        string recipientName,
        string recipientPhone,
        string body,
        Guid missionId,
        DateTimeOffset scheduledAtUtc,
        int maximumRetryCount,
        int retryIntervalSeconds,
        int maximumStatusPollingAttempts,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var missionExists = await db.Missions.AnyAsync(
            mission => mission.Id == missionId,
            cancellationToken);
        if (!missionExists)
        {
            throw new KeyNotFoundException("مأموریت مرتبط با پیامک پیدا نشد.");
        }

        var item = new SmsQueueItem(
            eventType,
            recipientName,
            recipientPhone,
            body,
            missionId,
            scheduledAtUtc,
            maximumRetryCount,
            retryIntervalSeconds,
            maximumStatusPollingAttempts);

        db.SmsQueueItems.Add(item);
        await db.SaveChangesAsync(cancellationToken);

        return item.Id;
    }

    /// <summary>جست‌وجوی سوابق پیامک با فیلترهای مستقل و حداکثر ۵۰۰ رکورد.</summary>
    public async Task<IReadOnlyList<SmsQueueItem>> SearchAsync(
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        string? recipientPhone = null,
        SmsEventType? eventType = null,
        SmsQueueStatus? status = null,
        Guid? missionId = null,
        bool errorsOnly = false,
        int take = 200,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.SmsQueueItems.AsNoTracking();

        if (fromUtc.HasValue) query = query.Where(item => item.CreatedAtUtc >= fromUtc.Value);
        if (toUtc.HasValue) query = query.Where(item => item.CreatedAtUtc <= toUtc.Value);
        if (!string.IsNullOrWhiteSpace(recipientPhone))
        {
            var phone = recipientPhone.Trim();
            query = query.Where(item => item.RecipientPhone.Contains(phone));
        }
        if (eventType.HasValue) query = query.Where(item => item.EventType == eventType.Value);
        if (status.HasValue) query = query.Where(item => item.Status == status.Value);
        if (missionId.HasValue) query = query.Where(item => item.MissionId == missionId.Value);
        if (errorsOnly)
        {
            query = query.Where(item =>
                item.Status == SmsQueueStatus.Failed
                || item.Status == SmsQueueStatus.PermanentFailure);
        }

        return await query
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(cancellationToken);
    }

    /// <summary>قرار دادن پیام ناموفق در صف تلاش مجدد دستی پس از رفع علت خطا.</summary>
    public async Task RetryManuallyAsync(
        Guid queueItemId,
        string actorUsername,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var item = await db.SmsQueueItems.SingleOrDefaultAsync(
            queueItem => queueItem.Id == queueItemId,
            cancellationToken)
            ?? throw new KeyNotFoundException("رکورد پیامک پیدا نشد.");

        var before = item.Status.ToString();
        item.RetryManually(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);

        await auditLog.RecordAsync(
            actorUsername,
            "RetrySmsManually",
            "SmsQueueItem",
            item.Id.ToString(),
            "Success",
            beforeJson: System.Text.Json.JsonSerializer.Serialize(new { Status = before }),
            afterJson: System.Text.Json.JsonSerializer.Serialize(new { Status = item.Status.ToString() }),
            cancellationToken: cancellationToken);
    }
}
