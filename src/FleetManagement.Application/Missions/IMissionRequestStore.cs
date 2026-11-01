using FleetManagement.Domain.Missions;

namespace FleetManagement.Application.Missions;

/// <summary>
/// درگاه ثبت درخواست مأموریت در تراکنش پایدار.
/// </summary>
public interface IMissionRequestStore
{
    /// <summary>اجرای عملیات ثبت در تراکنش Serializable و Commit خودکار در صورت موفقیت.</summary>
    Task<TResult> ExecuteSerializableAsync<TResult>(
        Func<IMissionRequestUnitOfWork, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}
