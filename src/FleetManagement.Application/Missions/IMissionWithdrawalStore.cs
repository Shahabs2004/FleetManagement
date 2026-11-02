namespace FleetManagement.Application.Missions;

/// <summary>مرز تراکنشی ذخیره‌سازی برای گردش انصراف و لغو مأموریت.</summary>
public interface IMissionWithdrawalStore
{
    /// <summary>اجرای عملیات مأموریت در یک تراکنش Serializable.</summary>
    Task<TResult> ExecuteSerializableAsync<TResult>(
        Func<IMissionWithdrawalUnitOfWork, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}
