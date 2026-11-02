using System.Text.Json;
using FleetManagement.Application.Auditing;
using FleetManagement.Application.Notifications;
using FleetManagement.Domain.Missions;
using FleetManagement.Domain.Notifications;

namespace FleetManagement.Application.Missions;

/// <summary>
/// هماهنگ‌کننده لغو، انصراف و خاتمه زودهنگام مأموریت؛ بدون وابستگی به EF Core.
/// </summary>
public sealed class MissionWithdrawalService(
    IMissionWithdrawalStore store,
    IAuditLogWriter auditLogWriter,
    IMissionNotificationSender notificationSender,
    IMissionSmsScheduler smsScheduler) : IMissionWithdrawalService
{
    /// <summary>لغو مدیریتی مأموریت و ثبت آثار جانبی پس از Commit.</summary>
    public async Task CancelAsync(
        Guid missionId,
        string reason,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var mission = await ExecuteAsync(
            missionId,
            item => item.Cancel(reason, DateTimeOffset.UtcNow),
            cancellationToken);

        await RecordMissionAuditAsync(actor, "Cancel", mission, "Success", cancellationToken, reason);
        await notificationSender.CreateAsync(
            mission.RequesterUsername,
            "مأموریت لغو شد",
            $"مأموریت {mission.MissionNumber} لغو شد. علت: {reason}",
            "MissionCancelled",
            "Mission",
            mission.Id.ToString(),
            cancellationToken);
        await notificationSender.CreateForRolesAsync(
            ["FleetManager", "Operator"],
            "مأموریت لغو شد",
            $"مأموریت {mission.MissionNumber} لغو شد.",
            "MissionCancelled",
            "Mission",
            mission.Id.ToString(),
            cancellationToken);
        await smsScheduler.CancelUnsentRemindersSafelyAsync(
            mission.Id,
            "مأموریت لغو شد.",
            actor,
            cancellationToken);
        await smsScheduler.QueueSafelyAsync(
            mission,
            SmsEventType.MissionCancelled,
            reason,
            cancellationToken);
    }

    /// <summary>خاتمه زودهنگام مأموریت با آزادسازی فوری منابع.</summary>
    public async Task EndEarlyAsync(
        Guid missionId,
        DateTimeOffset endedAtUtc,
        string reason,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var result = await store.ExecuteSerializableAsync(async (unitOfWork, token) =>
        {
            var mission = await GetMissionAsync(unitOfWork, missionId, token);
            var beforeJson = JsonSerializer.Serialize(new
            {
                mission.Status,
                mission.PlannedEndAtUtc,
                mission.ActualEndAtUtc
            });
            mission.EndEarly(endedAtUtc, reason);
            return new EndEarlyResult(mission, beforeJson);
        }, cancellationToken);

        await auditLogWriter.RecordAsync(
            actor,
            "EndEarly",
            "Mission",
            result.Mission.Id.ToString(),
            "Success",
            reason,
            beforeJson: result.BeforeJson,
            afterJson: JsonSerializer.Serialize(new
            {
                result.Mission.Status,
                result.Mission.ActualEndAtUtc
            }),
            cancellationToken: cancellationToken);
        await notificationSender.CreateAsync(
            result.Mission.RequesterUsername,
            "مأموریت زودتر از موعد خاتمه یافت",
            $"مأموریت {result.Mission.MissionNumber} پیش از زمان برنامه‌ریزی‌شده خاتمه یافت.",
            "MissionEndedEarly",
            "Mission",
            result.Mission.Id.ToString(),
            cancellationToken);
        await notificationSender.CreateForRolesAsync(
            ["FleetManager", "Operator"],
            "مأموریت زودتر از موعد خاتمه یافت",
            $"مأموریت {result.Mission.MissionNumber} پیش از زمان برنامه‌ریزی‌شده خاتمه یافت.",
            "MissionEndedEarly",
            "Mission",
            result.Mission.Id.ToString(),
            cancellationToken);
        await smsScheduler.QueueSafelyAsync(
            result.Mission,
            SmsEventType.MissionCompleted,
            reason,
            cancellationToken);
    }

    /// <summary>لغو مستقیم درخواست تأییدنشده فقط توسط درخواست‌کننده.</summary>
    public async Task CancelPendingRequestAsync(
        Guid missionId,
        string requesterUsername,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var mission = await store.ExecuteSerializableAsync(async (unitOfWork, token) =>
        {
            var item = await GetMissionAsync(unitOfWork, missionId, token);
            EnsureRequester(item, requesterUsername);
            if (item.Status is not (MissionStatus.Pending or MissionStatus.UnderReview))
            {
                throw new InvalidOperationException("لغو مستقیم فقط برای درخواست تأییدنشده مجاز است.");
            }

            item.Cancel(reason, DateTimeOffset.UtcNow);
            return item;
        }, cancellationToken);

        await RecordMissionAuditAsync(
            requesterUsername,
            "CancelPendingRequest",
            mission,
            "Success",
            cancellationToken,
            reason);
    }

    /// <summary>ثبت درخواست انصراف توسط مالک مأموریت تأییدشده یا در حال اجرا.</summary>
    public async Task RequestWithdrawalAsync(
        Guid missionId,
        string requesterUsername,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var mission = await store.ExecuteSerializableAsync(async (unitOfWork, token) =>
        {
            var item = await GetMissionAsync(unitOfWork, missionId, token);
            EnsureRequester(item, requesterUsername);
            if (item.Status is MissionStatus.Pending or MissionStatus.UnderReview)
            {
                throw new InvalidOperationException("درخواست تأییدنشده باید مستقیماً لغو شود و نیاز به درخواست انصراف ندارد.");
            }

            item.RequestWithdrawal(reason);
            return item;
        }, cancellationToken);

        await RecordMissionAuditAsync(
            requesterUsername,
            "RequestWithdrawal",
            mission,
            "Success",
            cancellationToken,
            reason);
        await notificationSender.CreateForRolesAsync(
            ["FleetManager", "Operator"],
            "درخواست انصراف مأموریت",
            $"برای مأموریت {mission.MissionNumber} درخواست انصراف ثبت شده است.",
            "MissionWithdrawalRequested",
            "Mission",
            mission.Id.ToString(),
            cancellationToken);
    }

    /// <summary>تأیید درخواست انصراف و آزادسازی منابع.</summary>
    public async Task ApproveWithdrawalAsync(
        Guid missionId,
        string reviewer,
        CancellationToken cancellationToken = default)
    {
        var mission = await ExecuteAsync(
            missionId,
            item => item.ApproveWithdrawal(DateTimeOffset.UtcNow),
            cancellationToken);

        await RecordMissionAuditAsync(reviewer, "ApproveWithdrawal", mission, "Success", cancellationToken);
        await notificationSender.CreateAsync(
            mission.RequesterUsername,
            "درخواست انصراف تأیید شد",
            $"درخواست انصراف مأموریت {mission.MissionNumber} تأیید شد.",
            "WithdrawalApproved",
            "Mission",
            mission.Id.ToString(),
            cancellationToken);
        await notificationSender.CreateForRolesAsync(
            ["FleetManager", "Operator"],
            "درخواست انصراف تأیید شد",
            $"انصراف از مأموریت {mission.MissionNumber} تأیید شد.",
            "WithdrawalApproved",
            "Mission",
            mission.Id.ToString(),
            cancellationToken);
        await smsScheduler.CancelUnsentRemindersSafelyAsync(
            mission.Id,
            "درخواست انصراف تأیید شد.",
            reviewer,
            cancellationToken);
        await smsScheduler.QueueSafelyAsync(
            mission,
            SmsEventType.WithdrawalApproved,
            null,
            cancellationToken);
    }

    /// <summary>رد درخواست انصراف و بازگرداندن وضعیت قبلی مأموریت.</summary>
    public async Task RejectWithdrawalAsync(
        Guid missionId,
        string reason,
        string reviewer,
        CancellationToken cancellationToken = default)
    {
        var mission = await ExecuteAsync(
            missionId,
            item => item.RejectWithdrawal(reason),
            cancellationToken);

        await RecordMissionAuditAsync(
            reviewer,
            "RejectWithdrawal",
            mission,
            "Rejected",
            cancellationToken,
            reason);
        await notificationSender.CreateAsync(
            mission.RequesterUsername,
            "درخواست انصراف رد شد",
            $"درخواست انصراف مأموریت {mission.MissionNumber} رد شد. علت: {reason}",
            "WithdrawalRejected",
            "Mission",
            mission.Id.ToString(),
            cancellationToken);
        await notificationSender.CreateForRolesAsync(
            ["FleetManager", "Operator"],
            "درخواست انصراف رد شد",
            $"درخواست انصراف مأموریت {mission.MissionNumber} رد شد.",
            "WithdrawalRejected",
            "Mission",
            mission.Id.ToString(),
            cancellationToken);
        await smsScheduler.QueueSafelyAsync(
            mission,
            SmsEventType.WithdrawalRejected,
            reason,
            cancellationToken);
    }

    private Task<Mission> ExecuteAsync(
        Guid missionId,
        Action<Mission> operation,
        CancellationToken cancellationToken) =>
        store.ExecuteSerializableAsync(async (unitOfWork, token) =>
        {
            var mission = await GetMissionAsync(unitOfWork, missionId, token);
            operation(mission);
            return mission;
        }, cancellationToken);

    private static async Task<Mission> GetMissionAsync(
        IMissionWithdrawalUnitOfWork unitOfWork,
        Guid missionId,
        CancellationToken cancellationToken) =>
        await unitOfWork.GetMissionAsync(missionId, cancellationToken)
        ?? throw new KeyNotFoundException("مأموریت موردنظر پیدا نشد.");

    private static void EnsureRequester(Mission mission, string requesterUsername)
    {
        if (!string.Equals(
            mission.RequesterUsername,
            requesterUsername,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("فقط درخواست‌کننده می‌تواند درخواست خود را لغو یا برای آن انصراف ثبت کند.");
        }
    }

    private Task RecordMissionAuditAsync(
        string actor,
        string action,
        Mission mission,
        string outcome,
        CancellationToken cancellationToken,
        string? reason = null) =>
        auditLogWriter.RecordAsync(
            actor,
            action,
            "Mission",
            mission.Id.ToString(),
            outcome,
            reason,
            afterJson: JsonSerializer.Serialize(new
            {
                mission.MissionNumber,
                mission.Status,
                mission.DriverId,
                mission.VehicleId,
                mission.PlannedStartAtUtc,
                mission.PlannedEndAtUtc,
                mission.ActualStartAtUtc,
                mission.ActualEndAtUtc
            }),
            cancellationToken: cancellationToken);

    private sealed record EndEarlyResult(Mission Mission, string BeforeJson);
}
