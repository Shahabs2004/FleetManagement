using System.Data;
using FleetManagement.Application.Missions;
using FleetManagement.Domain.Missions;
using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>پیاده‌سازی EF برای تراکنش ثبت و اعتبارسنجی کیلومترشمار.</summary>
public sealed class EfMissionOdometerStore(IDbContextFactory<FleetDbContext> dbContextFactory)
    : IMissionOdometerStore
{
    public async Task<TResult> ExecuteSerializableAsync<TResult>(
        Func<IMissionOdometerUnitOfWork, CancellationToken, Task<TResult>> operation,
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

    private sealed class EfUnitOfWork(FleetDbContext db) : IMissionOdometerUnitOfWork
    {
        public Task<Mission?> GetMissionAsync(
            Guid missionId,
            CancellationToken cancellationToken = default) =>
            db.Missions.SingleOrDefaultAsync(mission => mission.Id == missionId, cancellationToken);

        public Task<long?> GetPreviousOdometerEndAsync(
            Guid missionId,
            Guid vehicleId,
            DateTimeOffset missionStartAtUtc,
            CancellationToken cancellationToken = default) =>
            db.Missions
                .Where(item => item.Id != missionId
                    && item.VehicleId == vehicleId
                    && item.OdometerEndKm.HasValue
                    && (item.ActualEndAtUtc ?? item.PlannedEndAtUtc) <= missionStartAtUtc)
                .OrderByDescending(item => item.ActualEndAtUtc ?? item.PlannedEndAtUtc)
                .Select(item => item.OdometerEndKm)
                .FirstOrDefaultAsync(cancellationToken);

        public Task<long?> GetNextOdometerStartAsync(
            Guid missionId,
            Guid vehicleId,
            DateTimeOffset effectiveEndAtUtc,
            CancellationToken cancellationToken = default) =>
            db.Missions
                .Where(item => item.Id != missionId
                    && item.VehicleId == vehicleId
                    && item.OdometerStartKm.HasValue
                    && item.PlannedStartAtUtc >= effectiveEndAtUtc)
                .OrderBy(item => item.PlannedStartAtUtc)
                .Select(item => item.OdometerStartKm)
                .FirstOrDefaultAsync(cancellationToken);
    }
}
