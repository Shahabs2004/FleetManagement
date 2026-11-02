using System.Data;
using System.Text.Json;
using FleetManagement.Application.Configuration;
using FleetManagement.Application.Missions;
using FleetManagement.Domain.Missions;
using FleetManagement.Domain.Notifications;
using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// اجرای گردش انصراف، لغو و خاتمه زودهنگام با حفظ تراکنش‌ها و آثار جانبی.
/// </summary>
public sealed class MissionWithdrawalService(
    IDbContextFactory<FleetDbContext> dbContextFactory,
    ISystemSettingsStore settingsStore,
    AuditLogService auditLog,
    NotificationService notificationService,
    SmsQueueService smsQueueService,
    ILogger<MissionWithdrawalService> logger) : IMissionWithdrawalService
{
    private const string RulesKey = "Missions.Rules";

    private async Task<MissionRulesConfiguration> GetRulesAsync(CancellationToken cancellationToken = default)
    {
        var rules = await settingsStore.GetAsync<MissionRulesConfiguration>(RulesKey, cancellationToken)
            ?? MissionRulesConfiguration.CreateDefault();

        if (rules.MinimumGapHours is < 0 or > 24 || rules.BookingHorizonDays is < 1 or > 730)
        {
            throw new InvalidOperationException("قواعد برنامه‌ریزی مأموریت در تنظیمات معتبر نیست.");
        }

        return rules;
    }

    public async Task CancelAsync(Guid missionId, string reason, string actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var mission = await GetMissionAsync(db, missionId, cancellationToken);
        mission.Cancel(reason, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await RecordMissionAuditAsync(actor, "Cancel", mission, "Success", cancellationToken, reason);
        await notificationService.CreateAsync(
            mission.RequesterUsername,
            "مأموریت لغو شد",
            $"مأموریت {mission.MissionNumber} لغو شد. علت: {reason}",
            "MissionCancelled",
            "Mission",
            mission.Id.ToString(),
            cancellationToken);

        await notificationService.CreateForRolesAsync(
            ["FleetManager", "Operator"],
            "مأموریت لغو شد",
            $"مأموریت {mission.MissionNumber} لغو شد.",
            "MissionCancelled",
            "Mission",
            mission.Id.ToString(),
            cancellationToken);

        await CancelUnsentRemindersSafelyAsync(
            mission.Id,
            "مأموریت لغو شد.",
            actor,
            cancellationToken);
        await QueueSmsSafelyAsync(mission, SmsEventType.MissionCancelled, reason, cancellationToken);
    }

    /// <summary>خاتمه زودهنگام مأموریت با آزادسازی فوری منابع.</summary>
    public async Task EndEarlyAsync(
        Guid missionId,
        DateTimeOffset endedAtUtc,
        string reason,
        string actor,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var mission = await GetMissionAsync(db, missionId, cancellationToken);
        var before = new { mission.Status, mission.PlannedEndAtUtc, mission.ActualEndAtUtc };
        mission.EndEarly(endedAtUtc, reason);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await auditLog.RecordAsync(
            actor, "EndEarly", "Mission", mission.Id.ToString(), "Success", reason,
            beforeJson: JsonSerializer.Serialize(before),
            afterJson: JsonSerializer.Serialize(new { mission.Status, mission.ActualEndAtUtc }),
            cancellationToken: cancellationToken);
        await notificationService.CreateAsync(
            mission.RequesterUsername,
            "مأموریت زودتر از موعد خاتمه یافت",
            $"مأموریت {mission.MissionNumber} پیش از زمان برنامه‌ریزی‌شده خاتمه یافت.",
            "MissionEndedEarly",
            "Mission",
            mission.Id.ToString(),
            cancellationToken);

        await notificationService.CreateForRolesAsync(
            ["FleetManager", "Operator"],
            "مأموریت زودتر از موعد خاتمه یافت",
            $"مأموریت {mission.MissionNumber} پیش از زمان برنامه‌ریزی‌شده خاتمه یافت.",
            "MissionEndedEarly",
            "Mission",
            mission.Id.ToString(),
            cancellationToken);

        await QueueSmsSafelyAsync(mission, SmsEventType.MissionCompleted, reason, cancellationToken);
    }

    /// <summary>لغو مستقیم درخواست تأییدنشده توسط درخواست‌کننده.</summary>
    public async Task CancelPendingRequestAsync(
        Guid missionId,
        string requesterUsername,
        string reason,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var mission = await GetMissionAsync(db, missionId, cancellationToken);
        if (!string.Equals(mission.RequesterUsername, requesterUsername, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("فقط درخواست‌کننده می‌تواند درخواست خود را لغو کند.");
        }

        if (mission.Status is not (MissionStatus.Pending or MissionStatus.UnderReview))
        {
            throw new InvalidOperationException("لغو مستقیم فقط برای درخواست تأییدنشده مجاز است.");
        }

        mission.Cancel(reason, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await RecordMissionAuditAsync(requesterUsername, "CancelPendingRequest", mission, "Success", cancellationToken, reason);
    }

    /// <summary>درخواست انصراف توسط درخواست‌کننده.</summary>
    public async Task RequestWithdrawalAsync(
        Guid missionId,
        string requesterUsername,
        string reason,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var mission = await GetMissionAsync(db, missionId, cancellationToken);
        if (!string.Equals(mission.RequesterUsername, requesterUsername, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("فقط درخواست‌کننده می‌تواند برای مأموریت خود درخواست انصراف ثبت کند.");
        }

        if (mission.Status is MissionStatus.Pending or MissionStatus.UnderReview)
        {
            throw new InvalidOperationException("درخواست تأییدنشده باید مستقیماً لغو شود و نیاز به درخواست انصراف ندارد.");
        }

        mission.RequestWithdrawal(reason);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await RecordMissionAuditAsync(requesterUsername, "RequestWithdrawal", mission, "Success", cancellationToken, reason);
        await notificationService.CreateForRolesAsync(
            ["FleetManager", "Operator"],
            "درخواست انصراف مأموریت",
            $"برای مأموریت {mission.MissionNumber} درخواست انصراف ثبت شده است.",
            "MissionWithdrawalRequested",
            "Mission",
            mission.Id.ToString(),
            cancellationToken);
    }

    /// <summary>تأیید درخواست انصراف و آزادسازی منابع.</summary>
    public async Task ApproveWithdrawalAsync(
        Guid missionId,
        string reviewer,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var mission = await GetMissionAsync(db, missionId, cancellationToken);
        mission.ApproveWithdrawal(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await RecordMissionAuditAsync(reviewer, "ApproveWithdrawal", mission, "Success", cancellationToken);
        await notificationService.CreateAsync(
            mission.RequesterUsername,
            "درخواست انصراف تأیید شد",
            $"درخواست انصراف مأموریت {mission.MissionNumber} تأیید شد.",
            "WithdrawalApproved",
            "Mission",
            mission.Id.ToString(),
            cancellationToken);

        await notificationService.CreateForRolesAsync(
            ["FleetManager", "Operator"],
            "درخواست انصراف تأیید شد",
            $"انصراف از مأموریت {mission.MissionNumber} تأیید شد.",
            "WithdrawalApproved",
            "Mission",
            mission.Id.ToString(),
            cancellationToken);

        await CancelUnsentRemindersSafelyAsync(
            mission.Id,
            "درخواست انصراف تأیید شد.",
            reviewer,
            cancellationToken);
        await QueueSmsSafelyAsync(mission, SmsEventType.WithdrawalApproved, null, cancellationToken);
    }

    /// <summary>رد درخواست انصراف و بازگرداندن وضعیت قبلی.</summary>
    public async Task RejectWithdrawalAsync(
        Guid missionId,
        string reason,
        string reviewer,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var mission = await GetMissionAsync(db, missionId, cancellationToken);
        mission.RejectWithdrawal(reason);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await RecordMissionAuditAsync(reviewer, "RejectWithdrawal", mission, "Rejected", cancellationToken, reason);
        await notificationService.CreateAsync(
            mission.RequesterUsername,
            "درخواست انصراف رد شد",
            $"درخواست انصراف مأموریت {mission.MissionNumber} رد شد. علت: {reason}",
            "WithdrawalRejected",
            "Mission",
            mission.Id.ToString(),
            cancellationToken);

        await notificationService.CreateForRolesAsync(
            ["FleetManager", "Operator"],
            "درخواست انصراف رد شد",
            $"درخواست انصراف مأموریت {mission.MissionNumber} رد شد.",
            "WithdrawalRejected",
            "Mission",
            mission.Id.ToString(),
            cancellationToken);

        await QueueSmsSafelyAsync(mission, SmsEventType.WithdrawalRejected, reason, cancellationToken);
    }

    /// <summary>تمدید مأموریت با بررسی تعارض جدید برای همان راننده و خودرو.</summary>

    private static async Task<Mission> GetMissionAsync(
        FleetDbContext db,
        Guid missionId,
        CancellationToken cancellationToken)
    {
        return await db.Missions.SingleOrDefaultAsync(
            mission => mission.Id == missionId,
            cancellationToken)
            ?? throw new KeyNotFoundException("مأموریت موردنظر پیدا نشد.");
    }

    private async Task QueueSmsSafelyAsync(
        Mission mission,
        SmsEventType eventType,
        string? reason,
        CancellationToken cancellationToken)
    {
        try
        {
            await smsQueueService.EnqueueForMissionAsync(
                mission,
                eventType,
                reason,
                cancellationToken: cancellationToken);
        }
        catch (Exception exception)
        {
            var trackingId = Guid.NewGuid().ToString("N");
            logger.LogWarning(
                exception,
                "صف‌بندی پیامک رویداد {EventType} برای مأموریت {MissionId} ناموفق بود. شناسه پیگیری: {TrackingId}",
                eventType,
                mission.Id,
                trackingId);
        }
    }

    private async Task CancelUnsentRemindersSafelyAsync(
        Guid missionId,
        string reason,
        string actor,
        CancellationToken cancellationToken)
    {
        try
        {
            await smsQueueService.CancelUnsentRemindersAsync(
                missionId,
                reason,
                actor,
                cancellationToken);
        }
        catch (Exception exception)
        {
            var trackingId = Guid.NewGuid().ToString("N");
            logger.LogWarning(
                exception,
                "لغو یادآوری‌های پیامکی مأموریت {MissionId} ناموفق بود. شناسه پیگیری: {TrackingId}",
                missionId,
                trackingId);
        }
    }

    private Task RecordMissionAuditAsync(
        string actor,
        string action,
        Mission mission,
        string outcome,
        CancellationToken cancellationToken,
        string? reason = null)
    {
        return auditLog.RecordAsync(
            actor,
            action,
            "Mission",
            mission.Id.ToString(),
            outcome,
            reason,
            afterJson: JsonSerializer.Serialize(new
            {
                mission.MissionNumber,
                mission.Status,
                mission.DriverId,
                mission.VehicleId,
                mission.PlannedStartAtUtc,
                mission.PlannedEndAtUtc,
                mission.ActualStartAtUtc,
                mission.ActualEndAtUtc
            }),
            cancellationToken: cancellationToken);
    }
}
