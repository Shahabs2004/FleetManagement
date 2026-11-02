using FleetManagement.Domain.Missions;
using FleetManagement.Domain.Notifications;

namespace FleetManagement.Application.Notifications;

/// <summary>
/// قرارداد صف‌بندی امن پیامک‌های چرخه عمر مأموریت.
/// </summary>
public interface IMissionSmsScheduler
{
    /// <summary>صف‌بندی پیامک؛ خطای سرویس پیامک نباید عملیات اصلی را برگرداند.</summary>
    Task QueueSafelyAsync(
        Mission mission,
        SmsEventType eventType,
        string? reason,
        CancellationToken cancellationToken = default);
    /// <summary>لغو امن پیامک‌های یادآوری زمان شروع که هنوز ارسال نشده‌اند.</summary>
    Task CancelUnsentRemindersSafelyAsync(
        Guid missionId,
        string reason,
        string actor,
        CancellationToken cancellationToken = default);
}
