using System.Data;
using System.Globalization;
using FleetManagement.Application.Missions;
using FleetManagement.Domain.Drivers;
using FleetManagement.Domain.Missions;
using FleetManagement.Domain.Vehicles;
using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>پیاده‌سازی EF Core برای تراکنش و پرس‌وجوهای تقسیم مأموریت.</summary>
public sealed class EfMissionSplitStore(IDbContextFactory<FleetDbContext> dbContextFactory)
    : IMissionSplitStore
{
    public async Task<TResult> ExecuteSerializableAsync<TResult>(
        Func<IMissionSplitUnitOfWork, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var result = await operation(new EfUnitOfWork(db), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private sealed class EfUnitOfWork(FleetDbContext db) : IMissionSplitUnitOfWork
    {
        public Task<Mission?> GetMissionAsync(
            Guid missionId,
            CancellationToken cancellationToken = default) =>
            db.Missions.SingleOrDefaultAsync(
                mission => mission.Id == missionId,
                cancellationToken);

        public Task<Driver?> GetDriverAsync(
            Guid driverId,
            CancellationToken cancellationToken = default) =>
            db.Drivers.SingleOrDefaultAsync(
                driver => driver.Id == driverId,
                cancellationToken);

        public Task<Vehicle?> GetVehicleAsync(
            Guid vehicleId,
            CancellationToken cancellationToken = default) =>
            db.Vehicles.SingleOrDefaultAsync(
                vehicle => vehicle.Id == vehicleId,
                cancellationToken);

        public Task<long?> GetLastOdometerEndAsync(
            Guid vehicleId,
            CancellationToken cancellationToken = default) =>
            db.Missions
                .Where(mission => mission.VehicleId == vehicleId && mission.OdometerEndKm.HasValue)
                .OrderByDescending(mission => mission.ActualEndAtUtc ?? mission.PlannedEndAtUtc)
                .Select(mission => mission.OdometerEndKm)
                .FirstOrDefaultAsync(cancellationToken);

        public async Task<string> GenerateMissionNumberAsync(
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken = default)
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

            // قفل انحصاری SQL به تفکیک سال شمسی، تولید شماره را در تراکنش سریالی می‌کند.
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

        public async Task<IReadOnlyList<Mission>> GetResourceConflictCandidatesAsync(
            Guid excludedMissionId,
            Guid driverId,
            Guid vehicleId,
            CancellationToken cancellationToken = default) =>
            await db.Missions
                .Where(mission => mission.Id != excludedMissionId
                    && (mission.DriverId == driverId || mission.VehicleId == vehicleId))
                .ToListAsync(cancellationToken);

        public void Add(Mission mission) => db.Missions.Add(mission);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            db.SaveChangesAsync(cancellationToken);
    }
}
