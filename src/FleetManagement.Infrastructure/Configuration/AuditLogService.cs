using FleetManagement.Domain.Auditing;
using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// ثبت و جست‌وجوی رویدادهای ممیزی؛ این سرویس هیچ API ویرایش یا حذف ارائه نمی‌کند.
/// </summary>
public sealed class AuditLogService(IDbContextFactory<FleetDbContext> dbContextFactory)
{
    /// <summary>
    /// ثبت یک رویداد ممیزی؛ فراخواننده باید داده‌های محرمانه را از تصویرها حذف کند.
    /// </summary>
    public async Task<Guid> RecordAsync(
        string actorUsername,
        string action,
        string entityType,
        string? entityId,
        string outcome,
        string? reason = null,
        string? beforeJson = null,
        string? afterJson = null,
        string? clientIpAddress = null,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entry = new AuditEntry(
            actorUsername,
            action,
            entityType,
            entityId,
            outcome,
            reason,
            beforeJson,
            afterJson,
            clientIpAddress);

        dbContext.AuditEntries.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entry.Id;
    }

    /// <summary>
    /// جست‌وجوی محدودشده سوابق ممیزی بدون امکان تغییر یا حذف.
    /// </summary>
    public async Task<IReadOnlyList<AuditEntry>> SearchAsync(
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        string? actorUsername = null,
        string? entityType = null,
        string? action = null,
        string? outcome = null,
        int take = 200,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = dbContext.AuditEntries.AsNoTracking();

        if (fromUtc.HasValue)
        {
            query = query.Where(entry => entry.OccurredAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(entry => entry.OccurredAtUtc <= toUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(actorUsername))
        {
            var value = actorUsername.Trim();
            query = query.Where(entry => entry.ActorUsername.Contains(value));
        }

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            var value = entityType.Trim();
            query = query.Where(entry => entry.EntityType.Contains(value));
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            var value = action.Trim();
            query = query.Where(entry => entry.Action.Contains(value));
        }

        if (!string.IsNullOrWhiteSpace(outcome))
        {
            var value = outcome.Trim();
            query = query.Where(entry => entry.Outcome == value);
        }

        return await query
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(cancellationToken);
    }
}
