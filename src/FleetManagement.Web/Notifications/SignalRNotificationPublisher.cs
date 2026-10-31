using FleetManagement.Application.Notifications;
using Microsoft.AspNetCore.SignalR;

namespace FleetManagement.Web.Notifications;

/// <summary>
/// انتشار اعلان داخلی به اتصال‌های زنده کاربر از طریق SignalR.
/// </summary>
public sealed class SignalRNotificationPublisher(
    IHubContext<NotificationHub> hubContext,
    NotificationEventBus eventBus) : INotificationPublisher
{
    /// <summary>ارسال پیام به گروه خصوصی کاربر.</summary>
    public Task PublishAsync(
        string username,
        NotificationPush notification,
        CancellationToken cancellationToken = default)
    {
        eventBus.Publish(username, notification);

        return hubContext.Clients
            .Group(NotificationHub.GetUserGroup(username))
            .SendAsync("ReceiveNotification", notification, cancellationToken);
    }
}
