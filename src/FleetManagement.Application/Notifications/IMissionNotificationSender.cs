namespace FleetManagement.Application.Notifications;

/// <summary>
/// قرارداد ثبت اعلان‌های چرخه عمر مأموریت برای کاربران و نقش‌ها.
/// </summary>
public interface IMissionNotificationSender
{
    /// <summary>ارسال اعلان به یک کاربر.</summary>
    Task CreateAsync(
        string recipientUsername,
        string title,
        string message,
        string eventKey,
        string? referenceType = null,
        string? referenceId = null,
        CancellationToken cancellationToken = default);

    /// <summary>ارسال اعلان به کاربران دارای نقش‌های مشخص.</summary>
    Task<int> CreateForRolesAsync(
        IEnumerable<string> roleKeys,
        string title,
        string message,
        string eventKey,
        string? referenceType = null,
        string? referenceId = null,
        CancellationToken cancellationToken = default);
}
