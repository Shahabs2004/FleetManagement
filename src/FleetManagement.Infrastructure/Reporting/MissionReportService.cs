using System.Globalization;
using FleetManagement.Domain.Missions;
using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FleetManagement.Infrastructure.Reporting;

/// <summary>
/// محاسبه گزارش‌های عملکرد راننده، خودرو، نوع مأموریت، لغو و تصمیم‌ها.
/// مأموریت‌های تقسیم‌شده به‌صورت دو رکورد مستقل محاسبه می‌شوند.
/// </summary>
public sealed class MissionReportService(IDbContextFactory<FleetDbContext> dbContextFactory)
{
    /// <summary>ساخت گزارش یکپارچه بر اساس فیلترهای انتخاب‌شده.</summary>
    public async Task<MissionReportResult> BuildAsync(
        MissionReportFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        if (filter.FromUtc.HasValue && filter.ToUtc.HasValue && filter.FromUtc > filter.ToUtc)
        {
            throw new ArgumentException("ابتدای بازه نباید بعد از انتهای بازه باشد.", nameof(filter));
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Missions.AsNoTracking();

        if (filter.FromUtc.HasValue) query = query.Where(mission => mission.PlannedStartAtUtc >= filter.FromUtc.Value);
        if (filter.ToUtc.HasValue) query = query.Where(mission => mission.PlannedStartAtUtc <= filter.ToUtc.Value);
        if (filter.DriverId.HasValue) query = query.Where(mission => mission.DriverId == filter.DriverId.Value);
        if (filter.VehicleId.HasValue) query = query.Where(mission => mission.VehicleId == filter.VehicleId.Value);
        if (filter.Type.HasValue) query = query.Where(mission => mission.Type == filter.Type.Value);
        if (filter.Status.HasValue) query = query.Where(mission => mission.Status == filter.Status.Value);
        if (!string.IsNullOrWhiteSpace(filter.OrganizationalUnit))
        {
            var unit = filter.OrganizationalUnit.Trim();
            query = query.Where(mission => mission.RequesterOrganizationalUnit.Contains(unit));
        }

        var missions = await query
            .OrderBy(mission => mission.PlannedStartAtUtc)
            .ToListAsync(cancellationToken);

        var driverIds = missions.Where(mission => mission.DriverId.HasValue)
            .Select(mission => mission.DriverId!.Value).Distinct().ToList();
        var vehicleIds = missions.Where(mission => mission.VehicleId.HasValue)
            .Select(mission => mission.VehicleId!.Value).Distinct().ToList();

        var drivers = await db.Drivers.AsNoTracking()
            .Where(driver => driverIds.Contains(driver.Id))
            .ToDictionaryAsync(driver => driver.Id, cancellationToken);
        var vehicles = await db.Vehicles.AsNoTracking()
            .Where(vehicle => vehicleIds.Contains(vehicle.Id))
            .ToDictionaryAsync(vehicle => vehicle.Id, cancellationToken);

        var driverRows = missions
            .Where(mission => mission.DriverId.HasValue && drivers.ContainsKey(mission.DriverId.Value))
            .GroupBy(mission => mission.DriverId!.Value)
            .Select(group =>
            {
                var driver = drivers[group.Key];
                var distances = group.Where(mission => mission.DistanceKm.HasValue)
                    .Select(mission => mission.DistanceKm!.Value).ToList();

                return new DriverPerformanceRow(
                    group.Key,
                    $"{driver.FirstName} {driver.LastName}",
                    distances.Count == 0 ? null : distances.Sum(),
                    group.Count(),
                    group.Sum(GetMissionHours));
            })
            .OrderBy(row => row.DriverName)
            .ToList();

        var vehicleRows = missions
            .Where(mission => mission.VehicleId.HasValue && vehicles.ContainsKey(mission.VehicleId.Value))
            .GroupBy(mission => mission.VehicleId!.Value)
            .Select(group =>
            {
                var vehicle = vehicles[group.Key];
                var distances = group.Where(mission => mission.DistanceKm.HasValue)
                    .Select(mission => mission.DistanceKm!.Value).ToList();

                return new VehiclePerformanceRow(
                    group.Key,
                    $"{vehicle.Brand} {vehicle.Model} - {vehicle.LicensePlate}",
                    distances.Count == 0 ? null : distances.Sum(),
                    group.Count());
            })
            .OrderBy(row => row.VehicleName)
            .ToList();

        var jalaliCalendar = new PersianCalendar();
        var monthlyRows = missions
            .Where(mission => mission.DriverId.HasValue
                && mission.DistanceKm.HasValue
                && drivers.ContainsKey(mission.DriverId.Value))
            .GroupBy(mission =>
            {
                var local = ConvertToTehran(mission.PlannedStartAtUtc);
                return (DriverId: mission.DriverId!.Value,
                    Year: jalaliCalendar.GetYear(local.DateTime),
                    Month: jalaliCalendar.GetMonth(local.DateTime));
            })
            .Select(group => new MonthlyDriverMileageRow(
                group.Key.DriverId,
                $"{drivers[group.Key.DriverId].FirstName} {drivers[group.Key.DriverId].LastName}",
                group.Key.Year,
                group.Key.Month,
                group.Sum(mission => mission.DistanceKm!.Value)))
            .OrderBy(row => row.JalaliYear)
            .ThenBy(row => row.JalaliMonth)
            .ThenBy(row => row.DriverName)
            .ToList();

        var cancelled = missions
            .Where(mission => mission.Status == MissionStatus.Cancelled)
            .Select(mission => new CancelledMissionRow(
                mission.MissionNumber,
                mission.Title,
                mission.DecisionReason,
                mission.CreatedAtUtc,
                mission.RequesterUsername))
            .ToList();

        var approvedCount = missions.Count(mission => mission.ApprovedAtUtc.HasValue);
        var rejectedCount = missions.Count(mission => mission.Status == MissionStatus.Rejected);

        return new MissionReportResult(
            driverRows,
            vehicleRows,
            monthlyRows,
            cancelled,
            new MissionDecisionSummary(approvedCount, rejectedCount),
            missions.Count(mission => mission.Type == MissionType.Freight),
            missions.Count(mission => mission.Type == MissionType.Passenger));
    }

    /// <summary>
    /// محاسبه ساعت مأموریت بر اساس قواعد پایان عادی، خاتمه زودهنگام و لغو.
    /// </summary>
    private static decimal GetMissionHours(Mission mission)
    {
        if (mission.Status == MissionStatus.Cancelled && !mission.ActualStartAtUtc.HasValue)
        {
            return 0;
        }

        var start = mission.ActualStartAtUtc ?? mission.PlannedStartAtUtc;
        var end = mission.ActualEndAtUtc ?? mission.PlannedEndAtUtc;
        if (end <= start) return 0;
        return (decimal)(end - start).TotalHours;
    }

    /// <summary>تبدیل زمان UTC به ساعت تهران برای دسته‌بندی ماه جلالی.</summary>
    private static DateTimeOffset ConvertToTehran(DateTimeOffset value)
    {
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time"); }
        catch (TimeZoneNotFoundException) { zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran"); }
        return TimeZoneInfo.ConvertTime(value, zone);
    }
}
