using FleetManagement.Domain.Missions;

namespace FleetManagement.Application.Missions;

/// <summary>
/// هماهنگ‌کننده سناریوهای ثبت، ویرایش و بررسی درخواست مأموریت.
/// </summary>
public sealed class MissionRequestWorkflowService(
    MissionRequestService requestService,
    MissionPendingRequestService pendingRequestService,
    MissionReviewService reviewService) : IMissionRequestService
{
    public Task<Guid> CreateRequestAsync(
        string title, string requesterUsername, string requesterName, string requesterPhoneSnapshot,
        string requesterOrganizationalUnit, string requestingGroup, MissionType type, string purpose,
        string origin, string destination, IEnumerable<string>? intermediateStops,
        DateTimeOffset plannedStartAtUtc, DateTimeOffset plannedEndAtUtc, string? description,
        int? passengerCount, string? missionOrderNumber, DateOnly? missionOrderDate, string actor,
        CancellationToken cancellationToken = default)
        => requestService.CreateRequestAsync(
            title, requesterUsername, requesterName, requesterPhoneSnapshot,
            requesterOrganizationalUnit, requestingGroup, type, purpose,
            origin, destination, intermediateStops, plannedStartAtUtc, plannedEndAtUtc,
            description, passengerCount, missionOrderNumber, missionOrderDate, actor, cancellationToken);

    public Task UpdatePendingRequestAsync(
        Guid missionId, string requesterUsername, string title, string requesterPhoneSnapshot,
        string requestingGroup, MissionType type, string purpose, string origin, string destination,
        IEnumerable<string>? intermediateStops, DateTimeOffset plannedStartAtUtc,
        DateTimeOffset plannedEndAtUtc, string? description, int? passengerCount,
        string? missionOrderNumber, DateOnly? missionOrderDate,
        CancellationToken cancellationToken = default)
        => pendingRequestService.UpdatePendingRequestAsync(
            missionId, requesterUsername, title, requesterPhoneSnapshot, requestingGroup, type,
            purpose, origin, destination, intermediateStops, plannedStartAtUtc, plannedEndAtUtc,
            description, passengerCount, missionOrderNumber, missionOrderDate, cancellationToken);

    public Task StartReviewAsync(Guid missionId, string actor, CancellationToken cancellationToken = default)
        => reviewService.StartReviewAsync(missionId, actor, cancellationToken);

    public Task ApproveAsync(
        Guid missionId, Guid driverId, Guid vehicleId, string actor,
        CancellationToken cancellationToken = default)
        => reviewService.ApproveAsync(missionId, driverId, vehicleId, actor, cancellationToken);

    public Task RejectAsync(
        Guid missionId, string reason, string actor, CancellationToken cancellationToken = default)
        => reviewService.RejectAsync(missionId, reason, actor, cancellationToken);
}
