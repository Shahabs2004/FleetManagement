using System.Data;
using System.Text.Json;
using FleetManagement.Application.Configuration;
using FleetManagement.Application.Missions;
using FleetManagement.Application.Notifications;
using FleetManagement.Domain.Drivers;
using FleetManagement.Domain.Missions;
using FleetManagement.Domain.Notifications;
using FleetManagement.Domain.Vehicles;
using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// اجرای گردش درخواست تغییر مأموریت و اعمال اتمیک پیشنهادهای تأییدشده.
/// </summary>
public sealed class MissionChangeManagementService(
    IDbContextFactory<FleetDbContext> dbContextFactory,
    ISystemSettingsStore settingsStore,
    AuditLogService auditLog,
    NotificationService notificationService,
    SmsQueueService smsQueueService,
    ILogger<MissionChangeManagementService> logger) : IMissionChangeManagementService
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

    public async Task<Guid> RequestChangeAsync(
        Guid missionId,
        string requesterUsername,
        string reason,
        MissionChangeProposal proposal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var mission = await GetMissionAsync(db, missionId, cancellationToken);

        if (!string.Equals(mission.RequesterUsername, requesterUsername, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("فقط درخواست‌کننده می‌تواند برای مأموریت خود درخواست تغییر ثبت کند.");
        }

        if (mission.Status is not (MissionStatus.Confirmed or MissionStatus.Running))
        {
            throw new InvalidOperationException("درخواست تغییر پس از تأیید مأموریت قابل ثبت است.");
        }

        var hasOpenRequest = await db.MissionChangeRequests.AnyAsync(
            item => item.MissionId == missionId
                && (item.Status == MissionChangeRequestStatus.Pending
                    || item.Status == MissionChangeRequestStatus.UnderReview),
            cancellationToken);

        if (hasOpenRequest)
        {
            throw new InvalidOperationException("برای این مأموریت یک درخواست تغییر در حال بررسی وجود دارد.");
        }

        var json = JsonSerializer.Serialize(proposal);
        var changeRequest = new MissionChangeRequest(missionId, requesterUsername, reason, json);
        db.MissionChangeRequests.Add(changeRequest);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await auditLog.RecordAsync(
            requesterUsername,
            "RequestChange",
            "Mission",
            missionId.ToString(),
            "Success",
            reason,
            afterJson: JsonSerializer.Serialize(new { ChangeRequestId = changeRequest.Id }),
            cancellationToken: cancellationToken);

        return changeRequest.Id;
    }

    /// <summary>دریافت درخواست‌های تغییر برای بازبین یا درخواست‌کننده.</summary>
    public async Task<IReadOnlyList<MissionChangeRequest>> GetChangeRequestsAsync(
        string? requesterUsername = null,
        MissionChangeRequestStatus? status = null,
        int take = 200,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.MissionChangeRequests.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(requesterUsername))
        {
            query = query.Where(item => item.RequesterUsername == requesterUsername);
        }

        if (status.HasValue)
        {
            query = query.Where(item => item.Status == status.Value);
        }

        return await query
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(cancellationToken);
    }

    /// <summary>شروع بررسی درخواست تغییر و قفل‌کردن آن برای نخستین بازبین.</summary>
    public async Task StartChangeReviewAsync(
        Guid changeRequestId,
        string reviewer,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var change = await db.MissionChangeRequests.SingleOrDefaultAsync(
            item => item.Id == changeRequestId,
            cancellationToken)
            ?? throw new KeyNotFoundException("درخواست تغییر پیدا نشد.");

        change.StartReview(reviewer, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await auditLog.RecordAsync(
            reviewer, "StartChangeReview", "MissionChangeRequest", change.Id.ToString(), "Success",
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// تأیید و اعمال اتمیک تمام مقادیر پیشنهادی پس از بررسی منابع، زمان و مکان‌ها.
    /// </summary>
    public async Task ApproveChangeAsync(
        Guid changeRequestId,
        string reviewer,
        CancellationToken cancellationToken = default)
    {
        var rules = await GetRulesAsync(cancellationToken);
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var change = await db.MissionChangeRequests.SingleOrDefaultAsync(
            item => item.Id == changeRequestId,
            cancellationToken)
            ?? throw new KeyNotFoundException("درخواست تغییر پیدا نشد.");

        var mission = await GetMissionAsync(db, change.MissionId, cancellationToken);
        var proposal = JsonSerializer.Deserialize<MissionChangeProposal>(change.ProposedValuesJson)
            ?? throw new InvalidOperationException("اطلاعات پیشنهادی درخواست تغییر قابل خواندن نیست.");

        if (proposal.PlannedEndAtUtc <= proposal.PlannedStartAtUtc)
        {
            throw new InvalidOperationException("زمان پایان پیشنهادی باید بعد از زمان شروع باشد.");
        }

        var locationNames = proposal.IntermediateStops
            .Append(proposal.Origin)
            .Append(proposal.Destination)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var approvedLocationNames = await db.Locations
            .Where(location => location.Status == FleetManagement.Domain.Locations.LocationStatus.Approved
                && locationNames.Contains(location.Name))
            .Select(location => location.Name)
            .ToListAsync(cancellationToken);

        if (approvedLocationNames.Count != locationNames.Count)
        {
            throw new InvalidOperationException("یکی از مکان‌های پیشنهادی دیگر تأییدشده یا قابل انتخاب نیست.");
        }

        var driver = await db.Drivers.SingleOrDefaultAsync(item => item.Id == proposal.DriverId, cancellationToken)
            ?? throw new KeyNotFoundException("راننده پیشنهادی پیدا نشد.");
        var vehicle = await db.Vehicles.SingleOrDefaultAsync(item => item.Id == proposal.VehicleId, cancellationToken)
            ?? throw new KeyNotFoundException("خودروی پیشنهادی پیدا نشد.");

        if (!driver.CanBeAssignedToMission())
        {
            throw new InvalidOperationException("راننده پیشنهادی فعال و قابل تخصیص نیست.");
        }

        if (!vehicle.CanBeAssignedToMission(hasActiveMission: false))
        {
            throw new InvalidOperationException("خودروی پیشنهادی آماده تخصیص نیست.");
        }

        if (await HasResourceConflictAsync(
            db,
            mission,
            proposal.DriverId,
            proposal.VehicleId,
            TimeSpan.FromHours(rules.MinimumGapHours),
            cancellationToken,
            proposal.PlannedEndAtUtc,
            proposal.PlannedStartAtUtc))
        {
            throw new InvalidOperationException("زمان یا منابع پیشنهادی با مأموریت دیگری تعارض دارد.");
        }

        if (mission.VehicleId != proposal.VehicleId)
        {
            if (!proposal.OdometerStartKm.HasValue)
            {
                throw new InvalidOperationException("برای تغییر خودرو، کیلومترشمار شروع خودروی جدید الزامی است.");
            }

            var lastOdometer = await db.Missions
                .Where(item => item.VehicleId == proposal.VehicleId && item.OdometerEndKm.HasValue)
                .OrderByDescending(item => item.ActualEndAtUtc ?? item.PlannedEndAtUtc)
                .Select(item => item.OdometerEndKm)
                .FirstOrDefaultAsync(cancellationToken);

            if (lastOdometer.HasValue && proposal.OdometerStartKm.Value < lastOdometer.Value)
            {
                throw new InvalidOperationException("کیلومترشمار شروع خودروی جدید از آخرین مقدار ثبت‌شده کمتر است.");
            }
        }

        var before = JsonSerializer.Serialize(new
        {
            mission.Title,
            mission.Type,
            mission.Origin,
            mission.Destination,
            mission.PlannedStartAtUtc,
            mission.PlannedEndAtUtc,
            mission.DriverId,
            mission.VehicleId,
            mission.RequesterPhoneSnapshot
        });

        var previousStartAtUtc = mission.PlannedStartAtUtc;
        var previousDriverId = mission.DriverId;
        var previousVehicleId = mission.VehicleId;

        mission.ApplyApprovedChanges(
            proposal.Title,
            proposal.Type,
            proposal.Purpose,
            proposal.Origin,
            proposal.Destination,
            proposal.IntermediateStops,
            proposal.PlannedStartAtUtc,
            proposal.PlannedEndAtUtc,
            proposal.DriverId,
            proposal.VehicleId,
            proposal.RequesterName,
            proposal.RequesterPhoneSnapshot,
            proposal.RequesterOrganizationalUnit,
            proposal.RequestingGroup,
            proposal.Description,
            proposal.PassengerCount,
            proposal.MissionOrderNumber,
            proposal.MissionOrderDate,
            proposal.OdometerStartKm);

        change.Approve(reviewer, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await auditLog.RecordAsync(
            reviewer,
            "ApproveChange",
            "Mission",
            mission.Id.ToString(),
            "Success",
            change.Reason,
            beforeJson: before,
            afterJson: JsonSerializer.Serialize(new
            {
                mission.Title,
                mission.Type,
                mission.Origin,
                mission.Destination,
                mission.PlannedStartAtUtc,
                mission.PlannedEndAtUtc,
                mission.DriverId,
                mission.VehicleId,
                mission.RequesterPhoneSnapshot
            }),
            cancellationToken: cancellationToken);

        await notificationService.CreateAsync(
            mission.RequesterUsername,
            "تغییرات مأموریت تأیید شد",
            $"تغییرات درخواستی مأموریت {mission.MissionNumber} اعمال شد.",
            "MissionChangeApproved",
            "Mission",
            mission.Id.ToString(),
            cancellationToken);

        await notificationService.CreateForRolesAsync(
            ["FleetManager", "Operator"],
            "تغییرات مأموریت اعمال شد",
            $"تغییرات مأموریت {mission.MissionNumber} تأیید و اعمال شد.",
            "MissionChangeApproved",
            "Mission",
            mission.Id.ToString(),
            cancellationToken);

        await QueueSmsSafelyAsync(mission, SmsEventType.MissionChanged, change.Reason, cancellationToken);

        if (previousDriverId != mission.DriverId || previousVehicleId != mission.VehicleId)
        {
            await QueueSmsSafelyAsync(
                mission,
                SmsEventType.DriverOrVehicleChanged,
                change.Reason,
                cancellationToken);
        }

        if (previousStartAtUtc != mission.PlannedStartAtUtc)
        {
            await CancelUnsentRemindersSafelyAsync(
                mission.Id,
                "زمان شروع مأموریت تغییر کرد.",
                reviewer,
                cancellationToken);
            await QueueSmsSafelyAsync(
                mission,
                SmsEventType.MissionStartReminder,
                null,
                cancellationToken);
        }
    }

    /// <summary>رد درخواست تغییر بدون دست‌زدن به مقادیر مأموریت اصلی.</summary>
    public async Task RejectChangeAsync(
        Guid changeRequestId,
        string reviewer,
        string reason,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var change = await db.MissionChangeRequests.SingleOrDefaultAsync(
            item => item.Id == changeRequestId,
            cancellationToken)
            ?? throw new KeyNotFoundException("درخواست تغییر پیدا نشد.");

        change.Reject(reviewer, reason, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var mission = await GetMissionAsync(db, change.MissionId, cancellationToken);
        await auditLog.RecordAsync(
            reviewer, "RejectChange", "Mission", mission.Id.ToString(), "Rejected",
            reason,
            afterJson: JsonSerializer.Serialize(new { ChangeRequestId = change.Id }),
            cancellationToken: cancellationToken);

        await notificationService.CreateAsync(
            change.RequesterUsername,
            "درخواست تغییر مأموریت رد شد",
            $"درخواست تغییر مأموریت {mission.MissionNumber} رد شد. علت: {reason}",
            "MissionChangeRejected",
            "Mission",
            mission.Id.ToString(),
            cancellationToken);
    }

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
}
