using System.Text.Json;
using FleetManagement.Application.Auditing;
using FleetManagement.Application.Notifications;
using FleetManagement.Domain.Missions;

namespace FleetManagement.Application.Missions;

/// <summary>
/// هماهنگ‌کننده سناریوهای ثبت، مشاهده، شروع بررسی و رد درخواست تغییر مأموریت.
/// </summary>
public sealed class MissionChangeRequestService(
    IMissionChangeRequestStore store,
    IAuditLogWriter auditLog,
    IMissionChangeNotificationSender notificationSender)
{
    /// <summary>ثبت درخواست تغییر با کنترل مالکیت، وضعیت مأموریت و یکتایی درخواست باز.</summary>
    public async Task<Guid> RequestChangeAsync(
        Guid missionId,
        string requesterUsername,
        string reason,
        MissionChangeProposal proposal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(proposal);

        var requestId = await store.ExecuteSerializableAsync(async (unitOfWork, token) =>
        {
            var mission = await unitOfWork.GetMissionAsync(missionId, token)
                ?? throw new KeyNotFoundException("مأموریت پیدا نشد.");

            if (!string.Equals(mission.RequesterUsername, requesterUsername, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("فقط درخواست‌کننده می‌تواند برای مأموریت خود درخواست تغییر ثبت کند.");
            }

            if (mission.Status is not (MissionStatus.Confirmed or MissionStatus.Running))
            {
                throw new InvalidOperationException("درخواست تغییر پس از تأیید مأموریت قابل ثبت است.");
            }

            if (await unitOfWork.HasOpenRequestAsync(missionId, token))
            {
                throw new InvalidOperationException("برای این مأموریت یک درخواست تغییر در حال بررسی وجود دارد.");
            }

            var json = JsonSerializer.Serialize(proposal);
            var request = new MissionChangeRequest(missionId, requesterUsername, reason, json);
            unitOfWork.Add(request);
            return request.Id;
        }, cancellationToken);

        await auditLog.RecordAsync(
            requesterUsername,
            "RequestChange",
            "Mission",
            missionId.ToString(),
            "Success",
            reason,
            afterJson: JsonSerializer.Serialize(new { ChangeRequestId = requestId }),
            cancellationToken: cancellationToken);

        return requestId;
    }

    /// <summary>دریافت درخواست‌های تغییر برای بازبین یا درخواست‌کننده.</summary>
    public Task<IReadOnlyList<MissionChangeRequest>> GetChangeRequestsAsync(
        string? requesterUsername = null,
        MissionChangeRequestStatus? status = null,
        int take = 200,
        CancellationToken cancellationToken = default) =>
        store.GetChangeRequestsAsync(requesterUsername, status, take, cancellationToken);

    /// <summary>شروع بررسی درخواست و قفل‌کردن آن برای نخستین بازبین.</summary>
    public async Task StartChangeReviewAsync(
        Guid changeRequestId,
        string reviewer,
        CancellationToken cancellationToken = default)
    {
        await store.ExecuteSerializableAsync(async (unitOfWork, token) =>
        {
            var change = await unitOfWork.GetChangeRequestAsync(changeRequestId, token)
                ?? throw new KeyNotFoundException("درخواست تغییر پیدا نشد.");

            change.StartReview(reviewer, DateTimeOffset.UtcNow);
            return true;
        }, cancellationToken);

        await auditLog.RecordAsync(
            reviewer,
            "StartChangeReview",
            "MissionChangeRequest",
            changeRequestId.ToString(),
            "Success",
            cancellationToken: cancellationToken);
    }

    /// <summary>رد درخواست تغییر بدون دست‌زدن به مقادیر مأموریت اصلی.</summary>
    public async Task RejectChangeAsync(
        Guid changeRequestId,
        string reviewer,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var result = await store.ExecuteSerializableAsync(async (unitOfWork, token) =>
        {
            var change = await unitOfWork.GetChangeRequestAsync(changeRequestId, token)
                ?? throw new KeyNotFoundException("درخواست تغییر پیدا نشد.");

            change.Reject(reviewer, reason, DateTimeOffset.UtcNow);

            var mission = await unitOfWork.GetMissionAsync(change.MissionId, token)
                ?? throw new KeyNotFoundException("مأموریت مرتبط با درخواست تغییر پیدا نشد.");

            return new RejectedChangeRequest(
                change.Id,
                change.RequesterUsername,
                mission.Id,
                mission.MissionNumber);
        }, cancellationToken);

        await auditLog.RecordAsync(
            reviewer,
            "RejectChange",
            "Mission",
            result.MissionId.ToString(),
            "Rejected",
            reason,
            afterJson: JsonSerializer.Serialize(new { ChangeRequestId = result.ChangeRequestId }),
            cancellationToken: cancellationToken);

        await notificationSender.CreateAsync(
            result.RequesterUsername,
            "درخواست تغییر مأموریت رد شد",
            $"درخواست تغییر مأموریت {result.MissionNumber} رد شد. علت: {reason}",
            "MissionChangeRejected",
            "Mission",
            result.MissionId.ToString(),
            cancellationToken);
    }

    private sealed record RejectedChangeRequest(
        Guid ChangeRequestId,
        string RequesterUsername,
        Guid MissionId,
        string MissionNumber);
}
