using FleetManagement.Domain.Missions;
using FleetManagement.Domain.Notifications;

namespace FleetManagement.Application.Notifications;

/// <summary>
/// قرارداد زمان‌بندی پیامک‌های مرتبط با تغییر مأموریت؛ خطای صف نباید عملیات اصلی را برگرداند.
/// </summary>
public interface IMissionChangeSmsScheduler
{
    /// <summary>صف‌بندی امن پیامک رویداد مأموریت.</summary>
    Task QueueSafelyAsync(
        Mission mission,
        SmsEventType eventType,
        string? reason,
        CancellationToken cancellationToken = default);

    /// <summary>لغو امن یادآوری‌های ارسال‌نشده پس از تغییر زمان شروع.</summary>
    Task CancelUnsentRemindersSafelyAsync(
        Guid missionId,
        string reason,
        string actor,
        CancellationToken cancellationToken = default);
}
