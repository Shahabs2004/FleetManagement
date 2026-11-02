namespace FleetManagement.Application.Missions;

/// <summary>مرز تراکنشی ذخیره‌سازی برای تقسیم مأموریت.</summary>
public interface IMissionSplitStore
{
    /// <summary>اجرای سناریوی تقسیم در یک تراکنش Serializable.</summary>
    Task<TResult> ExecuteSerializableAsync<TResult>(
        Func<IMissionSplitUnitOfWork, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}
