using System.Data;
using FleetManagement.Application.Missions;
using FleetManagement.Domain.Locations;
using FleetManagement.Domain.Missions;
using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// ذخیره‌ساز EF برای ویرایش درخواست Pending با کنترل هم‌زمانی تراکنشی.
/// </summary>
public sealed class EfMissionPendingRequestStore(IDbContextFactory<FleetDbContext> dbContextFactory)
    : IMissionPendingRequestStore
{
    public async Task<TResult> ExecuteSerializableAsync<TResult>(
        Func<IMissionPendingRequestUnitOfWork, CancellationToken, Task<TResult>> operation,
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

    private sealed class EfUnitOfWork(FleetDbContext db) : IMissionPendingRequestUnitOfWork
    {
        public Task<Mission?> GetMissionAsync(
            Guid missionId,
            CancellationToken cancellationToken = default) =>
            db.Missions.SingleOrDefaultAsync(mission => mission.Id == missionId, cancellationToken);

        public Task<string[]> GetApprovedLocationNamesAsync(
            string[] requestedLocations,
            CancellationToken cancellationToken = default) =>
            db.Locations
                .Where(location =>
                    location.Status == LocationStatus.Approved
                    && requestedLocations.Contains(location.Name))
                .Select(location => location.Name)
                .ToArrayAsync(cancellationToken);
    }
}
