using FleetManagement.Application.Notifications;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// اتصال اعلان‌های Application به سرویس اعلان پایدار و انتشار لحظه‌ای زیرساخت.
/// </summary>
public sealed class MissionChangeNotificationSender(NotificationService notificationService)
    : IMissionChangeNotificationSender
{
    /// <inheritdoc />
    public Task CreateAsync(
        string recipientUsername,
        string title,
        string message,
        string eventKey,
        string? referenceType = null,
        string? referenceId = null,
        CancellationToken cancellationToken = default) =>
        notificationService.CreateAsync(
            recipientUsername,
            title,
            message,
            eventKey,
            referenceType,
            referenceId,
            cancellationToken);
}
