using FleetManagement.Domain.Locations;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Globalization;
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
    ISystemSettingsStore settingsStore,
    AuditLogService auditLog,
    NotificationService notificationService,
    SmsQueueService smsQueueService,
    ILogger<MissionManagementService> logger,
    IMissionChangeManagementService changeManagementService,
    IMissionOdometerService odometerService,
    IMissionSplitService splitService)
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
        if (plannedStartAtUtc < now)
        {
            throw new InvalidOperationException("زمان شروع مأموریت نمی‌تواند در گذشته باشد.");
        }

        if (plannedStartAtUtc > now.AddDays(rules.BookingHorizonDays)
            || plannedEndAtUtc > now.AddDays(rules.BookingHorizonDays))
        {
            throw new InvalidOperationException("بازه انتخاب‌شده از افق مجاز رزرو مأموریت دورتر است.");
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
        await notificationService.CreateForRolesAsync(
            ["FleetManager", "Operator"],
            "درخواست مأموریت جدید",
            $"درخواست {mission.MissionNumber} توسط {mission.RequesterName} ثبت شد و در انتظار بررسی است.",
            "MissionRequestCreated",
            "Mission",
            mission.Id.ToString(),
            cancellationToken);

        return mission.Id;
    }

    /// <summary>
    /// ویرایش درخواست فقط تا پیش از شروع بررسی توسط مسئول ناوگان.
    /// </summary>
    public async Task UpdatePendingRequestAsync(
        Guid missionId,
        string requesterUsername,
        string title,
        string requesterPhoneSnapshot,
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
        CancellationToken cancellationToken = default)
    {
        var rules = await GetRulesAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        if (plannedStartAtUtc < now)
        {
            throw new InvalidOperationException("زمان شروع مأموریت نمی‌تواند در گذشته باشد.");
        }

        if (plannedStartAtUtc > now.AddDays(rules.BookingHorizonDays)
            || plannedEndAtUtc > now.AddDays(rules.BookingHorizonDays))
        {
            throw new InvalidOperationException("بازه انتخاب‌شده از افق مجاز رزرو مأموریت دورتر است.");
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var mission = await GetMissionAsync(db, missionId, cancellationToken);
        if (!string.Equals(
            mission.RequesterUsername,
            requesterUsername,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("فقط درخواست‌کننده می‌تواند درخواست خود را ویرایش کند.");
        }

        var stops = intermediateStops?.Select(value => value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray() ?? [];

        var requestedLocations = stops
            .Append(origin.Trim())
            .Append(destination.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var approvedLocations = await db.Locations
            .Where(location => location.Status == LocationStatus.Approved
                && requestedLocations.Contains(location.Name))
            .Select(location => location.Name)
            .ToListAsync(cancellationToken);

        if (approvedLocations.Count != requestedLocations.Length)
        {
            throw new InvalidOperationException("مبدأ، مقصد و توقف‌ها باید از فهرست مکان‌های تأییدشده انتخاب شوند.");
        }

        var before = JsonSerializer.Serialize(new
        {
            mission.Title,
            mission.RequesterPhoneSnapshot,
            mission.RequestingGroup,
            mission.Type,
            mission.Purpose,
            mission.Origin,
            mission.Destination,
            mission.RouteJson,
            mission.PlannedStartAtUtc,
            mission.PlannedEndAtUtc,
            mission.Description,
            mission.PassengerCount,
            mission.MissionOrderNumber,
            mission.MissionOrderDate
        });

        mission.EditPendingRequest(
            title,
            requesterPhoneSnapshot,
            requestingGroup,
            type,
            purpose,
            origin,
            destination,
            stops,
            plannedStartAtUtc,
            plannedEndAtUtc,
            description,
            passengerCount,
            missionOrderNumber,
            missionOrderDate);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await auditLog.RecordAsync(
            requesterUsername,
            "EditPendingRequest",
            "Mission",
            mission.Id.ToString(),
            "Success",
            beforeJson: before,
            afterJson: JsonSerializer.Serialize(new
            {
                mission.Title,
                mission.RequesterPhoneSnapshot,
                mission.RequestingGroup,
                mission.Type,
                mission.Purpose,
                mission.Origin,
                mission.Destination,
                mission.RouteJson,
                mission.PlannedStartAtUtc,
                mission.PlannedEndAtUtc,
                mission.Description,
                mission.PassengerCount,
                mission.MissionOrderNumber,
                mission.MissionOrderDate
            }),
            cancellationToken: cancellationToken);
    }

    /// <summary>شروع بررسی درخواست و ثبت زمان اولین بازبینی.</summary>
    public async Task StartReviewAsync(Guid missionId, string actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var mission = await GetMissionAsync(db, missionId, cancellationToken);
        mission.StartReview(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
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

        await notificationService.CreateForRolesAsync(
            ["FleetManager", "Operator"],
            "مأموریت تأیید شد",
            $"مأموریت {mission.MissionNumber} تأیید و برای اجرا آماده شد.",
            "MissionApproved",
            "Mission",
            mission.Id.ToString(),
            cancellationToken);

        await QueueSmsSafelyAsync(mission, SmsEventType.MissionApproved, null, cancellationToken);
        await QueueSmsSafelyAsync(mission, SmsEventType.MissionStartReminder, null, cancellationToken);
    }

    /// <summary>رد درخواست با علت اجباری.</summary>
    public async Task RejectAsync(Guid missionId, string reason, string actor, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var mission = await GetMissionAsync(db, missionId, cancellationToken);
        mission.Reject(reason);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await RecordMissionAuditAsync(actor, "Reject", mission, "Rejected", cancellationToken, reason);
        await notificationService.CreateAsync(
            mission.RequesterUsername,
            "درخواست مأموریت رد شد",
            $"درخواست {mission.MissionNumber} رد شد. علت: {reason}",
            "MissionRejected",
            "Mission",
            mission.Id.ToString(),
            cancellationToken);

        await notificationService.CreateForRolesAsync(
            ["FleetManager", "Operator"],
            "درخواست مأموریت رد شد",
            $"درخواست {mission.MissionNumber} رد شد.",
            "MissionRejected",
            "Mission",
            mission.Id.ToString(),
            cancellationToken);

        await QueueSmsSafelyAsync(mission, SmsEventType.MissionRejected, reason, cancellationToken);
    }

    /// <summary>لغو مأموریت با علت و ثبت ممیزی.</summary>
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
    public async Task<int> ProcessDueMissionsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
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
        await transaction.CommitAsync(cancellationToken);

        foreach (var item in auditActions)
        {
            await RecordMissionAuditAsync(
                "system",
                item.Action,
                item.Mission,
                "Success",
                cancellationToken);
            if (item.Action == "AutoCloseWithdrawal")
            {
                await CancelUnsentRemindersSafelyAsync(
                    item.Mission.Id,
                    "مهلت مأموریت هنگام بررسی انصراف به پایان رسید.",
                    "system",
                    cancellationToken);
                await QueueSmsSafelyAsync(
                    item.Mission,
                    SmsEventType.MissionCancelled,
                    "پایان زمان برنامه‌ریزی‌شده در زمان بررسی انصراف",
                    cancellationToken);
            }

            if (item.Action == "AutoComplete")
            {
                await notificationService.CreateForRolesAsync(
                    ["Operator"],
                    "مأموریت پایان یافت",
                    $"مأموریت {item.Mission.MissionNumber} به پایان برنامه‌ریزی‌شده رسید.",
                    "MissionCompleted",
                    "Mission",
                    item.Mission.Id.ToString(),
                    cancellationToken);

                await QueueSmsSafelyAsync(
                    item.Mission,
                    SmsEventType.MissionCompleted,
                    null,
                    cancellationToken);
            }
        }

        return dueMissions.Count;
    }

    /// <summary>
    /// دریافت مأموریت‌های دارای راننده که با بازه قابل مشاهده جدول زمانی هم‌پوشانی دارند.
    /// </summary>
    public async Task<IReadOnlyList<Mission>> GetTimelineAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        int take = 1000,
        CancellationToken cancellationToken = default)
    {
        if (toUtc <= fromUtc)
        {
            throw new ArgumentException("پایان بازه باید بعد از شروع آن باشد.");
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await db.Missions
            .AsNoTracking()
            .Where(mission => mission.DriverId.HasValue
                && mission.PlannedStartAtUtc < toUtc
                && mission.PlannedEndAtUtc > fromUtc)
            .OrderBy(mission => mission.PlannedStartAtUtc)
            .Take(Math.Clamp(take, 1, 2000))
            .ToListAsync(cancellationToken);
    }

    /// <summary>دریافت یک مأموریت با شناسه برای نمایش جزئیات و چاپ دستور.</summary>
    public async Task<Mission> GetByIdAsync(Guid missionId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await GetMissionAsync(db, missionId, cancellationToken);
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

        // A serializable range scan alone can deadlock with concurrent inserts when SQL Server
        // chooses a broad scan plan. Serialize number allocation per Jalali year explicitly.
        var lockResource = $"FleetManagement:MissionNumber:{jalaliYear}";
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"DECLARE @lockResult int; EXEC @lockResult = sys.sp_getapplock @Resource = {lockResource}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000; IF @lockResult < 0 THROW 51000, 'Unable to acquire mission number lock.', 1;",
            cancellationToken);

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
