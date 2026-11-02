using FleetManagement.Domain.Missions;

namespace FleetManagement.Application.Missions;

/// <summary>
/// هماهنگ‌کننده گردش درخواست تغییر مأموریت در لایه Application.
/// </summary>
public sealed class MissionChangeWorkflowService(
    MissionChangeRequestService requestService,
    MissionChangeApprovalService approvalService) : IMissionChangeManagementService
{
    public Task<Guid> RequestChangeAsync(
        Guid missionId,
        string requesterUsername,
        string reason,
        MissionChangeProposal proposal,
        CancellationToken cancellationToken = default)
        => requestService.RequestChangeAsync(missionId, requesterUsername, reason, proposal, cancellationToken);

    public Task<IReadOnlyList<MissionChangeRequest>> GetChangeRequestsAsync(
        string? requesterUsername = null,
        MissionChangeRequestStatus? status = null,
        int take = 200,
        CancellationToken cancellationToken = default)
        => requestService.GetChangeRequestsAsync(requesterUsername, status, take, cancellationToken);

    public Task StartChangeReviewAsync(
        Guid changeRequestId,
        string reviewer,
        CancellationToken cancellationToken = default)
        => requestService.StartChangeReviewAsync(changeRequestId, reviewer, cancellationToken);

    public Task ApproveChangeAsync(
        Guid changeRequestId,
        string reviewer,
        CancellationToken cancellationToken = default)
        => approvalService.ApproveChangeAsync(changeRequestId, reviewer, cancellationToken);

    public Task RejectChangeAsync(
        Guid changeRequestId,
        string reviewer,
        string reason,
        CancellationToken cancellationToken = default)
        => requestService.RejectChangeAsync(changeRequestId, reviewer, reason, cancellationToken);
}
