using FleetManagement.Domain.Missions;

namespace FleetManagement.Application.Missions;

/// <summary>
/// قرارداد کاربردی تقسیم مأموریت در حال اجرا.
/// </summary>
public interface IMissionSplitService
{
    /// <summary>تقسیم مأموریت و ایجاد بخش دوم با منابع و شماره مستقل.</summary>
    Task<Guid> SplitAsync(
        Guid missionId,
        Guid newDriverId,
        Guid newVehicleId,
        DateTimeOffset splitAtUtc,
        long firstSegmentOdometerEndKm,
        long? secondVehicleOdometerStartKm,
        MissionSplitReason splitReason,
        string? explanation,
        string actor,
        CancellationToken cancellationToken = default);
}
