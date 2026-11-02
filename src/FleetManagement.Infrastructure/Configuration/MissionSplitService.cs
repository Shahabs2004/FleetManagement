using System.Data;
using System.Globalization;
using System.Text.Json;
using FleetManagement.Application.Configuration;
using FleetManagement.Application.Missions;
using FleetManagement.Domain.Drivers;
using FleetManagement.Domain.Missions;
using FleetManagement.Domain.Vehicles;
using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// اجرای تراکنشی تقسیم مأموریت و ایجاد بخش دوم با کنترل منابع و کیلومترشمار.
/// </summary>
public sealed class MissionSplitService(
    IDbContextFactory<FleetDbContext> dbContextFactory,
    IMissionRulesService rulesService,
    AuditLogService auditLog,
    NotificationService notificationService) : IMissionSplitService
{
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
        var rules = await rulesService.GetRulesAsync(cancellationToken);
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
}
