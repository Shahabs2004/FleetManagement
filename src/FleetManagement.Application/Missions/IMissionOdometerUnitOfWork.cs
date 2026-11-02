using FleetManagement.Domain.Missions;

namespace FleetManagement.Application.Missions;

/// <summary>
/// عملیات داده‌ای لازم برای کنترل پیوستگی کیلومتر خودرو.
/// </summary>
public interface IMissionOdometerUnitOfWork
{
    /// <summary>دریافت مأموریت برای ثبت کیلومترشمار.</summary>
    Task<Mission?> GetMissionAsync(Guid missionId, CancellationToken cancellationToken = default);

    /// <summary>آخرین کیلومتر پایان مأموریت‌های قبلی همین خودرو.</summary>
    Task<long?> GetPreviousEndOdometerAsync(
        Guid missionId,
        Guid vehicleId,
        DateTimeOffset beforeOrAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>اولین کیلومتر شروع مأموریت‌های بعدی همین خودرو.</summary>
    Task<long?> GetNextStartOdometerAsync(
        Guid missionId,
        Guid vehicleId,
        DateTimeOffset afterOrAtUtc,
        CancellationToken cancellationToken = default);
}
