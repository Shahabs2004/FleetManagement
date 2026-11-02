namespace FleetManagement.Application.Missions;

/// <summary>
/// مرز تراکنشی ذخیره‌سازی برای ویرایش درخواست Pending.
/// </summary>
public interface IMissionPendingRequestStore
{
    Task<TResult> ExecuteSerializableAsync<TResult>(
        Func<IMissionPendingRequestUnitOfWork, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}
