using FleetManagement.Application.Notifications;

namespace FleetManagement.Web.Notifications;

/// <summary>
/// رویداد درون‌فرایندی برای به‌روزرسانی فوری پنل اعلان در نشست‌های Blazor.
/// </summary>
public sealed class NotificationEventBus
{
    /// <summary>رویداد انتشار اعلان برای یک نام کاربری.</summary>
    public event Action<string, NotificationPush>? Published;

    /// <summary>انتشار اعلان به شنونده‌های محلی.</summary>
    public void Publish(string username, NotificationPush notification)
        => Published?.Invoke(username, notification);
}
