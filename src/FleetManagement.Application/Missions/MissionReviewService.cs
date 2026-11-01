using System.Text.Json;
using FleetManagement.Application.Auditing;
using FleetManagement.Application.Notifications;
using FleetManagement.Domain.Missions;
using FleetManagement.Domain.Notifications;

namespace FleetManagement.Application.Missions;

/// <summary>
/// سناریوهای شروع بررسی، تأیید تخصیص منابع و رد درخواست اولیه مأموریت.
/// </summary>
public sealed class MissionReviewService(
    IMissionPlanningRulesProvider rulesProvider,
    IMissionReviewStore store,
    IMissionAuditWriter auditLog,
    IMissionNotificationSender notificationSender,
    IMissionSmsScheduler smsScheduler)
{
    /// <summary>شروع بررسی درخواست و ثبت زمان اولین بازبینی.</summary>
    public async Task StartReviewAsync(
        Guid missionId,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var mission = await store.ExecuteSerializableAsync(async (unitOfWork, token) =>
        {
            var item = await GetMissionAsync(unitOfWork, missionId, token);
            item.StartReview(DateTimeOffset.UtcNow);
            return item;
        }, cancellationToken);

        await auditLog.RecordAsync(
            actor,
            "StartReview",
            "Mission",
            missionId.ToString(),
            "Success",
            afterJson: SerializeMissionState(mission),
            cancellationToken: cancellationToken);
    }

    /// <summary>تأیید درخواست با کنترل وضعیت راننده، خودرو و تعارض زمانی به‌صورت اتمیک.</summary>
    public async Task ApproveAsync(
        Guid missionId,
        Guid driverId,
        Guid vehicleId,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var rules = await rulesProvider.GetAsync(cancellationToken);
        var approved = await store.ExecuteSerializableAsync(async (unitOfWork, token) =>
        {
            var mission = await GetMissionAsync(unitOfWork, missionId, token);
            var driver = await unitOfWork.GetDriverAsync(driverId, token)
                ?? throw new KeyNotFoundException("راننده انتخاب‌شده پیدا نشد.");
            var vehicle = await unitOfWork.GetVehicleAsync(vehicleId, token)
                ?? throw new KeyNotFoundException("خودروی انتخاب‌شده پیدا نشد.");

            if (!driver.CanBeAssignedToMission())
            {
                throw new InvalidOperationException("راننده فعال و قابل تخصیص نیست.");
            }

            if (!vehicle.CanBeAssignedToMission(hasActiveMission: false))
            {
                throw new InvalidOperationException("خودرو آماده تخصیص نیست.");
            }

            if (await unitOfWork.HasResourceConflictAsync(
                mission,
                driverId,
                vehicleId,
                TimeSpan.FromHours(rules.MinimumGapHours),
                token))
            {
                throw new InvalidOperationException("در بازه انتخاب‌شده برای راننده یا خودرو مأموریت متعارض وجود دارد.");
            }

            mission.Approve(driverId, vehicleId, actor, DateTimeOffset.UtcNow);

            return new ApprovedMission(
                mission.Id,
                mission.MissionNumber,
                mission.RequesterUsername,
                mission.Title,
                mission);
        }, cancellationToken);

        await auditLog.RecordAsync(
            actor,
            "Approve",
            "Mission",
            approved.Id.ToString(),
            "Success",
            afterJson: SerializeMissionState(approved.Mission),
            cancellationToken: cancellationToken);

        await notificationSender.CreateAsync(
            approved.RequesterUsername,
            "درخواست مأموریت تأیید شد",
            $"درخواست {approved.MissionNumber} تأیید شد و راننده و خودرو تخصیص یافتند.",
            "MissionApproved",
            "Mission",
            approved.Id.ToString(),
            cancellationToken);

        await notificationSender.CreateForRolesAsync(
            ["FleetManager", "Operator"],
            "مأموریت تأیید شد",
            $"مأموریت {approved.MissionNumber} تأیید و برای اجرا آماده شد.",
            "MissionApproved",
            "Mission",
            approved.Id.ToString(),
            cancellationToken);

        await smsScheduler.QueueSafelyAsync(approved.Mission, SmsEventType.MissionApproved, null, cancellationToken);
        await smsScheduler.QueueSafelyAsync(approved.Mission, SmsEventType.MissionStartReminder, null, cancellationToken);
    }

    /// <summary>رد درخواست با علت اجباری و اعلان به درخواست‌کننده و نقش‌های عملیاتی.</summary>
    public async Task RejectAsync(
        Guid missionId,
        string reason,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var rejected = await store.ExecuteSerializableAsync(async (unitOfWork, token) =>
        {
            var mission = await GetMissionAsync(unitOfWork, missionId, token);
            mission.Reject(reason);

            return new RejectedMission(
                mission.Id,
                mission.MissionNumber,
                mission.RequesterUsername,
                mission);
        }, cancellationToken);

        await auditLog.RecordAsync(
            actor,
            "Reject",
            "Mission",
            rejected.Id.ToString(),
            "Rejected",
            reason,
            afterJson: SerializeMissionState(rejected.Mission),
            cancellationToken: cancellationToken);

        await notificationSender.CreateAsync(
            rejected.RequesterUsername,
            "درخواست مأموریت رد شد",
            $"درخواست {rejected.MissionNumber} رد شد. علت: {reason}",
            "MissionRejected",
            "Mission",
            rejected.Id.ToString(),
            cancellationToken);

        await notificationSender.CreateForRolesAsync(
            ["FleetManager", "Operator"],
            "درخواست مأموریت رد شد",
            $"درخواست {rejected.MissionNumber} رد شد.",
            "MissionRejected",
            "Mission",
            rejected.Id.ToString(),
            cancellationToken);

        // رد درخواست، مانند رفتار قبلی، تنها پس از Commit برای صف پیامک ارسال می‌شود.
        await smsScheduler.QueueSafelyAsync(
            rejected.Mission,
            SmsEventType.MissionRejected,
            reason,
            cancellationToken);
    }

    private static string SerializeMissionState(Mission mission) =>
        JsonSerializer.Serialize(new
        {
            mission.MissionNumber,
            mission.Status,
            mission.DriverId,
            mission.VehicleId,
            mission.PlannedStartAtUtc,
            mission.PlannedEndAtUtc,
            mission.ActualStartAtUtc,
            mission.ActualEndAtUtc
        });

    private static async Task<Mission> GetMissionAsync(
        IMissionReviewUnitOfWork unitOfWork,
        Guid missionId,
        CancellationToken cancellationToken) =>
        await unitOfWork.GetMissionAsync(missionId, cancellationToken)
            ?? throw new KeyNotFoundException("مأموریت موردنظر پیدا نشد.");

    private sealed record ApprovedMission(
        Guid Id,
        string MissionNumber,
        string RequesterUsername,
        string Title,
        Mission Mission);

    private sealed record RejectedMission(
        Guid Id,
        string MissionNumber,
        string RequesterUsername,
        Mission Mission);
}
