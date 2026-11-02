using System.Data;
using FleetManagement.Application.Missions;
using FleetManagement.Domain.Missions;
using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>پیاده‌سازی EF Core برای خواندن سررسیدها و تراکنش پردازش چرخه عمر.</summary>
public sealed class EfMissionLifecycleProcessorStore(IDbContextFactory<FleetDbContext> dbContextFactory)
    : IMissionLifecycleProcessorStore
{
    public async Task<TResult> ExecuteSerializableAsync<TResult>(
        Func<IMissionLifecycleProcessorUnitOfWork, CancellationToken, Task<TResult>> operation,
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

    private sealed class EfUnitOfWork(FleetDbContext db) : IMissionLifecycleProcessorUnitOfWork
    {
        public async Task<IReadOnlyList<Mission>> GetDueMissionsAsync(
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken = default) =>
            await db.Missions
                .Where(mission =>
                    (mission.Status == MissionStatus.Confirmed && mission.PlannedStartAtUtc <= nowUtc)
                    || ((mission.Status == MissionStatus.Running || mission.Status == MissionStatus.WithdrawalPending)
                        && mission.PlannedEndAtUtc <= nowUtc))
                .ToListAsync(cancellationToken);
    }
}
