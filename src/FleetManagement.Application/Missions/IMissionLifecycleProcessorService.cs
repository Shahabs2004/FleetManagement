namespace FleetManagement.Application.Missions;

/// <summary>
/// قرارداد کاربردی پردازش زمان‌بندی‌شده چرخه عمر مأموریت.
/// </summary>
public interface IMissionLifecycleProcessorService
{
    /// <summary>شروع، تکمیل و بستن خودکار مأموریت‌های سررسیدشده.</summary>
    Task<int> ProcessDueMissionsAsync(CancellationToken cancellationToken = default);
}
