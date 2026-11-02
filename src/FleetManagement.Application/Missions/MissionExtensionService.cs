using System.Text.Json;
using FleetManagement.Application.Auditing;
using FleetManagement.Application.Configuration;
using FleetManagement.Application.Notifications;
using FleetManagement.Domain.Missions;
using FleetManagement.Domain.Notifications;

namespace FleetManagement.Application.Missions;

/// <summary>
/// هماهنگ‌کننده تمدید مأموریت و کنترل تعارض منابع در لایه Application.
/// </summary>
public sealed class MissionExtensionService(
    IMissionExtensionStore store,
    IMissionRulesService rulesService,
    IAuditLogWriter auditLogWriter,
    IMissionNotificationSender notificationSender,
    IMissionSmsScheduler smsScheduler) : IMissionExtensionService
{
    /// <summary>تمدید زمان پایان مأموریت با رعایت فاصله و تعارض منابع.</summary>
    public async Task ExtendAsync(
        Guid missionId,
        DateTimeOffset newEndAtUtc,
        string reason,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var rules = await rulesService.GetRulesAsync(cancellationToken);
        var result = await store.ExecuteSerializableAsync(async (unitOfWork, token) =>
        {
            var mission = await unitOfWork.GetMissionAsync(missionId, token)
                ?? throw new KeyNotFoundException("مأموریت موردنظر پیدا نشد.");

            if (mission.DriverId is null || mission.VehicleId is null)
            {
                throw new InvalidOperationException("راننده و خودرو برای مأموریت مشخص نشده‌اند.");
            }

            var candidates = await unitOfWork.GetResourceConflictCandidatesAsync(
                mission.Id,
                mission.DriverId.Value,
                mission.VehicleId.Value,
                token);
            var minimumGap = TimeSpan.FromHours(rules.MinimumGapHours);
            var hasConflict = candidates.Any(existing =>
                existing.Status is (MissionStatus.Confirmed
                    or MissionStatus.Running
                    or MissionStatus.WithdrawalPending)
                && mission.PlannedStartAtUtc < existing.PlannedEndAtUtc.Add(minimumGap)
                && newEndAtUtc.Add(minimumGap) > existing.PlannedStartAtUtc);

            if (hasConflict)
            {
                throw new InvalidOperationException("تمدید انتخاب‌شده با مأموریت دیگری تداخل دارد.");
            }

            var oldEnd = mission.PlannedEndAtUtc;
            mission.Extend(newEndAtUtc, reason);
            return new ExtensionResult(mission, oldEnd, newEndAtUtc);
        }, cancellationToken);

        await auditLogWriter.RecordAsync(
            actor,
            "Extend",
            "Mission",
            result.Mission.Id.ToString(),
            "Success",
            reason,
            beforeJson: JsonSerializer.Serialize(new { PlannedEndAtUtc = result.OldEndAtUtc }),
            afterJson: JsonSerializer.Serialize(new { PlannedEndAtUtc = result.NewEndAtUtc }),
            cancellationToken: cancellationToken);
        await notificationSender.CreateAsync(
            result.Mission.RequesterUsername,
            "زمان مأموریت تغییر کرد",
            $"زمان پایان مأموریت {result.Mission.MissionNumber} به‌روزرسانی شد.",
            "MissionExtended",
            "Mission",
            result.Mission.Id.ToString(),
            cancellationToken);
        await notificationSender.CreateForRolesAsync(
            ["FleetManager", "Operator"],
            "زمان مأموریت تغییر کرد",
            $"زمان پایان مأموریت {result.Mission.MissionNumber} به‌روزرسانی شد.",
            "MissionExtended",
            "Mission",
            result.Mission.Id.ToString(),
            cancellationToken);
        await smsScheduler.QueueSafelyAsync(
            result.Mission,
            SmsEventType.MissionChanged,
            reason,
            cancellationToken);
    }

    private sealed record ExtensionResult(
        Mission Mission,
        DateTimeOffset OldEndAtUtc,
        DateTimeOffset NewEndAtUtc);
}
