using FleetManagement.Domain.Missions;

namespace FleetManagement.Application.Missions;

/// <summary>عملیات داده‌ای موردنیاز سناریوی تمدید مأموریت.</summary>
public interface IMissionExtensionUnitOfWork
{
    Task<Mission?> GetMissionAsync(Guid missionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Mission>> GetResourceConflictCandidatesAsync(
        Guid excludedMissionId,
        Guid driverId,
        Guid vehicleId,
        CancellationToken cancellationToken = default);
}
