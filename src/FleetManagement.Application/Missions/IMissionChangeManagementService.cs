using FleetManagement.Domain.Missions;

namespace FleetManagement.Application.Missions;

/// <summary>
/// قرارداد کاربردی مدیریت درخواست تغییر مأموریت.
/// </summary>
public interface IMissionChangeManagementService
{
    /// <summary>ثبت درخواست تغییر توسط درخواست‌کننده.</summary>
    Task<Guid> RequestChangeAsync(Guid missionId, string requesterUsername, string reason, MissionChangeProposal proposal, CancellationToken cancellationToken = default);

    /// <summary>دریافت فهرست درخواست‌های تغییر برای بازبین یا درخواست‌کننده.</summary>
    Task<IReadOnlyList<MissionChangeRequest>> GetChangeRequestsAsync(string? requesterUsername = null, MissionChangeRequestStatus? status = null, int take = 200, CancellationToken cancellationToken = default);

    /// <summary>شروع بررسی درخواست تغییر.</summary>
    Task StartChangeReviewAsync(Guid changeRequestId, string reviewer, CancellationToken cancellationToken = default);

    /// <summary>تأیید و اعمال اتمیک پیشنهاد تغییر.</summary>
    Task ApproveChangeAsync(Guid changeRequestId, string reviewer, CancellationToken cancellationToken = default);

    /// <summary>رد درخواست تغییر با ثبت علت.</summary>
    Task RejectChangeAsync(Guid changeRequestId, string reviewer, string reason, CancellationToken cancellationToken = default);
}
