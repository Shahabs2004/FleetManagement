using FleetManagement.Domain.Drivers;
using FleetManagement.Domain.Missions;
using FleetManagement.Domain.Vehicles;

namespace FleetManagement.Application.Missions;

/// <summary>عملیات داده‌ای موردنیاز سناریوی تقسیم مأموریت.</summary>
public interface IMissionSplitUnitOfWork
{
    Task<Mission?> GetMissionAsync(Guid missionId, CancellationToken cancellationToken = default);
    Task<Driver?> GetDriverAsync(Guid driverId, CancellationToken cancellationToken = default);
    Task<Vehicle?> GetVehicleAsync(Guid vehicleId, CancellationToken cancellationToken = default);
    Task<long?> GetLastOdometerEndAsync(Guid vehicleId, CancellationToken cancellationToken = default);
    Task<string> GenerateMissionNumberAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Mission>> GetResourceConflictCandidatesAsync(
        Guid excludedMissionId,
        Guid driverId,
        Guid vehicleId,
        CancellationToken cancellationToken = default);
    void Add(Mission mission);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
