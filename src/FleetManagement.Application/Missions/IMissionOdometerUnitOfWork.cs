using FleetManagement.Domain.Missions;

namespace FleetManagement.Application.Missions;

/// <summary>عملیات داده‌ای موردنیاز اعتبارسنجی پیوستگی کیلومترشمار.</summary>
public interface IMissionOdometerUnitOfWork
{
    Task<Mission?> GetMissionAsync(Guid missionId, CancellationToken cancellationToken = default);

    Task<long?> GetPreviousOdometerEndAsync(
        Guid missionId,
        Guid vehicleId,
        DateTimeOffset missionStartAtUtc,
        CancellationToken cancellationToken = default);

    Task<long?> GetNextOdometerStartAsync(
        Guid missionId,
        Guid vehicleId,
        DateTimeOffset effectiveEndAtUtc,
        CancellationToken cancellationToken = default);
}
