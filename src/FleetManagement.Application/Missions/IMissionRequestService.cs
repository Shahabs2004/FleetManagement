using FleetManagement.Domain.Missions;

namespace FleetManagement.Application.Missions;

/// <summary>
/// قرارداد کاربردی ثبت، ویرایش، بررسی، تأیید و رد درخواست مأموریت.
/// </summary>
public interface IMissionRequestService
{
    Task<Guid> CreateRequestAsync(
        string title, string requesterUsername, string requesterName, string requesterPhoneSnapshot,
        string requesterOrganizationalUnit, string requestingGroup, MissionType type, string purpose,
        string origin, string destination, IEnumerable<string>? intermediateStops,
        DateTimeOffset plannedStartAtUtc, DateTimeOffset plannedEndAtUtc, string? description,
        int? passengerCount, string? missionOrderNumber, DateOnly? missionOrderDate, string actor,
        CancellationToken cancellationToken = default);

    Task UpdatePendingRequestAsync(
        Guid missionId, string requesterUsername, string title, string requesterPhoneSnapshot,
        string requestingGroup, MissionType type, string purpose, string origin, string destination,
        IEnumerable<string>? intermediateStops, DateTimeOffset plannedStartAtUtc,
        DateTimeOffset plannedEndAtUtc, string? description, int? passengerCount,
        string? missionOrderNumber, DateOnly? missionOrderDate,
        CancellationToken cancellationToken = default);

    Task StartReviewAsync(Guid missionId, string actor, CancellationToken cancellationToken = default);
    Task ApproveAsync(Guid missionId, Guid driverId, Guid vehicleId, string actor, CancellationToken cancellationToken = default);
    Task RejectAsync(Guid missionId, string reason, string actor, CancellationToken cancellationToken = default);
}
