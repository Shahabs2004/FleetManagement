namespace FleetManagement.Application.Missions;

/// <summary>مرز تراکنشی ذخیره‌سازی برای پردازش سررسیدهای چرخه عمر مأموریت.</summary>
public interface IMissionLifecycleProcessorStore
{
    /// <summary>اجرای پردازش دسته‌ای در یک تراکنش Serializable.</summary>
    Task<TResult> ExecuteSerializableAsync<TResult>(
        Func<IMissionLifecycleProcessorUnitOfWork, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}
