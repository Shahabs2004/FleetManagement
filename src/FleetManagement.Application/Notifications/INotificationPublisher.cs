namespace FleetManagement.Application.Notifications;

/// <summary>
/// پیام اعلان لحظه‌ای برای ارسال به نشست‌های کاربر.
/// </summary>
public sealed record NotificationPush(
    Guid Id,
    string Title,
    string Message,
    string EventKey,
    string? ReferenceType,
    string? ReferenceId,
    DateTimeOffset CreatedAtUtc);

/// <summary>
/// قرارداد انتشار اعلان در کانال لحظه‌ای؛ پیاده‌سازی آن در لایه وب قرار دارد.
/// </summary>
public interface INotificationPublisher
{
    /// <summary>ارسال اعلان به نشست‌های فعال یک کاربر.</summary>
    Task PublishAsync(string username, NotificationPush notification, CancellationToken cancellationToken = default);
}
