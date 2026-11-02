namespace FleetManagement.Application.Missions;

/// <summary>مرز تراکنشی ذخیره‌سازی برای تمدید مأموریت.</summary>
public interface IMissionExtensionStore
{
    /// <summary>اجرای تمدید در یک تراکنش Serializable.</summary>
    Task<TResult> ExecuteSerializableAsync<TResult>(
        Func<IMissionExtensionUnitOfWork, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}
