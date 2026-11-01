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

        var normalizedPhone = NormalizeIranianMobile(recipientPhone);
        var item = new SmsQueueItem(
            eventType,
            recipientName,
            normalizedPhone,
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
        if (configuration is null
            || await smsSettingsService.IsSkippedAsync(cancellationToken))
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
        if ((template.RecipientRule is SmsRecipientRule.Requester or SmsRecipientRule.RequesterAndDriver)
            && !string.IsNullOrWhiteSpace(mission.RequesterPhoneSnapshot))
        {
            recipients.Add((mission.RequesterName, mission.RequesterPhoneSnapshot));
        }

        if ((template.RecipientRule is SmsRecipientRule.Driver or SmsRecipientRule.RequesterAndDriver)
            && driver is not null
            && !string.IsNullOrWhiteSpace(driver.MobilePhone))
        {
            recipients.Add(($"{driver.FirstName} {driver.LastName}", driver.MobilePhone));
        }

        var distinctRecipients = recipients
            .Select(item => new
            {
                item.Name,
                Phone = TryNormalizeIranianMobile(item.Phone)
            })
            .Where(item => item.Phone is not null)
            .GroupBy(item => item.Phone!, StringComparer.Ordinal)
            .Select(group => (group.First().Name, Phone: group.Key))
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

    /// <summary>تبدیل شماره همراه ایران به قالب ۱۲ رقمی دارای پیش‌شماره کشور.</summary>
    private static string NormalizeIranianMobile(string phone)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phone);
        var digits = new string(phone
            .Where(char.IsDigit)
            .Select(character =>
            {
                var numericValue = (int)char.GetNumericValue(character);
                return numericValue is >= 0 and <= 9 ? (char)('0' + numericValue) : '\0';
            })
            .Where(character => character != '\0')
            .ToArray());

        if (digits.StartsWith("0098", StringComparison.Ordinal))
        {
            digits = digits[2..];
        }
        else if (digits.StartsWith("0", StringComparison.Ordinal))
        {
            digits = digits[1..];
        }

        if (digits.Length == 10 && digits.StartsWith("9", StringComparison.Ordinal))
        {
            digits = "98" + digits;
        }

        if (digits.Length != 12 || !digits.StartsWith("98", StringComparison.Ordinal))
        {
            throw new ArgumentException("شماره همراه برای ارسال پیامک باید با پیش‌شماره ۹۸ و ۱۲ رقم ثبت شود.", nameof(phone));
        }

        return digits;
    }

    /// <summary>تبدیل امن شماره؛ شماره نامعتبر وارد صف پیامک نمی‌شود.</summary>
    private static string? TryNormalizeIranianMobile(string phone)
    {
        try { return NormalizeIranianMobile(phone); }
        catch (ArgumentException) { return null; }
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

    /// <summary>لغو یادآوری‌های هنوز ارسال‌نشده مأموریت.</summary>
    public async Task<int> CancelUnsentRemindersAsync(
        Guid missionId,
        string reason,
        string actorUsername,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var reminders = await db.SmsQueueItems
            .Where(item => item.MissionId == missionId
                && item.EventType == SmsEventType.MissionStartReminder
                && (item.Status == SmsQueueStatus.Queued
                    || item.Status == SmsQueueStatus.RetryScheduled))
            .ToListAsync(cancellationToken);

        foreach (var reminder in reminders)
        {
            reminder.CancelScheduledReminder(reason);
        }

        if (reminders.Count == 0)
        {
            return 0;
        }

        await db.SaveChangesAsync(cancellationToken);
        await auditLog.RecordAsync(
            actorUsername,
            "CancelUnsentSmsReminders",
            "Mission",
            missionId.ToString(),
            "Success",
            reason,
            afterJson: System.Text.Json.JsonSerializer.Serialize(new { CancelledCount = reminders.Count }),
            cancellationToken: cancellationToken);

        return reminders.Count;
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
        string correctionReason,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correctionReason);
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var item = await db.SmsQueueItems.SingleOrDefaultAsync(
            queueItem => queueItem.Id == queueItemId,
            cancellationToken)
            ?? throw new KeyNotFoundException("رکورد پیامک پیدا نشد.");

        var before = new { Status = item.Status.ToString(), item.ErrorReason };
        item.RetryManually(DateTimeOffset.UtcNow, correctionReason);
        await db.SaveChangesAsync(cancellationToken);

        await auditLog.RecordAsync(
            actorUsername,
            "RetrySmsManually",
            "SmsQueueItem",
            item.Id.ToString(),
            "Success",
            correctionReason,
            beforeJson: System.Text.Json.JsonSerializer.Serialize(before),
            afterJson: System.Text.Json.JsonSerializer.Serialize(new
            {
                Status = item.Status.ToString(),
                CorrectionReason = correctionReason
            }),
            cancellationToken: cancellationToken);
    }
}
