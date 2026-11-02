namespace FleetManagement.Application.Missions;

/// <summary>
/// قرارداد کاربردی لغو، انصراف و خاتمه زودهنگام مأموریت.
/// </summary>
public interface IMissionWithdrawalService
{
    Task CancelAsync(Guid missionId, string reason, string actor, CancellationToken cancellationToken = default);
    Task EndEarlyAsync(Guid missionId, DateTimeOffset endedAtUtc, string reason, string actor, CancellationToken cancellationToken = default);
    Task CancelPendingRequestAsync(Guid missionId, string requesterUsername, string reason, CancellationToken cancellationToken = default);
    Task RequestWithdrawalAsync(Guid missionId, string requesterUsername, string reason, CancellationToken cancellationToken = default);
    Task ApproveWithdrawalAsync(Guid missionId, string reviewer, CancellationToken cancellationToken = default);
    Task RejectWithdrawalAsync(Guid missionId, string reason, string reviewer, CancellationToken cancellationToken = default);
}
