namespace FleetManagement.Application.Missions;

/// <summary>
/// درگاه ذخیره‌سازی سناریوی ثبت کیلومترشمار با تضمین اجرای تراکنشی.
/// </summary>
public interface IMissionOdometerStore
{
    /// <summary>اجرای عملیات کیلومترشمار در تراکنش Serializable.</summary>
    Task<TResult> ExecuteSerializableAsync<TResult>(
        Func<IMissionOdometerUnitOfWork, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}
