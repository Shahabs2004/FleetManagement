using FleetManagement.Application.Missions;
using FleetManagement.Domain.Missions;
using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// پیاده‌سازی queryهای خواندنی مأموریت.
/// </summary>
public sealed class MissionQueryService(
    IDbContextFactory<FleetDbContext> dbContextFactory) : IMissionQueryService
{
    public async Task<IReadOnlyList<Mission>> GetTimelineAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        int take = 1000,
        CancellationToken cancellationToken = default)
    {
        if (toUtc <= fromUtc)
        {
            throw new ArgumentException("پایان بازه باید بعد از شروع آن باشد.");
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await db.Missions
            .AsNoTracking()
            .Where(mission => mission.DriverId.HasValue
                && mission.PlannedStartAtUtc < toUtc
                && mission.PlannedEndAtUtc > fromUtc)
            .OrderBy(mission => mission.PlannedStartAtUtc)
            .Take(Math.Clamp(take, 1, 2000))
            .ToListAsync(cancellationToken);
    }

    /// <summary>دریافت یک مأموریت با شناسه برای نمایش جزئیات و چاپ دستور.</summary>
    public async Task<Mission> GetByIdAsync(Guid missionId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await GetMissionAsync(db, missionId, cancellationToken);
    }

    /// <summary>جست‌وجوی مأموریت‌ها؛ درخواست‌کننده فقط سوابق خود را دریافت می‌کند.</summary>
    public async Task<IReadOnlyList<Mission>> SearchAsync(
        string? requesterUsername = null,
        MissionStatus? status = null,
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        int take = 200,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Missions.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(requesterUsername))
        {
            query = query.Where(mission => mission.RequesterUsername == requesterUsername);
        }

        if (status.HasValue)
        {
            query = query.Where(mission => mission.Status == status.Value);
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(mission => mission.PlannedStartAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(mission => mission.PlannedStartAtUtc <= toUtc.Value);
        }

        return await query
            .OrderByDescending(mission => mission.CreatedAtUtc)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(cancellationToken);
    }

    /// <summary>تولید شماره یکتای سالانه بر اساس تقویم جلالی در تراکنش سریال‌پذیر.</summary>
    /// <summary>بررسی هم‌پوشانی یا فاصله ناکافی برای راننده و خودرو.</summary>
    private static async Task<Mission> GetMissionAsync(
        FleetDbContext db,
        Guid missionId,
        CancellationToken cancellationToken)
    {
        return await db.Missions.SingleOrDefaultAsync(
            mission => mission.Id == missionId,
            cancellationToken)
            ?? throw new KeyNotFoundException("مأموریت موردنظر پیدا نشد.");
    }

}
