using System.Data;
using FleetManagement.Application.Missions;
using FleetManagement.Domain.Drivers;
using FleetManagement.Domain.Missions;
using FleetManagement.Domain.Vehicles;
using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// پیاده‌سازی داده‌ای گردش بررسی درخواست مأموریت با تراکنش Serializable.
/// </summary>
public sealed class EfMissionReviewStore(IDbContextFactory<FleetDbContext> dbContextFactory)
    : IMissionReviewStore
{
    /// <inheritdoc />
    public async Task<TResult> ExecuteSerializableAsync<TResult>(
        Func<IMissionReviewUnitOfWork, CancellationToken, Task<TResult>> operation,
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

    private sealed class EfUnitOfWork(FleetDbContext db) : IMissionReviewUnitOfWork
    {
        public Task<Mission?> GetMissionAsync(
            Guid missionId,
            CancellationToken cancellationToken = default) =>
            db.Missions.SingleOrDefaultAsync(item => item.Id == missionId, cancellationToken);

        public Task<Driver?> GetDriverAsync(
            Guid driverId,
            CancellationToken cancellationToken = default) =>
            db.Drivers.SingleOrDefaultAsync(item => item.Id == driverId, cancellationToken);

        public Task<Vehicle?> GetVehicleAsync(
            Guid vehicleId,
            CancellationToken cancellationToken = default) =>
            db.Vehicles.SingleOrDefaultAsync(item => item.Id == vehicleId, cancellationToken);

        public async Task<bool> HasResourceConflictAsync(
            Mission current,
            Guid driverId,
            Guid vehicleId,
            TimeSpan minimumGap,
            CancellationToken cancellationToken = default)
        {
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

            // محاسبه فاصله در حافظه انجام می‌شود تا ترجمه DateTimeOffset.Add(TimeSpan) به SQL وابسته نباشد.
            return candidates.Any(existing =>
                current.PlannedStartAtUtc < existing.PlannedEndAtUtc.Add(minimumGap)
                && current.PlannedEndAtUtc.Add(minimumGap) > existing.PlannedStartAtUtc);
        }
    }
}
