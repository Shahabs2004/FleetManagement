using System.Globalization;
using FleetManagement.Domain.Drivers;
using FleetManagement.Domain.Missions;
using FleetManagement.Domain.Notifications;
using FleetManagement.Domain.Vehicles;
using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// مدیریت صف پایدار پیامک، گزارش وضعیت و تلاش مجدد دستی.
/// </summary>
public sealed class SmsQueueService(
    IDbContextFactory<FleetDbContext> dbContextFactory,
    AuditLogService auditLog,
    SmsSettingsService smsSettingsService)
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

    /// <summary>
    /// ساخت پیام‌های یک رویداد مأموریت طبق قالب و قاعده گیرنده تنظیم‌شده.
    /// </summary>
    public async Task<int> EnqueueForMissionAsync(
        Mission mission,
        SmsEventType eventType,
        string? reason = null,
        DateTimeOffset? scheduledAtUtc = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mission);

        var configuration = await smsSettingsService.GetAsync(cancellationToken);
        if (configuration is null)
        {
            return 0;
        }

        var template = configuration.EventTemplates.SingleOrDefault(
            item => item.EventType == eventType && item.IsEnabled);
        if (template is null)
        {
            return 0;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var driver = mission.DriverId.HasValue
            ? await db.Drivers.AsNoTracking().SingleOrDefaultAsync(
                item => item.Id == mission.DriverId.Value,
                cancellationToken)
            : null;
        var vehicle = mission.VehicleId.HasValue
            ? await db.Vehicles.AsNoTracking().SingleOrDefaultAsync(
                item => item.Id == mission.VehicleId.Value,
                cancellationToken)
            : null;

        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["MissionNumber"] = mission.MissionNumber,
            ["MissionTitle"] = mission.Title,
            ["MissionOrderNumber"] = mission.MissionOrderNumber ?? string.Empty,
            ["MissionOrderDate"] = mission.MissionOrderDate?.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) ?? string.Empty,
            ["RequesterName"] = mission.RequesterName,
            ["RequesterPhone"] = mission.RequesterPhoneSnapshot,
            ["DriverName"] = driver is null ? string.Empty : $"{driver.FirstName} {driver.LastName}",
            ["DriverPhone"] = driver?.MobilePhone ?? string.Empty,
            ["Vehicle"] = vehicle is null ? string.Empty : $"{vehicle.Brand} {vehicle.Model}",
            ["LicensePlate"] = vehicle?.LicensePlate ?? string.Empty,
            ["Origin"] = mission.Origin,
            ["Destination"] = mission.Destination,
            ["Route"] = string.Join("، ", mission.GetIntermediateStops()),
            ["StartDateTime"] = FormatTehranTime(mission.PlannedStartAtUtc),
            ["EndDateTime"] = FormatTehranTime(mission.PlannedEndAtUtc),
            ["MissionType"] = mission.Type == MissionType.Freight ? "بار" : "مسافر",
            ["RejectionReason"] = reason ?? mission.DecisionReason ?? string.Empty,
            ["CancellationReason"] = reason ?? mission.DecisionReason ?? string.Empty,
            ["Reason"] = reason ?? mission.DecisionReason ?? string.Empty
        };

        var body = template.Template;
        foreach (var pair in values)
        {
            body = body.Replace($"{{{pair.Key}}}", pair.Value, StringComparison.Ordinal);
        }

        var recipients = new List<(string Name, string Phone)>();
        if (template.RecipientRule is SmsRecipientRule.Requester or SmsRecipientRule.RequesterAndDriver
            && !string.IsNullOrWhiteSpace(mission.RequesterPhoneSnapshot))
        {
            recipients.Add((mission.RequesterName, mission.RequesterPhoneSnapshot));
        }

        if (template.RecipientRule is SmsRecipientRule.Driver or SmsRecipientRule.RequesterAndDriver
            && driver is not null
            && !string.IsNullOrWhiteSpace(driver.MobilePhone))
        {
            recipients.Add(($"{driver.FirstName} {driver.LastName}", driver.MobilePhone));
        }

        var distinctRecipients = recipients
            .GroupBy(item => item.Phone.Trim(), StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();

        var sendAt = scheduledAtUtc ?? DateTimeOffset.UtcNow;
        if (eventType == SmsEventType.MissionStartReminder)
        {
            sendAt = mission.PlannedStartAtUtc.AddMinutes(-Math.Clamp(configuration.ReminderLeadMinutes, 1, 10080));
            if (sendAt <= DateTimeOffset.UtcNow)
            {
                return 0;
            }
        }

        var created = 0;
        foreach (var recipient in distinctRecipients)
        {
            await EnqueueAsync(
                eventType,
                recipient.Name,
                recipient.Phone,
                body,
                mission.Id,
                sendAt,
                configuration.MaximumRetryCount,
                configuration.RetryIntervalSeconds,
                configuration.MaximumStatusPollingAttempts,
                cancellationToken);

            created++;
        }

        return created;
    }

    /// <summary>قالب‌سازی زمان پیامک بر اساس منطقه زمانی تهران.</summary>
    private static string FormatTehranTime(DateTimeOffset utc)
    {
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time"); }
        catch (TimeZoneNotFoundException) { zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran"); }

        var local = TimeZoneInfo.ConvertTime(utc, zone);
        var calendar = new PersianCalendar();
        return $"{calendar.GetYear(local.DateTime):0000}/{calendar.GetMonth(local.DateTime):00}/{calendar.GetDayOfMonth(local.DateTime):00} {local.Hour:00}:00";
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
