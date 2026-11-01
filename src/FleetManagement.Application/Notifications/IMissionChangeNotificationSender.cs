namespace FleetManagement.Application.Notifications;

/// <summary>
/// قرارداد ارسال اعلان‌های مرتبط با گردش درخواست تغییر مأموریت.
/// </summary>
public interface IMissionChangeNotificationSender
{
    /// <summary>ثبت و انتشار اعلان برای یک کاربر.</summary>
    Task CreateAsync(
        string recipientUsername,
        string title,
        string message,
        string eventKey,
        string? referenceType = null,
        string? referenceId = null,
        CancellationToken cancellationToken = default);
}
