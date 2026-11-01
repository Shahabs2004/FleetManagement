using FleetManagement.Domain.Drivers;
using FleetManagement.Domain.Missions;
using FleetManagement.Domain.Vehicles;
using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FleetManagement.Infrastructure.Reporting;

/// <summary>
/// محاسبه آمار نقش‌محور داشبورد با پرس‌وجوهای تجمیعی SQL.
/// </summary>
public sealed class FleetDashboardService(IDbContextFactory<FleetDbContext> dbContextFactory)
{
    /// <summary>
    /// ساخت خلاصه مأموریت‌های کاربر یا کل ناوگان بر اساس سطح دسترسی.
    /// </summary>
    public async Task<FleetDashboardSummary> BuildAsync(
        string? requesterUsername,
        bool includeFleetAssets,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var missions = db.Missions.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(requesterUsername))
        {
            missions = missions.Where(mission => mission.RequesterUsername == requesterUsername);
        }

        var pending = await missions.CountAsync(
            mission => mission.Status == MissionStatus.Pending
                || mission.Status == MissionStatus.UnderReview,
            cancellationToken);

        var confirmed = await missions.CountAsync(
            mission => mission.Status == MissionStatus.Confirmed,
            cancellationToken);

        var running = await missions.CountAsync(
            mission => mission.Status == MissionStatus.Running
                || mission.Status == MissionStatus.WithdrawalPending,
            cancellationToken);

        var completed = await missions.CountAsync(
            mission => mission.Status == MissionStatus.Completed,
            cancellationToken);

        var cancelled = await missions.CountAsync(
            mission => mission.Status == MissionStatus.Cancelled,
            cancellationToken);

        var timeZone = GetTehranTimeZone();
        var localNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone);
        var localMidnight = DateTime.SpecifyKind(localNow.Date, DateTimeKind.Unspecified);
        var nextMidnight = localMidnight.AddDays(1);
        var todayStartUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localMidnight, timeZone));
        var tomorrowStartUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(nextMidnight, timeZone));

        var today = await missions.CountAsync(
            mission => mission.PlannedStartAtUtc >= todayStartUtc
                && mission.PlannedStartAtUtc < tomorrowStartUtc,
            cancellationToken);

        if (!includeFleetAssets)
        {
            return new FleetDashboardSummary(
                pending,
                confirmed,
                running,
                completed,
                cancelled,
                today,
                null,
                null,
                null,
                null);
        }

        var activeDrivers = await db.Drivers.CountAsync(
            driver => !driver.IsArchived
                && driver.EmploymentStatus == DriverEmploymentStatus.Active,
            cancellationToken);

        var totalDrivers = await db.Drivers.CountAsync(
            driver => !driver.IsArchived,
            cancellationToken);

        var activeMissionVehicleIds = db.Missions
            .Where(mission => mission.Status == MissionStatus.Running
                || mission.Status == MissionStatus.WithdrawalPending)
            .Select(mission => mission.VehicleId);

        var readyVehicles = await db.Vehicles.CountAsync(
            vehicle => !vehicle.IsArchived
                && vehicle.ManualStatus == ManualVehicleStatus.Ready
                && !activeMissionVehicleIds.Contains(vehicle.Id),
            cancellationToken);

        var totalVehicles = await db.Vehicles.CountAsync(
            vehicle => !vehicle.IsArchived,
            cancellationToken);

        return new FleetDashboardSummary(
            pending,
            confirmed,
            running,
            completed,
            cancelled,
            today,
            activeDrivers,
            totalDrivers,
            readyVehicles,
            totalVehicles);
    }

    /// <summary>یافتن منطقه زمانی تهران در Windows یا Linux.</summary>
    private static TimeZoneInfo GetTehranTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");
        }
    }
}
