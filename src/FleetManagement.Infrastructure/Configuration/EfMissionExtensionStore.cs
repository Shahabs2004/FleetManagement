using System.Data;
using FleetManagement.Application.Missions;
using FleetManagement.Domain.Missions;
using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>پیاده‌سازی EF Core برای تراکنش و پرس‌وجوهای تمدید مأموریت.</summary>
public sealed class EfMissionExtensionStore(IDbContextFactory<FleetDbContext> dbContextFactory)
    : IMissionExtensionStore
{
    public async Task<TResult> ExecuteSerializableAsync<TResult>(
        Func<IMissionExtensionUnitOfWork, CancellationToken, Task<TResult>> operation,
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

    private sealed class EfUnitOfWork(FleetDbContext db) : IMissionExtensionUnitOfWork
    {
        public Task<Mission?> GetMissionAsync(
            Guid missionId,
            CancellationToken cancellationToken = default) =>
            db.Missions.SingleOrDefaultAsync(
                mission => mission.Id == missionId,
                cancellationToken);

        public async Task<IReadOnlyList<Mission>> GetResourceConflictCandidatesAsync(
            Guid excludedMissionId,
            Guid driverId,
            Guid vehicleId,
            CancellationToken cancellationToken = default) =>
            await db.Missions
                .Where(mission => mission.Id != excludedMissionId
                    && (mission.DriverId == driverId || mission.VehicleId == vehicleId))
                .ToListAsync(cancellationToken);
    }
}
