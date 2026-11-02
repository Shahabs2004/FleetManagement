using FleetManagement.Domain.Drivers;
using FleetManagement.Domain.Missions;
using FleetManagement.Domain.Vehicles;

namespace FleetManagement.Application.Missions;

/// <summary>
/// عملیات داده‌ای موردنیاز برای بررسی و تخصیص درخواست مأموریت.
/// </summary>
public interface IMissionReviewUnitOfWork
{
    /// <summary>دریافت درخواست مأموریت برای تغییر وضعیت.</summary>
    Task<Mission?> GetMissionAsync(Guid missionId, CancellationToken cancellationToken = default);

    /// <summary>دریافت راننده برای بررسی قابلیت تخصیص.</summary>
    Task<Driver?> GetDriverAsync(Guid driverId, CancellationToken cancellationToken = default);

    /// <summary>دریافت خودرو برای بررسی قابلیت تخصیص.</summary>
    Task<Vehicle?> GetVehicleAsync(Guid vehicleId, CancellationToken cancellationToken = default);

    /// <summary>بررسی تعارض زمانی راننده یا خودرو با مأموریت‌های تأییدشده و در حال اجرا.</summary>
    Task<bool> HasResourceConflictAsync(
        Mission current,
        Guid driverId,
        Guid vehicleId,
        TimeSpan minimumGap,
        CancellationToken cancellationToken = default);
}
