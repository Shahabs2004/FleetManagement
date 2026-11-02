using FleetManagement.Domain.Missions;

namespace FleetManagement.Application.Missions;

/// <summary>
/// درگاه اجرای عملیات بررسی درخواست اولیه مأموریت در تراکنش پایدار.
/// </summary>
public interface IMissionReviewStore
{
    /// <summary>اجرای عملیات بررسی در تراکنش Serializable.</summary>
    Task<TResult> ExecuteSerializableAsync<TResult>(
        Func<IMissionReviewUnitOfWork, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}
