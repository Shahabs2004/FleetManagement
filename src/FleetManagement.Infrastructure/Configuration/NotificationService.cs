using FleetManagement.Application.Notifications;
using FleetManagement.Domain.Notifications;
using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// ثبت، دریافت و خوانده‌شدن اعلان‌های داخلی با انتشار لحظه‌ای.
/// </summary>
public sealed class NotificationService(
    IDbContextFactory<FleetDbContext> dbContextFactory,
    INotificationPublisher publisher,
    ILogger<NotificationService> logger,
    UserNotificationRecipientResolver recipientResolver) : IMissionNotificationSender
{
    /// <summary>ثبت اعلان برای یک کاربر و انتشار آن به نشست‌های فعال.</summary>
    public async Task<Guid> CreateAsync(
        string recipientUsername,
        string title,
        string message,
        string eventKey,
        string? referenceType = null,
        string? referenceId = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var notification = new UserNotification(
            recipientUsername,
            title,
            message,
            eventKey,
            referenceType,
            referenceId);

        db.UserNotifications.Add(notification);
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            await publisher.PublishAsync(
                recipientUsername,
                new NotificationPush(
                    notification.Id,
                    notification.Title,
                    notification.Message,
                    notification.EventKey,
                    notification.ReferenceType,
                    notification.ReferenceId,
                    notification.CreatedAtUtc),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // اعلان در SQL ثبت شده است؛ اختلال انتشار لحظه‌ای نباید عملیات اصلی را ناموفق کند.
            logger.LogWarning(exception, "انتشار لحظه‌ای اعلان برای کاربر {Username} انجام نشد.", recipientUsername);
        }

        return notification.Id;
    }

    /// <summary>ثبت و انتشار اعلان برای تمام کاربران دارای نقش‌های مشخص.</summary>
    public async Task<int> CreateForRolesAsync(
        IEnumerable<string> roleKeys,
        string title,
        string message,
        string eventKey,
        string? referenceType = null,
        string? referenceId = null,
        CancellationToken cancellationToken = default)
    {
        var recipients = await recipientResolver.ResolveAsync(roleKeys, cancellationToken);
        foreach (var username in recipients)
        {
            await CreateAsync(
                username,
                title,
                message,
                eventKey,
                referenceType,
                referenceId,
                cancellationToken);
        }

        return recipients.Count;
    }

    /// <summary>دریافت اعلان‌های اخیر کاربر جاری.</summary>
    public async Task<IReadOnlyList<UserNotification>> GetRecentAsync(
        string username,
        bool unreadOnly = false,
        int take = 100,
        CancellationToken cancellationToken = default,
        int skip = 0)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.UserNotifications.AsNoTracking()
            .Where(notification => notification.RecipientUsername == username);

        if (unreadOnly)
        {
            query = query.Where(notification => notification.ReadAtUtc == null);
        }

        return await query
            .OrderByDescending(notification => notification.CreatedAtUtc)
            .ThenByDescending(notification => notification.Id)
            .Skip(Math.Max(0, skip))
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(cancellationToken);
    }

    /// <summary>تعداد کل اعلان‌های کاربر، با امکان محدودکردن به خوانده‌نشده‌ها.</summary>
    public async Task<int> GetCountAsync(
        string username,
        bool unreadOnly = false,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.UserNotifications.AsNoTracking()
            .Where(notification => notification.RecipientUsername == username);

        if (unreadOnly)
        {
            query = query.Where(notification => notification.ReadAtUtc == null);
        }

        return await query.CountAsync(cancellationToken);
    }

    /// <summary>تعداد اعلان‌های خوانده‌نشده کاربر.</summary>
    public async Task<int> GetUnreadCountAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.UserNotifications.CountAsync(
            notification => notification.RecipientUsername == username && notification.ReadAtUtc == null,
            cancellationToken);
    }

    /// <summary>علامت‌گذاری یک اعلان متعلق به کاربر جاری به‌عنوان خوانده‌شده.</summary>
    public async Task MarkAsReadAsync(
        Guid notificationId,
        string username,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var notification = await db.UserNotifications.SingleOrDefaultAsync(
            item => item.Id == notificationId && item.RecipientUsername == username,
            cancellationToken);

        if (notification is null) return;
        notification.MarkAsRead(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>علامت‌گذاری همه اعلان‌های خوانده‌نشده کاربر جاری.</summary>
    public async Task MarkAllAsReadAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var unread = await db.UserNotifications
            .Where(item => item.RecipientUsername == username && item.ReadAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var notification in unread)
        {
            notification.MarkAsRead(DateTimeOffset.UtcNow);
        }

        if (unread.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
