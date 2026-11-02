namespace FleetManagement.Application.Missions;

/// <summary>مرز تراکنشی ذخیره‌سازی برای ثبت کیلومترشمار مأموریت.</summary>
public interface IMissionOdometerStore
{
    Task<TResult> ExecuteSerializableAsync<TResult>(
        Func<IMissionOdometerUnitOfWork, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}
