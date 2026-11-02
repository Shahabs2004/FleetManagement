using FleetManagement.Domain.Locations;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Text.Json;
using FleetManagement.Application.Configuration;
using FleetManagement.Application.Missions;
using FleetManagement.Domain.Drivers;
using FleetManagement.Domain.Missions;
using FleetManagement.Domain.Notifications;
using FleetManagement.Domain.Vehicles;
using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// گردش اصلی درخواست، تأیید، اجرای مأموریت و کنترل تعارض منابع.
/// </summary>
public sealed class MissionManagementService(
    IDbContextFactory<FleetDbContext> dbContextFactory,
    IMissionRulesService missionRulesService,
    AuditLogService auditLog,
    NotificationService notificationService,
    SmsQueueService smsQueueService,
    ILogger<MissionManagementService> logger,
    IMissionChangeManagementService changeManagementService,
    IMissionOdometerService odometerService,
    IMissionSplitService splitService,
    IMissionWithdrawalService withdrawalService,
    IMissionRequestService requestService,
    IMissionLifecycleProcessorService lifecycleProcessorService,
    IMissionQueryService queryService)
{
    /// <summary>خواندن قواعد برنامه‌ریزی مأموریت از سناریوی کاربردی.</summary>
    public Task<MissionRulesConfiguration> GetRulesAsync(CancellationToken cancellationToken = default)
        => missionRulesService.GetRulesAsync(cancellationToken);

    /// <summary>ذخیره قواعد برنامه‌ریزی مأموریت از سناریوی کاربردی.</summary>
    public Task SaveRulesAsync(
        MissionRulesConfiguration rules,
        string actor,
        CancellationToken cancellationToken = default)
        => missionRulesService.SaveRulesAsync(rules, actor, cancellationToken);

    /// <summary>ثبت درخواست تغییر مأموریت؛ اجرای سناریو در سرویس تخصصی تغییر انجام می‌شود.</summary>
    public Task<Guid> RequestChangeAsync(
        Guid missionId,
        string requesterUsername,
        string reason,
        MissionChangeProposal proposal,
        CancellationToken cancellationToken = default)
        => changeManagementService.RequestChangeAsync(missionId, requesterUsername, reason, proposal, cancellationToken);

    /// <summary>دریافت درخواست‌های تغییر از سرویس تخصصی تغییر.</summary>
    public Task<IReadOnlyList<MissionChangeRequest>> GetChangeRequestsAsync(
        string? requesterUsername = null,
        MissionChangeRequestStatus? status = null,
        int take = 200,
        CancellationToken cancellationToken = default)
        => changeManagementService.GetChangeRequestsAsync(requesterUsername, status, take, cancellationToken);

    /// <summary>شروع بررسی درخواست تغییر.</summary>
    public Task StartChangeReviewAsync(
        Guid changeRequestId,
        string reviewer,
        CancellationToken cancellationToken = default)
        => changeManagementService.StartChangeReviewAsync(changeRequestId, reviewer, cancellationToken);

    /// <summary>تأیید و اعمال اتمیک پیشنهاد تغییر.</summary>
    public Task ApproveChangeAsync(
        Guid changeRequestId,
        string reviewer,
        CancellationToken cancellationToken = default)
        => changeManagementService.ApproveChangeAsync(changeRequestId, reviewer, cancellationToken);

    /// <summary>رد درخواست تغییر با ثبت علت.</summary>
    public Task RejectChangeAsync(
        Guid changeRequestId,
        string reviewer,
        string reason,
        CancellationToken cancellationToken = default)
        => changeManagementService.RejectChangeAsync(changeRequestId, reviewer, reason, cancellationToken);

    /// <summary>
    /// ثبت درخواست مأموریت؛ درخواست‌های در انتظار منابع را رزرو نمی‌کنند.
    /// شماره سالانه در تراکنش Serializable تولید می‌شود تا تکراری نباشد.
    /// </summary>
    /// <summary>ثبت درخواست مأموریت.</summary>
    public Task<Guid> CreateRequestAsync(
        string title, string requesterUsername, string requesterName, string requesterPhoneSnapshot,
        string requesterOrganizationalUnit, string requestingGroup, MissionType type, string purpose,
        string origin, string destination, IEnumerable<string>? intermediateStops,
        DateTimeOffset plannedStartAtUtc, DateTimeOffset plannedEndAtUtc, string? description,
        int? passengerCount, string? missionOrderNumber, DateOnly? missionOrderDate, string actor,
        CancellationToken cancellationToken = default)
        => requestService.CreateRequestAsync(
            title, requesterUsername, requesterName, requesterPhoneSnapshot, requesterOrganizationalUnit,
            requestingGroup, type, purpose, origin, destination, intermediateStops, plannedStartAtUtc,
            plannedEndAtUtc, description, passengerCount, missionOrderNumber, missionOrderDate, actor,
            cancellationToken);

    /// <summary>ویرایش درخواست تأییدنشده.</summary>
    public Task UpdatePendingRequestAsync(
        Guid missionId, string requesterUsername, string title, string requesterPhoneSnapshot,
        string requestingGroup, MissionType type, string purpose, string origin, string destination,
        IEnumerable<string>? intermediateStops, DateTimeOffset plannedStartAtUtc,
        DateTimeOffset plannedEndAtUtc, string? description, int? passengerCount,
        string? missionOrderNumber, DateOnly? missionOrderDate,
        CancellationToken cancellationToken = default)
        => requestService.UpdatePendingRequestAsync(
            missionId, requesterUsername, title, requesterPhoneSnapshot, requestingGroup, type, purpose,
            origin, destination, intermediateStops, plannedStartAtUtc, plannedEndAtUtc, description,
            passengerCount, missionOrderNumber, missionOrderDate, cancellationToken);

    /// <summary>شروع بررسی درخواست مأموریت.</summary>
    public Task StartReviewAsync(Guid missionId, string actor, CancellationToken cancellationToken = default)
        => requestService.StartReviewAsync(missionId, actor, cancellationToken);

    /// <summary>تأیید و تخصیص منابع به مأموریت.</summary>
    public Task ApproveAsync(Guid missionId, Guid driverId, Guid vehicleId, string actor, CancellationToken cancellationToken = default)
        => requestService.ApproveAsync(missionId, driverId, vehicleId, actor, cancellationToken);

    /// <summary>رد درخواست مأموریت.</summary>
    public Task RejectAsync(Guid missionId, string reason, string actor, CancellationToken cancellationToken = default)
        => requestService.RejectAsync(missionId, reason, actor, cancellationToken);

    public Task CancelAsync(Guid missionId, string reason, string actor, CancellationToken cancellationToken = default)
        => withdrawalService.CancelAsync(missionId, reason, actor, cancellationToken);

    /// <summary>ثبت خاتمه زودهنگام مأموریت در حال اجرا.</summary>
    public Task EndEarlyAsync(Guid missionId, DateTimeOffset endedAtUtc, string reason, string actor, CancellationToken cancellationToken = default)
        => withdrawalService.EndEarlyAsync(missionId, endedAtUtc, reason, actor, cancellationToken);

    /// <summary>لغو مستقیم درخواست تأییدنشده توسط درخواست‌کننده.</summary>
    public Task CancelPendingRequestAsync(Guid missionId, string requesterUsername, string reason, CancellationToken cancellationToken = default)
        => withdrawalService.CancelPendingRequestAsync(missionId, requesterUsername, reason, cancellationToken);

    /// <summary>ثبت درخواست انصراف از مأموریت قطعی.</summary>
    public Task RequestWithdrawalAsync(Guid missionId, string requesterUsername, string reason, CancellationToken cancellationToken = default)
        => withdrawalService.RequestWithdrawalAsync(missionId, requesterUsername, reason, cancellationToken);

    /// <summary>تأیید درخواست انصراف.</summary>
    public Task ApproveWithdrawalAsync(Guid missionId, string reviewer, CancellationToken cancellationToken = default)
        => withdrawalService.ApproveWithdrawalAsync(missionId, reviewer, cancellationToken);

    /// <summary>رد درخواست انصراف.</summary>
    public Task RejectWithdrawalAsync(Guid missionId, string reason, string reviewer, CancellationToken cancellationToken = default)
        => withdrawalService.RejectWithdrawalAsync(missionId, reason, reviewer, cancellationToken);

    public async Task ExtendAsync(
        Guid missionId,
        DateTimeOffset newEndAtUtc,
        string reason,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var rules = await GetRulesAsync(cancellationToken);
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var mission = await GetMissionAsync(db, missionId, cancellationToken);

        if (mission.DriverId is null || mission.VehicleId is null)
        {
            throw new InvalidOperationException("راننده و خودرو برای مأموریت مشخص نشده‌اند.");
        }

        var oldEnd = mission.PlannedEndAtUtc;
        if (await HasResourceConflictAsync(
            db,
            mission,
            mission.DriverId.Value,
            mission.VehicleId.Value,
            TimeSpan.FromHours(rules.MinimumGapHours),
            cancellationToken,
            newEndAtUtc))
        {
            throw new InvalidOperationException("تمدید انتخاب‌شده با مأموریت دیگری تداخل دارد.");
        }

        mission.Extend(newEndAtUtc, reason);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await auditLog.RecordAsync(
            actor, "Extend", "Mission", mission.Id.ToString(), "Success", reason,
            beforeJson: JsonSerializer.Serialize(new { PlannedEndAtUtc = oldEnd }),
            afterJson: JsonSerializer.Serialize(new { PlannedEndAtUtc = newEndAtUtc }),
            cancellationToken: cancellationToken);
        await notificationService.CreateAsync(
            mission.RequesterUsername,
            "زمان مأموریت تغییر کرد",
            $"زمان پایان مأموریت {mission.MissionNumber} به‌روزرسانی شد.",
            "MissionExtended",
            "Mission",
            mission.Id.ToString(),
            cancellationToken);

        await notificationService.CreateForRolesAsync(
            ["FleetManager", "Operator"],
            "زمان مأموریت تغییر کرد",
            $"زمان پایان مأموریت {mission.MissionNumber} به‌روزرسانی شد.",
            "MissionExtended",
            "Mission",
            mission.Id.ToString(),
            cancellationToken);

        await QueueSmsSafelyAsync(mission, SmsEventType.MissionChanged, reason, cancellationToken);
    }

    /// <summary>ثبت یا اصلاح کیلومترشمار شروع و پایان توسط کاربر مجاز.</summary>
    /// <summary>ثبت کیلومترشمار از طریق سرویس تخصصی کیلومتر.</summary>
    public Task SetOdometerAsync(
        Guid missionId,
        long? startKm,
        long? endKm,
        string actor,
        CancellationToken cancellationToken = default)
        => odometerService.SetOdometerAsync(missionId, startKm, endKm, actor, cancellationToken);

    /// <summary>
    /// تقسیم مأموریت در حال اجرا به دو رکورد مستقل با شماره‌های جداگانه.
    /// </summary>
    /// <summary>تقسیم مأموریت از طریق سرویس تخصصی تقسیم.</summary>
    public Task<Guid> SplitAsync(
        Guid missionId,
        Guid newDriverId,
        Guid newVehicleId,
        DateTimeOffset splitAtUtc,
        long firstSegmentOdometerEndKm,
        long? secondVehicleOdometerStartKm,
        MissionSplitReason splitReason,
        string? explanation,
        string actor,
        CancellationToken cancellationToken = default)
        => splitService.SplitAsync(
            missionId,
            newDriverId,
            newVehicleId,
            splitAtUtc,
            firstSegmentOdometerEndKm,
            secondVehicleOdometerStartKm,
            splitReason,
            explanation,
            actor,
            cancellationToken);

    /// <summary>
    /// شروع خودکار مأموریت‌های تأییدشده و پایان خودکار مأموریت‌های در حال اجرا.
    /// </summary>
    /// <summary>پردازش مأموریت‌های سررسیدشده از طریق سرویس تخصصی چرخه عمر.</summary>
    public Task<int> ProcessDueMissionsAsync(CancellationToken cancellationToken = default)
        => lifecycleProcessorService.ProcessDueMissionsAsync(cancellationToken);

    /// <summary>دریافت بازه زمانی مأموریت‌ها.</summary>
    public Task<IReadOnlyList<Mission>> GetTimelineAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        int take = 1000,
        CancellationToken cancellationToken = default)
        => queryService.GetTimelineAsync(fromUtc, toUtc, take, cancellationToken);

    /// <summary>دریافت مأموریت بر اساس شناسه.</summary>
    public Task<Mission> GetByIdAsync(Guid missionId, CancellationToken cancellationToken = default)
        => queryService.GetByIdAsync(missionId, cancellationToken);

    /// <summary>جست‌وجوی مأموریت‌ها بر اساس فیلترهای درخواست‌کننده، وضعیت و تاریخ.</summary>
    public Task<IReadOnlyList<Mission>> SearchAsync(
        string? requesterUsername = null,
        MissionStatus? status = null,
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        int take = 200,
        CancellationToken cancellationToken = default)
        => queryService.SearchAsync(requesterUsername, status, fromUtc, toUtc, take, cancellationToken);

    private static async Task<bool> HasResourceConflictAsync(
        FleetDbContext db,
        Mission current,
        Guid driverId,
        Guid vehicleId,
        TimeSpan gap,
        CancellationToken cancellationToken,
        DateTimeOffset? proposedEnd = null,
        DateTimeOffset? proposedStart = null)
    {
        var end = proposedEnd ?? current.PlannedEndAtUtc;
        var start = proposedStart ?? current.PlannedStartAtUtc;
        var blockingStatuses = new[]
        {
            MissionStatus.Confirmed,
            MissionStatus.Running,
            MissionStatus.WithdrawalPending
        };

        var candidates = await db.Missions
            .Where(existing =>
                existing.Id != current.Id
                && blockingStatuses.Contains(existing.Status)
                && (existing.DriverId == driverId || existing.VehicleId == vehicleId))
            .ToListAsync(cancellationToken);

        // محاسبه بازه‌ها در حافظه انجام می‌شود تا ترجمه DateTimeOffset.Add(TimeSpan) به SQL وابسته نباشد.
        return candidates.Any(existing =>
            start < existing.PlannedEndAtUtc.Add(gap)
            && end.Add(gap) > existing.PlannedStartAtUtc);
    }

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

    /// <summary>
    /// صف‌بندی پیامک پس از ثبت موفق عملیات؛ خطای پیامک هرگز عملیات مأموریت را برنمی‌گرداند.
    /// </summary>
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

    /// <summary>لغو یادآوری‌های قدیمی بدون متوقف‌کردن تغییر مأموریت.</summary>
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
