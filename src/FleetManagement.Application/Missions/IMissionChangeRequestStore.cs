using FleetManagement.Domain.Missions;

namespace FleetManagement.Application.Missions;

/// <summary>
/// درگاه پایدارسازی درخواست تغییر؛ پیاده‌سازی زیرساختی، تراکنش Serializable را مدیریت می‌کند.
/// </summary>
public interface IMissionChangeRequestStore
{
    /// <summary>اجرای عملیات و ذخیره تغییرات در یک تراکنش اتمیک.</summary>
    Task<TResult> ExecuteSerializableAsync<TResult>(
        Func<IMissionChangeRequestUnitOfWork, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);

    /// <summary>دریافت فهرست درخواست‌ها با فیلتر اختیاری.</summary>
    Task<IReadOnlyList<MissionChangeRequest>> GetChangeRequestsAsync(
        string? requesterUsername = null,
        MissionChangeRequestStatus? status = null,
        int take = 200,
        CancellationToken cancellationToken = default);
}
