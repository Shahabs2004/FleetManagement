using System.Data;
using System.Globalization;
using System.Text.Json;
using FleetManagement.Application.Configuration;
using FleetManagement.Application.Missions;
using FleetManagement.Domain.Drivers;
using FleetManagement.Domain.Locations;
using FleetManagement.Domain.Missions;
using FleetManagement.Domain.Notifications;
using FleetManagement.Domain.Vehicles;
using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// اجرای ثبت و گردش تأیید درخواست مأموریت.
/// </summary>
public sealed class MissionRequestService(
    IDbContextFactory<FleetDbContext> dbContextFactory,
    ISystemSettingsStore settingsStore,
    AuditLogService auditLog,
    NotificationService notificationService,
    SmsQueueService smsQueueService,
    ILogger<MissionRequestService> logger) : IMissionRequestService
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
    /// <summary>لغو مستقیم مأموریت توسط مدیر/اپراتور مجاز.</summary>

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
