using FleetManagement.Domain.Locations;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Text.Json;
using FleetManagement.Application.Configuration;
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
    ILogger<MissionManagementService> logger)
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

        if (mission.Status != MissionStatus.Pending)
        {
            throw new InvalidOperationException("لغو مستقیم فقط برای درخواست در انتظار بررسی مجاز است.");
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

        if (mission.Status == MissionStatus.Pending)
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
    public async Task SetOdometerAsync(
        Guid missionId,
        long? startKm,
        long? endKm,
        string actor,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var mission = await GetMissionAsync(db, missionId, cancellationToken);
        var before = new { mission.OdometerStartKm, mission.OdometerEndKm };
        if ((startKm.HasValue || endKm.HasValue) && !mission.VehicleId.HasValue)
        {
            throw new InvalidOperationException("ابتدا باید خودرو برای مأموریت تعیین شده باشد.");
        }

        var effectiveEndAtUtc = mission.ActualEndAtUtc ?? mission.PlannedEndAtUtc;
        var resultingStartKm = startKm ?? mission.OdometerStartKm;
        var resultingEndKm = endKm ?? mission.OdometerEndKm;

        if (mission.VehicleId.HasValue && resultingStartKm.HasValue)
        {
            var previousEnd = await db.Missions
                .Where(item => item.Id != mission.Id
                    && item.VehicleId == mission.VehicleId
                    && item.OdometerEndKm.HasValue
                    && (item.ActualEndAtUtc ?? item.PlannedEndAtUtc) <= mission.PlannedStartAtUtc)
                .OrderByDescending(item => item.ActualEndAtUtc ?? item.PlannedEndAtUtc)
                .Select(item => item.OdometerEndKm)
                .FirstOrDefaultAsync(cancellationToken);

            if (previousEnd.HasValue && resultingStartKm.Value < previousEnd.Value)
            {
                throw new InvalidOperationException("کیلومترشمار شروع از آخرین مقدار ثبت‌شده برای این خودرو کمتر است.");
            }
        }

        var lastCurrentMissionOdometer = resultingEndKm ?? resultingStartKm;
        if (mission.VehicleId.HasValue && lastCurrentMissionOdometer.HasValue)
        {
            var nextStart = await db.Missions
                .Where(item => item.Id != mission.Id
                    && item.VehicleId == mission.VehicleId
                    && item.OdometerStartKm.HasValue
                    && item.PlannedStartAtUtc >= effectiveEndAtUtc)
                .OrderBy(item => item.PlannedStartAtUtc)
                .Select(item => item.OdometerStartKm)
                .FirstOrDefaultAsync(cancellationToken);

            if (nextStart.HasValue && lastCurrentMissionOdometer.Value > nextStart.Value)
            {
                throw new InvalidOperationException("مقدار کیلومترشمار این مأموریت از شروع مأموریت بعدی بیشتر است.");
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
        await transaction.CommitAsync(cancellationToken);
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
        await notificationService.CreateForRolesAsync(
            ["FleetManager", "Operator"],
            "مأموریت تقسیم شد",
            $"مأموریت {first.MissionNumber} به دو مأموریت مستقل تقسیم شد. شماره بخش دوم: {second.MissionNumber}.",
            "MissionSplit",
            "Mission",
            first.Id.ToString(),
            cancellationToken);

        return second.Id;
    }

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
