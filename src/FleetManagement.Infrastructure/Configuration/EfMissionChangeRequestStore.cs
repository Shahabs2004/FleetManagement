using System.Data;
using FleetManagement.Application.Missions;
using FleetManagement.Domain.Missions;
using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// پیاده‌سازی پایدارسازی درخواست تغییر با تراکنش Serializable در SQL Server.
/// </summary>
public sealed class EfMissionChangeRequestStore(IDbContextFactory<FleetDbContext> dbContextFactory)
    : IMissionChangeRequestStore
{
    /// <inheritdoc />
    public async Task<TResult> ExecuteSerializableAsync<TResult>(
        Func<IMissionChangeRequestUnitOfWork, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var unitOfWork = new EfUnitOfWork(db);
        var result = await operation(unitOfWork, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return result;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MissionChangeRequest>> GetChangeRequestsAsync(
        string? requesterUsername = null,
        MissionChangeRequestStatus? status = null,
        int take = 200,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.MissionChangeRequests.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(requesterUsername))
        {
            query = query.Where(item => item.RequesterUsername == requesterUsername);
        }

        if (status.HasValue)
        {
            query = query.Where(item => item.Status == status.Value);
        }

        return await query
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(cancellationToken);
    }

    private sealed class EfUnitOfWork(FleetDbContext db) : IMissionChangeRequestUnitOfWork
    {
        public Task<Mission?> GetMissionAsync(Guid missionId, CancellationToken cancellationToken = default) =>
            db.Missions.SingleOrDefaultAsync(item => item.Id == missionId, cancellationToken);

        public Task<MissionChangeRequest?> GetChangeRequestAsync(
            Guid changeRequestId,
            CancellationToken cancellationToken = default) =>
            db.MissionChangeRequests.SingleOrDefaultAsync(item => item.Id == changeRequestId, cancellationToken);

        public Task<bool> HasOpenRequestAsync(Guid missionId, CancellationToken cancellationToken = default) =>
            db.MissionChangeRequests.AnyAsync(
                item => item.MissionId == missionId
                    && (item.Status == MissionChangeRequestStatus.Pending
                        || item.Status == MissionChangeRequestStatus.UnderReview),
                cancellationToken);

        public void Add(MissionChangeRequest request) => db.MissionChangeRequests.Add(request);
    }
}
