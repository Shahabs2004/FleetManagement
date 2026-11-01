using FleetManagement.Application.Configuration;
using FleetManagement.Domain.Drivers;
using FleetManagement.Domain.Missions;
using FleetManagement.Domain.Vehicles;
using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FleetManagement.Infrastructure.Reporting;

/// <summary>
/// محاسبه ظرفیت عمومی ساعتی بدون افشای هویت راننده و خودرو.
/// </summary>
public sealed class MissionAvailabilityService(
    IDbContextFactory<FleetDbContext> dbContextFactory,
    ISystemSettingsStore settingsStore)
{
    private const string MissionRulesKey = "Missions.Rules";

    /// <summary>
    /// دریافت ظرفیت یک هفته از ابتدای شنبه جاری؛ درخواست‌های در انتظار بررسی منابع را رزرو نمی‌کنند.
    /// </summary>
    public async Task<IReadOnlyList<PublicAvailabilitySlot>> GetCurrentWeekAsync(
        CancellationToken cancellationToken = default)
    {
        var zone = GetTehranTimeZone();
        var localNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone);
        var daysSinceSaturday = ((int)localNow.DayOfWeek + 1) % 7;
        var saturday = DateTime.SpecifyKind(localNow.Date.AddDays(-daysSinceSaturday), DateTimeKind.Unspecified);
        var weekStartUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(saturday, zone));
        var weekEndUtc = weekStartUtc.AddDays(7);
        var nowUtc = DateTimeOffset.UtcNow;

        var rules = await settingsStore.GetAsync<MissionRulesConfiguration>(
            MissionRulesKey,
            cancellationToken) ?? MissionRulesConfiguration.CreateDefault();

        var gap = TimeSpan.FromHours(Math.Clamp(rules.MinimumGapHours, 0, 48));
        var horizonEndUtc = nowUtc.AddDays(Math.Clamp(rules.BookingHorizonDays, 1, 365));

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var drivers = await db.Drivers
            .AsNoTracking()
            .Where(driver => !driver.IsArchived
                && driver.EmploymentStatus == DriverEmploymentStatus.Active)
            .Select(driver => driver.Id)
            .ToListAsync(cancellationToken);

        var vehicles = await db.Vehicles
            .AsNoTracking()
            .Where(vehicle => !vehicle.IsArchived
                && vehicle.ManualStatus == ManualVehicleStatus.Ready)
            .Select(vehicle => vehicle.Id)
            .ToListAsync(cancellationToken);

        var blockingStatuses = new[]
        {
            MissionStatus.Confirmed,
            MissionStatus.Running,
            MissionStatus.WithdrawalPending
        };

        var reservations = await db.Missions
            .AsNoTracking()
            .Where(mission => blockingStatuses.Contains(mission.Status)
                && mission.PlannedStartAtUtc < weekEndUtc.Add(gap)
                && mission.PlannedEndAtUtc > weekStartUtc.Subtract(gap))
            .Select(mission => new Reservation(
                mission.DriverId,
                mission.VehicleId,
                mission.PlannedStartAtUtc,
                mission.PlannedEndAtUtc))
            .ToListAsync(cancellationToken);

        var slots = new List<PublicAvailabilitySlot>(168);
        for (var index = 0; index < 168; index++)
        {
            var slotStart = weekStartUtc.AddHours(index);
            var slotEnd = slotStart.AddHours(1);
            var available = slotStart >= nowUtc
                && slotStart < horizonEndUtc
                && drivers.Count > 0
                && vehicles.Count > 0;

            if (available)
            {
                var protectedStart = slotStart.Subtract(gap);
                var protectedEnd = slotEnd.Add(gap);

                var freeDriverExists = drivers.Any(driverId =>
                    reservations.All(reservation =>
                        reservation.DriverId != driverId
                        || !Overlaps(reservation, protectedStart, protectedEnd)));

                var freeVehicleExists = vehicles.Any(vehicleId =>
                    reservations.All(reservation =>
                        reservation.VehicleId != vehicleId
                        || !Overlaps(reservation, protectedStart, protectedEnd)));

                available = freeDriverExists && freeVehicleExists;
            }

            slots.Add(new PublicAvailabilitySlot(slotStart, available));
        }

        return slots;
    }

    /// <summary>بررسی هم‌پوشانی دو بازه زمانی.</summary>
    private static bool Overlaps(Reservation reservation, DateTimeOffset start, DateTimeOffset end) =>
        reservation.StartAtUtc < end && reservation.EndAtUtc > start;

    /// <summary>یافتن منطقه زمانی تهران در Windows یا Linux.</summary>
    private static TimeZoneInfo GetTehranTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran"); }
    }

    private sealed record Reservation(
        Guid? DriverId,
        Guid? VehicleId,
        DateTimeOffset StartAtUtc,
        DateTimeOffset EndAtUtc);
}
