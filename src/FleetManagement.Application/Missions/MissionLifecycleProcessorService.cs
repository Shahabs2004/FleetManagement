using System.Text.Json;
using FleetManagement.Application.Auditing;
using FleetManagement.Application.Notifications;
using FleetManagement.Domain.Missions;
using FleetManagement.Domain.Notifications;

namespace FleetManagement.Application.Missions;

/// <summary>
/// هماهنگ‌کننده شروع، تکمیل و بستن خودکار مأموریت‌های سررسیدشده.
/// </summary>
public sealed class MissionLifecycleProcessorService(
    IMissionLifecycleProcessorStore store,
    IAuditLogWriter auditLogWriter,
    IMissionNotificationSender notificationSender,
    IMissionSmsScheduler smsScheduler) : IMissionLifecycleProcessorService
{
    /// <summary>پردازش همه مأموریت‌های سررسیدشده و اجرای آثار جانبی پس از Commit.</summary>
    public async Task<int> ProcessDueMissionsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var processed = await store.ExecuteSerializableAsync(async (unitOfWork, token) =>
        {
            var dueMissions = await unitOfWork.GetDueMissionsAsync(now, token);
            var actions = new List<ProcessedMission>(dueMissions.Count);

            foreach (var mission in dueMissions)
            {
                if (mission.Status == MissionStatus.Confirmed)
                {
                    mission.StartAutomatically(now);
                    actions.Add(new ProcessedMission(mission, "AutoStart"));
                }
                else if (mission.Status == MissionStatus.Running)
                {
                    mission.CompleteAutomatically();
                    actions.Add(new ProcessedMission(mission, "AutoComplete"));
                }
                else if (mission.Status == MissionStatus.WithdrawalPending)
                {
                    mission.AutoCloseExpiredWithdrawal(now);
                    actions.Add(new ProcessedMission(mission, "AutoCloseWithdrawal"));
                }
            }

            return actions;
        }, cancellationToken);

        foreach (var item in processed)
        {
            await RecordMissionAuditAsync(
                "system",
                item.Action,
                item.Mission,
                "Success",
                cancellationToken);

            if (item.Action == "AutoCloseWithdrawal")
            {
                await smsScheduler.CancelUnsentRemindersSafelyAsync(
                    item.Mission.Id,
                    "مهلت مأموریت هنگام بررسی انصراف به پایان رسید.",
                    "system",
                    cancellationToken);
                await smsScheduler.QueueSafelyAsync(
                    item.Mission,
                    SmsEventType.MissionCancelled,
                    "پایان زمان برنامه‌ریزی‌شده در زمان بررسی انصراف",
                    cancellationToken);
            }

            if (item.Action == "AutoComplete")
            {
                await notificationSender.CreateForRolesAsync(
                    ["Operator"],
                    "مأموریت پایان یافت",
                    $"مأموریت {item.Mission.MissionNumber} به پایان برنامه‌ریزی‌شده رسید.",
                    "MissionCompleted",
                    "Mission",
                    item.Mission.Id.ToString(),
                    cancellationToken);
                await smsScheduler.QueueSafelyAsync(
                    item.Mission,
                    SmsEventType.MissionCompleted,
                    null,
                    cancellationToken);
            }
        }

        return processed.Count;
    }

    private Task RecordMissionAuditAsync(
        string actor,
        string action,
        Mission mission,
        string outcome,
        CancellationToken cancellationToken) =>
        auditLogWriter.RecordAsync(
            actor,
            action,
            "Mission",
            mission.Id.ToString(),
            outcome,
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

    private sealed record ProcessedMission(Mission Mission, string Action);
}
