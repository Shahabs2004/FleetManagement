using FleetManagement.Domain.Locations;
using FleetManagement.Domain.Missions;

namespace FleetManagement.Application.Missions;

/// <summary>
/// عملیات داده‌ای موردنیاز ویرایش درخواست Pending در یک تراکنش.
/// </summary>
public interface IMissionPendingRequestUnitOfWork
{
    Task<Mission?> GetMissionAsync(Guid missionId, CancellationToken cancellationToken = default);

    Task<string[]> GetApprovedLocationNamesAsync(
        string[] requestedLocations,
        CancellationToken cancellationToken = default);
}
