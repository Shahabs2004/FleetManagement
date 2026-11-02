using System.Text.Json;
using FleetManagement.Application.Auditing;
using FleetManagement.Application.Notifications;
using FleetManagement.Domain.Missions;
using FleetManagement.Domain.Notifications;

namespace FleetManagement.Application.Missions;

/// <summary>
/// هماهنگ‌کننده تأیید درخواست تغییر و اعمال اتمیک مقادیر پیشنهادی مأموریت.
/// </summary>
public sealed class MissionChangeApprovalService(
    IMissionChangeRequestStore store,
    IMissionRulesService rulesService,
    IAuditLogWriter auditLog,
    IMissionNotificationSender notificationSender,
    IMissionSmsScheduler smsScheduler)
{
    /// <summary>اعتبارسنجی و اعمال اتمیک پیشنهاد تغییر؛ اعلان و پیامک پس از Commit انجام می‌شوند.</summary>
    public async Task ApproveChangeAsync(
        Guid changeRequestId,
        string reviewer,
        CancellationToken cancellationToken = default)
    {
        var rules = await rulesService.GetRulesAsync(cancellationToken);

        var result = await store.ExecuteSerializableAsync(async (unitOfWork, token) =>
        {
            var change = await unitOfWork.GetChangeRequestAsync(changeRequestId, token)
                ?? throw new KeyNotFoundException("درخواست تغییر پیدا نشد.");

            var mission = await unitOfWork.GetMissionAsync(change.MissionId, token)
                ?? throw new KeyNotFoundException("مأموریت پیدا نشد.");

            var proposal = JsonSerializer.Deserialize<MissionChangeProposal>(change.ProposedValuesJson)
                ?? throw new InvalidOperationException("اطلاعات پیشنهادی درخواست تغییر قابل خواندن نیست.");

            if (proposal.PlannedEndAtUtc <= proposal.PlannedStartAtUtc)
            {
                throw new InvalidOperationException("زمان پایان پیشنهادی باید بعد از زمان شروع باشد.");
            }

            var locationNames = proposal.IntermediateStops
                .Append(proposal.Origin)
                .Append(proposal.Destination)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var approvedLocationNames = await unitOfWork.GetApprovedLocationNamesAsync(locationNames, token);
            if (approvedLocationNames.Count != locationNames.Count)
            {
                throw new InvalidOperationException("یکی از مکان‌های پیشنهادی دیگر تأییدشده یا قابل انتخاب نیست.");
            }

            var driver = await unitOfWork.GetDriverAsync(proposal.DriverId, token)
                ?? throw new KeyNotFoundException("راننده پیشنهادی پیدا نشد.");
            var vehicle = await unitOfWork.GetVehicleAsync(proposal.VehicleId, token)
                ?? throw new KeyNotFoundException("خودروی پیشنهادی پیدا نشد.");

            if (!driver.CanBeAssignedToMission())
            {
                throw new InvalidOperationException("راننده پیشنهادی فعال و قابل تخصیص نیست.");
            }

            if (!vehicle.CanBeAssignedToMission(hasActiveMission: false))
            {
                throw new InvalidOperationException("خودروی پیشنهادی آماده تخصیص نیست.");
            }

            if (await unitOfWork.HasResourceConflictAsync(
                mission,
                proposal.DriverId,
                proposal.VehicleId,
                TimeSpan.FromHours(rules.MinimumGapHours),
                proposal.PlannedEndAtUtc,
                proposal.PlannedStartAtUtc,
                token))
            {
                throw new InvalidOperationException("زمان یا منابع پیشنهادی با مأموریت دیگری تعارض دارد.");
            }

            if (mission.VehicleId != proposal.VehicleId)
            {
                if (!proposal.OdometerStartKm.HasValue)
                {
                    throw new InvalidOperationException("برای تغییر خودرو، کیلومترشمار شروع خودروی جدید الزامی است.");
                }

                var lastOdometer = await unitOfWork.GetLastOdometerAsync(proposal.VehicleId, token);
                if (lastOdometer.HasValue && proposal.OdometerStartKm.Value < lastOdometer.Value)
                {
                    throw new InvalidOperationException("کیلومترشمار شروع خودروی جدید از آخرین مقدار ثبت‌شده کمتر است.");
                }
            }

            var before = JsonSerializer.Serialize(new
            {
                mission.Title,
                mission.Type,
                mission.Origin,
                mission.Destination,
                mission.PlannedStartAtUtc,
                mission.PlannedEndAtUtc,
                mission.DriverId,
                mission.VehicleId,
                mission.RequesterPhoneSnapshot
            });

            var previousStartAtUtc = mission.PlannedStartAtUtc;
            var previousDriverId = mission.DriverId;
            var previousVehicleId = mission.VehicleId;

            mission.ApplyApprovedChanges(
                proposal.Title,
                proposal.Type,
                proposal.Purpose,
                proposal.Origin,
                proposal.Destination,
                proposal.IntermediateStops,
                proposal.PlannedStartAtUtc,
                proposal.PlannedEndAtUtc,
                proposal.DriverId,
                proposal.VehicleId,
                proposal.RequesterName,
                proposal.RequesterPhoneSnapshot,
                proposal.RequesterOrganizationalUnit,
                proposal.RequestingGroup,
                proposal.Description,
                proposal.PassengerCount,
                proposal.MissionOrderNumber,
                proposal.MissionOrderDate,
                proposal.OdometerStartKm);

            change.Approve(reviewer, DateTimeOffset.UtcNow);

            return new ApprovedMissionChange(
                mission,
                change.Reason,
                before,
                previousStartAtUtc,
                previousDriverId,
                previousVehicleId);
        }, cancellationToken);

        await auditLog.RecordAsync(
            reviewer,
            "ApproveChange",
            "Mission",
            result.Mission.Id.ToString(),
            "Success",
            result.Reason,
            beforeJson: result.BeforeJson,
            afterJson: JsonSerializer.Serialize(new
            {
                result.Mission.Title,
                result.Mission.Type,
                result.Mission.Origin,
                result.Mission.Destination,
                result.Mission.PlannedStartAtUtc,
                result.Mission.PlannedEndAtUtc,
                result.Mission.DriverId,
                result.Mission.VehicleId,
                result.Mission.RequesterPhoneSnapshot
            }),
            cancellationToken: cancellationToken);

        await notificationSender.CreateAsync(
            result.Mission.RequesterUsername,
            "تغییرات مأموریت تأیید شد",
            $"تغییرات درخواستی مأموریت {result.Mission.MissionNumber} اعمال شد.",
            "MissionChangeApproved",
            "Mission",
            result.Mission.Id.ToString(),
            cancellationToken);

        await notificationSender.CreateForRolesAsync(
            ["FleetManager", "Operator"],
            "تغییرات مأموریت اعمال شد",
            $"تغییرات مأموریت {result.Mission.MissionNumber} تأیید و اعمال شد.",
            "MissionChangeApproved",
            "Mission",
            result.Mission.Id.ToString(),
            cancellationToken);

        await smsScheduler.QueueSafelyAsync(result.Mission, SmsEventType.MissionChanged, result.Reason, cancellationToken);

        if (result.PreviousDriverId != result.Mission.DriverId
            || result.PreviousVehicleId != result.Mission.VehicleId)
        {
            await smsScheduler.QueueSafelyAsync(
                result.Mission,
                SmsEventType.DriverOrVehicleChanged,
                result.Reason,
                cancellationToken);
        }

        if (result.PreviousStartAtUtc != result.Mission.PlannedStartAtUtc)
        {
            await smsScheduler.CancelUnsentRemindersSafelyAsync(
                result.Mission.Id,
                "زمان شروع مأموریت تغییر کرد.",
                reviewer,
                cancellationToken);

            await smsScheduler.QueueSafelyAsync(
                result.Mission,
                SmsEventType.MissionStartReminder,
                null,
                cancellationToken);
        }
    }

    private sealed record ApprovedMissionChange(
        Mission Mission,
        string Reason,
        string BeforeJson,
        DateTimeOffset PreviousStartAtUtc,
        Guid? PreviousDriverId,
        Guid? PreviousVehicleId);
}
