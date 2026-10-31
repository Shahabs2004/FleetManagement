using System.Data;
using System.Globalization;
using System.Text.Json;
using FleetManagement.Application.Configuration;
using FleetManagement.Domain.Drivers;
using FleetManagement.Domain.Missions;
using FleetManagement.Domain.Vehicles;
using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// گردش اصلی درخواست، تأیید، اجرای مأموریت و کنترل تعارض منابع.
/// </summary>
public sealed class MissionManagementService(
    IDbContextFactory<FleetDbContext> dbContextFactory,
    ISystemSettingsStore settingsStore,
    AuditLogService auditLog,
    NotificationService notificationService)
{
    private const string RulesKey = "Missions.Rules";

    /// <summary>خواندن قواعد برنامه‌ریزی مأموریت.</summary>
    public async Task<MissionRulesConfiguration> GetRulesAsync(CancellationToken cancellationToken = default)
    {
        var rules = await settingsStore.GetAsync<MissionRulesConfiguration>(RulesKey, cancellationToken)
            ?? MissionRulesConfiguration.CreateDefault();

        if (rules.MinimumGapHours is < 0 or > 24 || rules.BookingHorizonDays is < 1 or > 730)
        {
            throw new InvalidOperationException("قواعد برنامه‌ریزی مأموریت در تنظیمات معتبر نیست.");
        }

        return rules;
    }

    /// <summary>ذخیره قواعد فاصله بین مأموریت‌ها و افق رزرو.</summary>
    public async Task SaveRulesAsync(
        MissionRulesConfiguration rules,
        string actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (rules.MinimumGapHours is < 0 or > 24 || rules.BookingHorizonDays is < 1 or > 730)
        {
            throw new ArgumentOutOfRangeException(nameof(rules), "مقادیر قواعد مأموریت خارج از محدوده مجاز هستند.");
        }

        await settingsStore.SetAsync(RulesKey, rules, cancellationToken);
        await auditLog.RecordAsync(
            actor, "UpdateRules", "MissionRules", null, "Success",
            afterJson: JsonSerializer.Serialize(rules),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// ثبت درخواست تغییر مأموریت؛ مقادیر اصلی تا زمان تأیید تغییر نمی‌کنند.
    /// </summary>
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

    /// <summary>
    /// ثبت درخواست مأموریت؛ درخواست‌های در انتظار منابع را رزرو نمی‌کنند.
    /// شماره سالانه در تراکنش Serializable تولید می‌شود تا تکراری نباشد.
    /// </summary>
    public async Task<Guid> CreateRequestAsync(
        string title,
        string requesterUsername,
        string requesterName,
        string requesterPhoneSnapshot,
        string requesterOrganizationalUnit,
        string requestingGroup,
        MissionType type,
        string purpose,
        string origin,
        string destination,
        IEnumerable<string>? intermediateStops,
        DateTimeOffset plannedStartAtUtc,
        DateTimeOffset plannedEndAtUtc,
        string? description,
        int? passengerCount,
        string? missionOrderNumber,
        DateOnly? missionOrderDate,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var rules = await GetRulesAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        if (plannedStartAtUtc > now.AddDays(rules.BookingHorizonDays))
        {
            throw new InvalidOperationException("زمان انتخاب‌شده از افق مجاز رزرو مأموریت دورتر است.");
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var missionNumber = await GenerateMissionNumberAsync(db, now, cancellationToken);
        var mission = new Mission(
            missionNumber,
            title,
            requesterUsername,
            requesterName,
            requesterPhoneSnapshot,
            requesterOrganizationalUnit,
            requestingGroup,
            type,
            purpose,
            origin,
            destination,
            intermediateStops,
            plannedStartAtUtc,
            plannedEndAtUtc,
            description,
            passengerCount,
            missionOrderNumber,
            missionOrderDate);

        db.Missions.Add(mission);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await auditLog.RecordAsync(
            actor, "CreateRequest", "Mission", mission.Id.ToString(), "Success",
            afterJson: JsonSerializer.Serialize(new
            {
                mission.MissionNumber,
                mission.Title,
                mission.RequesterUsername,
                mission.PlannedStartAtUtc,
                mission.PlannedEndAtUtc,
                mission.Origin,
                mission.Destination
            }),
            cancellationToken: cancellationToken);

        return mission.Id;
    }

    /// <summary>شروع بررسی درخواست و ثبت زمان اولین بازبینی.</summary>
    public async Task StartReviewAsync(Guid missionId, string actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var mission = await GetMissionAsync(db, missionId, cancellationToken);
        mission.StartReview(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        await RecordMissionAuditAsync(actor, "StartReview", mission, "Success", cancellationToken);
    }

    /// <summary>
    /// تأیید مأموریت با کنترل وضعیت راننده، خودرو و تعارض زمانی به‌صورت اتمیک.
    /// </summary>
    public async Task ApproveAsync(
        Guid missionId,
        Guid driverId,
        Guid vehicleId,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var rules = await GetRulesAsync(cancellationToken);
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var mission = await GetMissionAsync(db, missionId, cancellationToken);
        var driver = await db.Drivers.SingleOrDefaultAsync(item => item.Id == driverId, cancellationToken)
            ?? throw new KeyNotFoundException("راننده انتخاب‌شده پیدا نشد.");
        var vehicle = await db.Vehicles.SingleOrDefaultAsync(item => item.Id == vehicleId, cancellationToken)
            ?? throw new KeyNotFoundException("خودروی انتخاب‌شده پیدا نشد.");

        if (!driver.CanBeAssignedToMission())
        {
            throw new InvalidOperationException("راننده فعال و قابل تخصیص نیست.");
        }

        if (!vehicle.CanBeAssignedToMission(hasActiveMission: false))
        {
            throw new InvalidOperationException("خودرو آماده تخصیص نیست.");
        }

        if (await HasResourceConflictAsync(db, mission, driverId, vehicleId, TimeSpan.FromHours(rules.MinimumGapHours), cancellationToken))
        {
            throw new InvalidOperationException("در بازه انتخاب‌شده برای راننده یا خودرو مأموریت متعارض وجود دارد.");
        }

        mission.Approve(driverId, vehicleId, actor, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await RecordMissionAuditAsync(actor, "Approve", mission, "Success", cancellationToken);
        await notificationService.CreateAsync(
            mission.RequesterUsername,
            "درخواست مأموریت تأیید شد",
            $"درخواست {mission.MissionNumber} تأیید شد و راننده و خودرو تخصیص یافتند.",
            "MissionApproved",
            "Mission",
            mission.Id.ToString(),
            cancellationToken);
    }

    /// <summary>رد درخواست با علت اجباری.</summary>
    public async Task RejectAsync(Guid missionId, string reason, string actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var mission = await GetMissionAsync(db, missionId, cancellationToken);
        mission.Reject(reason);
        await db.SaveChangesAsync(cancellationToken);
        await RecordMissionAuditAsync(actor, "Reject", mission, "Rejected", cancellationToken, reason);
        await notificationService.CreateAsync(
            mission.RequesterUsername,
            "درخواست مأموریت رد شد",
            $"درخواست {mission.MissionNumber} رد شد. علت: {reason}",
            "MissionRejected",
            "Mission",
            mission.Id.ToString(),
            cancellationToken);
    }

    /// <summary>لغو مأموریت با علت و ثبت ممیزی.</summary>
    public async Task CancelAsync(Guid missionId, string reason, string actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var mission = await GetMissionAsync(db, missionId, cancellationToken);
        mission.Cancel(reason, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        await RecordMissionAuditAsync(actor, "Cancel", mission, "Success", cancellationToken, reason);
        await notificationService.CreateAsync(
            mission.RequesterUsername,
            "مأموریت لغو شد",
            $"مأموریت {mission.MissionNumber} لغو شد. علت: {reason}",
            "MissionCancelled",
            "Mission",
            mission.Id.ToString(),
            cancellationToken);
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
        var mission = await GetMissionAsync(db, missionId, cancellationToken);
        var before = new { mission.Status, mission.PlannedEndAtUtc, mission.ActualEndAtUtc };
        mission.EndEarly(endedAtUtc, reason);
        await db.SaveChangesAsync(cancellationToken);

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
    }

    /// <summary>لغو مستقیم درخواست تأییدنشده توسط درخواست‌کننده.</summary>
    public async Task CancelPendingRequestAsync(
        Guid missionId,
        string requesterUsername,
        string reason,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var mission = await GetMissionAsync(db, missionId, cancellationToken);
        if (!string.Equals(mission.RequesterUsername, requesterUsername, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("فقط درخواست‌کننده می‌تواند درخواست خود را لغو کند.");
        }

        if (mission.Status != MissionStatus.Pending)
        {
            throw new InvalidOperationException("لغو مستقیم فقط برای درخواست در انتظار بررسی مجاز است.");
        }

        mission.Cancel(reason, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
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
        var mission = await GetMissionAsync(db, missionId, cancellationToken);
        if (!string.Equals(mission.RequesterUsername, requesterUsername, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("فقط درخواست‌کننده می‌تواند برای مأموریت خود درخواست انصراف ثبت کند.");
        }

        if (mission.Status == MissionStatus.Pending)
        {
            throw new InvalidOperationException("درخواست تأییدنشده باید مستقیماً لغو شود و نیاز به درخواست انصراف ندارد.");
        }

        mission.RequestWithdrawal(reason);
        await db.SaveChangesAsync(cancellationToken);
        await RecordMissionAuditAsync(requesterUsername, "RequestWithdrawal", mission, "Success", cancellationToken, reason);
    }

    /// <summary>تأیید درخواست انصراف و آزادسازی منابع.</summary>
    public async Task ApproveWithdrawalAsync(
        Guid missionId,
        string reviewer,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var mission = await GetMissionAsync(db, missionId, cancellationToken);
        mission.ApproveWithdrawal(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        await RecordMissionAuditAsync(reviewer, "ApproveWithdrawal", mission, "Success", cancellationToken);
        await notificationService.CreateAsync(
            mission.RequesterUsername,
            "درخواست انصراف تأیید شد",
            $"درخواست انصراف مأموریت {mission.MissionNumber} تأیید شد.",
            "WithdrawalApproved",
            "Mission",
            mission.Id.ToString(),
            cancellationToken);
    }

    /// <summary>رد درخواست انصراف و بازگرداندن وضعیت قبلی.</summary>
    public async Task RejectWithdrawalAsync(
        Guid missionId,
        string reason,
        string reviewer,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var mission = await GetMissionAsync(db, missionId, cancellationToken);
        mission.RejectWithdrawal(reason);
        await db.SaveChangesAsync(cancellationToken);
        await RecordMissionAuditAsync(reviewer, "RejectWithdrawal", mission, "Rejected", cancellationToken, reason);
        await notificationService.CreateAsync(
            mission.RequesterUsername,
            "درخواست انصراف رد شد",
            $"درخواست انصراف مأموریت {mission.MissionNumber} رد شد. علت: {reason}",
            "WithdrawalRejected",
            "Mission",
            mission.Id.ToString(),
            cancellationToken);
    }

    /// <summary>تمدید مأموریت با بررسی تعارض جدید برای همان راننده و خودرو.</summary>
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
    }

    /// <summary>ثبت یا اصلاح کیلومترشمار شروع و پایان توسط کاربر مجاز.</summary>
    public async Task SetOdometerAsync(
        Guid missionId,
        long? startKm,
        long? endKm,
        string actor,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var mission = await GetMissionAsync(db, missionId, cancellationToken);
        var before = new { mission.OdometerStartKm, mission.OdometerEndKm };
        if ((startKm.HasValue || endKm.HasValue) && !mission.VehicleId.HasValue)
        {
            throw new InvalidOperationException("ابتدا باید خودرو برای مأموریت تعیین شده باشد.");
        }

        var effectiveEndAtUtc = mission.ActualEndAtUtc ?? mission.PlannedEndAtUtc;

        if (mission.VehicleId.HasValue && startKm.HasValue)
        {
            var previousEnd = await db.Missions
                .Where(item => item.Id != mission.Id
                    && item.VehicleId == mission.VehicleId
                    && item.OdometerEndKm.HasValue
                    && (item.ActualEndAtUtc ?? item.PlannedEndAtUtc) <= mission.PlannedStartAtUtc)
                .OrderByDescending(item => item.ActualEndAtUtc ?? item.PlannedEndAtUtc)
                .Select(item => item.OdometerEndKm)
                .FirstOrDefaultAsync(cancellationToken);

            if (previousEnd.HasValue && startKm.Value < previousEnd.Value)
            {
                throw new InvalidOperationException("کیلومترشمار شروع از آخرین مقدار ثبت‌شده برای این خودرو کمتر است.");
            }

        }

        if (mission.VehicleId.HasValue && endKm.HasValue)
        {
            var nextStart = await db.Missions
                .Where(item => item.Id != mission.Id
                    && item.VehicleId == mission.VehicleId
                    && item.OdometerStartKm.HasValue
                    && item.PlannedStartAtUtc >= effectiveEndAtUtc)
                .OrderBy(item => item.PlannedStartAtUtc)
                .Select(item => item.OdometerStartKm)
                .FirstOrDefaultAsync(cancellationToken);

            if (nextStart.HasValue && endKm.Value > nextStart.Value)
            {
                throw new InvalidOperationException("کیلومترشمار پایان از مقدار شروع مأموریت بعدی بیشتر است.");
            }
        }

        if (startKm.HasValue)
        {
            mission.SetOdometerStart(startKm.Value);
        }

        if (endKm.HasValue)
        {
            mission.SetOdometerEnd(endKm.Value);
        }

        await db.SaveChangesAsync(cancellationToken);
        await auditLog.RecordAsync(
            actor, "UpdateOdometer", "Mission", mission.Id.ToString(), "Success",
            beforeJson: JsonSerializer.Serialize(before),
            afterJson: JsonSerializer.Serialize(new { mission.OdometerStartKm, mission.OdometerEndKm, mission.DistanceKm }),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// تقسیم مأموریت در حال اجرا به دو رکورد مستقل با شماره‌های جداگانه.
    /// </summary>
    public async Task<Guid> SplitAsync(
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
    {
        var rules = await GetRulesAsync(cancellationToken);
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var first = await GetMissionAsync(db, missionId, cancellationToken);
        if (first.DriverId is null || first.VehicleId is null)
        {
            throw new InvalidOperationException("مأموریت جاری راننده و خودرو ندارد.");
        }

        var driverChanged = first.DriverId.Value != newDriverId;
        var vehicleChanged = first.VehicleId.Value != newVehicleId;
        if (!driverChanged && !vehicleChanged)
        {
            throw new InvalidOperationException("برای تقسیم باید راننده یا خودرو تغییر کند.");
        }

        var expectedReason = (driverChanged, vehicleChanged) switch
        {
            (true, false) => MissionSplitReason.DriverChange,
            (false, true) => MissionSplitReason.VehicleBreakdown,
            (true, true) => MissionSplitReason.DriverAndVehicleChange,
            _ => splitReason
        };

        if (splitReason != MissionSplitReason.Other && splitReason != expectedReason)
        {
            throw new InvalidOperationException("دلیل انتخاب‌شده با نوع تغییر راننده یا خودرو سازگار نیست.");
        }

        if (splitReason == MissionSplitReason.Other && string.IsNullOrWhiteSpace(explanation))
        {
            throw new InvalidOperationException("برای دلیل «سایر موارد» توضیح الزامی است.");
        }

        var reasonText = splitReason == MissionSplitReason.Other
            ? $"سایر موارد: {explanation!.Trim()}"
            : splitReason switch
            {
                MissionSplitReason.VehicleBreakdown => "خرابی خودرو",
                MissionSplitReason.DriverChange => "تعویض راننده",
                MissionSplitReason.DriverAndVehicleChange => "تعویض هم‌زمان راننده و خودرو",
                _ => throw new ArgumentOutOfRangeException(nameof(splitReason))
            };

        var newDriver = await db.Drivers.SingleOrDefaultAsync(item => item.Id == newDriverId, cancellationToken)
            ?? throw new KeyNotFoundException("راننده جدید پیدا نشد.");
        var newVehicle = await db.Vehicles.SingleOrDefaultAsync(item => item.Id == newVehicleId, cancellationToken)
            ?? throw new KeyNotFoundException("خودروی جدید پیدا نشد.");

        if (!newDriver.CanBeAssignedToMission())
        {
            throw new InvalidOperationException("راننده جدید فعال و قابل تخصیص نیست.");
        }

        if (!newVehicle.CanBeAssignedToMission(hasActiveMission: false))
        {
            throw new InvalidOperationException("خودروی جدید آماده تخصیص نیست.");
        }

        var originalEnd = first.PlannedEndAtUtc;
        var originalStart = first.PlannedStartAtUtc;
        var splitAt = splitAtUtc;
        first.CloseFirstSegmentForSplit(
            splitAt,
            firstSegmentOdometerEndKm,
            reasonText,
            actor);

        // ذخیره موقت بخش اول در همان تراکنش تا هنگام کنترل تعارض، مأموریت جاری منبع را اشغال نکند.
        await db.SaveChangesAsync(cancellationToken);

        var missionNumber = await GenerateMissionNumberAsync(db, DateTimeOffset.UtcNow, cancellationToken);
        var second = new Mission(
            missionNumber,
            first.Title,
            first.RequesterUsername,
            first.RequesterName,
            first.RequesterPhoneSnapshot,
            first.RequesterOrganizationalUnit,
            first.RequestingGroup,
            first.Type,
            first.Purpose,
            first.Origin,
            first.Destination,
            first.GetIntermediateStops(),
            splitAt,
            originalEnd,
            first.Description,
            first.PassengerCount,
            first.MissionOrderNumber,
            first.MissionOrderDate);

        if (newVehicleId == first.VehicleId)
        {
            second.SetOdometerStart(firstSegmentOdometerEndKm);
        }
        else
        {
            if (!secondVehicleOdometerStartKm.HasValue)
            {
                throw new InvalidOperationException("برای خودروی جدید باید کیلومترشمار شروع ثبت شود.");
            }

            var lastRecordedOdometer = await db.Missions
                .Where(item => item.VehicleId == newVehicleId && item.OdometerEndKm.HasValue)
                .OrderByDescending(item => item.ActualEndAtUtc ?? item.PlannedEndAtUtc)
                .Select(item => item.OdometerEndKm)
                .FirstOrDefaultAsync(cancellationToken);

            if (lastRecordedOdometer.HasValue
                && secondVehicleOdometerStartKm.Value < lastRecordedOdometer.Value)
            {
                throw new InvalidOperationException("کیلومترشمار شروع خودروی جدید از آخرین مقدار ثبت‌شده کمتر است.");
            }

            second.SetOdometerStart(secondVehicleOdometerStartKm.Value);
        }

        if (await HasResourceConflictAsync(
            db,
            second,
            newDriverId,
            newVehicleId,
            TimeSpan.FromHours(rules.MinimumGapHours),
            cancellationToken))
        {
            throw new InvalidOperationException("منابع جدید در بازه بخش دوم مأموریت تعارض زمانی دارند.");
        }

        var now = DateTimeOffset.UtcNow;
        second.Approve(newDriverId, newVehicleId, actor, now);
        second.StartAutomatically(splitAt);
        second.MarkAsSecondSplitSegment(first.Id, splitAt, reasonText, actor);
        first.SetRelatedMission(second.Id);

        db.Missions.Add(second);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await auditLog.RecordAsync(
            actor, "SplitFirstSegment", "Mission", first.Id.ToString(), "Success", reasonText,
            afterJson: JsonSerializer.Serialize(new
            {
                first.MissionNumber,
                RelatedMissionId = second.Id,
                first.SplitAtUtc,
                first.OdometerStartKm,
                first.OdometerEndKm
            }),
            cancellationToken: cancellationToken);

        await auditLog.RecordAsync(
            actor, "SplitSecondSegment", "Mission", second.Id.ToString(), "Success", reasonText,
            afterJson: JsonSerializer.Serialize(new
            {
                second.MissionNumber,
                RelatedMissionId = first.Id,
                second.DriverId,
                second.VehicleId,
                second.PlannedStartAtUtc,
                second.PlannedEndAtUtc,
                second.OdometerStartKm
            }),
            cancellationToken: cancellationToken);

        return second.Id;
    }

    /// <summary>
    /// شروع خودکار مأموریت‌های تأییدشده و پایان خودکار مأموریت‌های در حال اجرا.
    /// </summary>
    public async Task<int> ProcessDueMissionsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var dueMissions = await db.Missions
            .Where(mission =>
                (mission.Status == MissionStatus.Confirmed && mission.PlannedStartAtUtc <= now)
                || ((mission.Status == MissionStatus.Running || mission.Status == MissionStatus.WithdrawalPending)
                    && mission.PlannedEndAtUtc <= now))
            .ToListAsync(cancellationToken);

        var auditActions = new List<(Mission Mission, string Action)>();
        foreach (var mission in dueMissions)
        {
            if (mission.Status == MissionStatus.Confirmed)
            {
                mission.StartAutomatically(now);
                auditActions.Add((mission, "AutoStart"));
            }
            else if (mission.Status == MissionStatus.Running)
            {
                mission.CompleteAutomatically();
                auditActions.Add((mission, "AutoComplete"));
            }
            else if (mission.Status == MissionStatus.WithdrawalPending)
            {
                mission.AutoCloseExpiredWithdrawal(now);
                auditActions.Add((mission, "AutoCloseWithdrawal"));
            }
        }

        if (dueMissions.Count == 0)
        {
            return 0;
        }

        await db.SaveChangesAsync(cancellationToken);

        foreach (var item in auditActions)
        {
            await RecordMissionAuditAsync(
                "system",
                item.Action,
                item.Mission,
                "Success",
                cancellationToken);
        }

        return dueMissions.Count;
    }

    /// <summary>جست‌وجوی مأموریت‌ها؛ درخواست‌کننده فقط سوابق خود را دریافت می‌کند.</summary>
    public async Task<IReadOnlyList<Mission>> SearchAsync(
        string? requesterUsername = null,
        MissionStatus? status = null,
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        int take = 200,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Missions.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(requesterUsername))
        {
            query = query.Where(mission => mission.RequesterUsername == requesterUsername);
        }

        if (status.HasValue)
        {
            query = query.Where(mission => mission.Status == status.Value);
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(mission => mission.PlannedStartAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(mission => mission.PlannedStartAtUtc <= toUtc.Value);
        }

        return await query
            .OrderByDescending(mission => mission.CreatedAtUtc)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(cancellationToken);
    }

    /// <summary>تولید شماره یکتای سالانه بر اساس تقویم جلالی در تراکنش سریال‌پذیر.</summary>
    private static async Task<string> GenerateMissionNumberAsync(
        FleetDbContext db,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        TimeZoneInfo tehranZone;
        try
        {
            tehranZone = TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            tehranZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");
        }

        var localNow = TimeZoneInfo.ConvertTime(nowUtc, tehranZone);
        var jalaliYear = new PersianCalendar().GetYear(localNow.DateTime);
        var prefix = $"{jalaliYear}-";
        var numbers = await db.Missions
            .Where(mission => mission.MissionNumber.StartsWith(prefix))
            .Select(mission => mission.MissionNumber)
            .ToListAsync(cancellationToken);

        var lastNumber = numbers
            .Select(number => int.TryParse(number.AsSpan(prefix.Length), out var value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{jalaliYear}-{lastNumber + 1:000}";
    }

    /// <summary>بررسی هم‌پوشانی یا فاصله ناکافی برای راننده و خودرو.</summary>
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
