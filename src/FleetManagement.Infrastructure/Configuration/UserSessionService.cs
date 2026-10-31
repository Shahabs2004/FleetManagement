using FleetManagement.Domain.Sessions;
using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// ثبت دائمی تلاش‌های ورود، نشست‌های موفق، فعالیت، خروج و انقضای خودکار.
/// </summary>
public sealed class UserSessionService(IDbContextFactory<FleetDbContext> dbContextFactory)
{
    /// <summary>ثبت یک تلاش ورود موفق و بازگرداندن شناسه نشست.</summary>
    public async Task<Guid> RecordSuccessfulLoginAsync(
        string username,
        UserSessionLoginMethod method,
        string? clientIpAddress,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext =
            await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var session = new UserSession(username, method, true, clientIpAddress);
        dbContext.UserSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);

        return session.Id;
    }

    /// <summary>ثبت تلاش ناموفق بدون ذخیره رمز عبور.</summary>
    public async Task RecordFailedLoginAsync(
        string username,
        UserSessionLoginMethod method,
        string reason,
        string? clientIpAddress,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext =
            await dbContextFactory.CreateDbContextAsync(cancellationToken);

        dbContext.UserSessions.Add(
            new UserSession(username, method, false, clientIpAddress, reason));

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>ثبت خروج دستی نشست.</summary>
    public async Task EndSessionAsync(
        Guid sessionId,
        UserSessionStatus reason,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext =
            await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var session = await dbContext.UserSessions
            .SingleOrDefaultAsync(item => item.Id == sessionId, cancellationToken);

        session?.End(reason, DateTimeOffset.UtcNow);

        if (session is not null)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>به‌روزرسانی آخرین فعالیت نشست.</summary>
    public async Task TouchAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext =
            await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var session = await dbContext.UserSessions
            .SingleOrDefaultAsync(item => item.Id == sessionId, cancellationToken);

        if (session is null || session.Status != UserSessionStatus.Active)
        {
            return;
        }

        session.Touch(DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// بستن نشست‌هایی که از مهلت بیکاری یا عمر مطلق عبور کرده‌اند.
    /// </summary>
    public async Task<int> ExpireStaleSessionsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var dbContext =
            await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var idleCutoff = now.AddMinutes(-30);
        var absoluteCutoff = now.AddHours(-8);

        var staleSessions = await dbContext.UserSessions
            .Where(session => session.Status == UserSessionStatus.Active
                && (session.LastActivityAtUtc <= idleCutoff
                    || session.LoginAtUtc <= absoluteCutoff))
            .ToListAsync(cancellationToken);

        foreach (var session in staleSessions)
        {
            session.ExpireIfNeeded(
                now,
                TimeSpan.FromMinutes(30),
                TimeSpan.FromHours(8));
        }

        if (staleSessions.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return staleSessions.Count;
    }
}
