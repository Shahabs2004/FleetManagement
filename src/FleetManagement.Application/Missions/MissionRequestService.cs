using System.Text.Json;
using FleetManagement.Application.Auditing;
using FleetManagement.Application.Notifications;
using FleetManagement.Domain.Missions;

namespace FleetManagement.Application.Missions;

/// <summary>
/// سناریوی ثبت درخواست مأموریت و هماهنگی ممیزی و اعلان پس از ثبت پایدار.
/// </summary>
public sealed class MissionRequestService(
    IMissionRulesService rulesService,
    IMissionRequestStore store,
    IAuditLogWriter auditLog,
    IMissionNotificationSender notificationSender)
{
    /// <summary>ثبت درخواست با کنترل بازه زمانی و افق مجاز رزرو.</summary>
    public async Task<Guid> CreateRequestAsync(
        string title,
        string requesterUsername,
        string requesterName,
        string requesterPhoneSnapshot,
        string requesterOrganizationalUnit,
        string requestingGroup,
        MissionType type,
        string purpose,
        string origin,
        string destination,
        IEnumerable<string>? intermediateStops,
        DateTimeOffset plannedStartAtUtc,
        DateTimeOffset plannedEndAtUtc,
        string? description,
        int? passengerCount,
        string? missionOrderNumber,
        DateOnly? missionOrderDate,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var rules = await rulesService.GetRulesAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;

        if (plannedStartAtUtc < now)
        {
            throw new InvalidOperationException("زمان شروع مأموریت نمی‌تواند در گذشته باشد.");
        }

        if (plannedStartAtUtc > now.AddDays(rules.BookingHorizonDays)
            || plannedEndAtUtc > now.AddDays(rules.BookingHorizonDays))
        {
            throw new InvalidOperationException("بازه انتخاب‌شده از افق مجاز رزرو مأموریت دورتر است.");
        }

        var created = await store.ExecuteSerializableAsync(async (unitOfWork, token) =>
        {
            var missionNumber = await unitOfWork.GenerateMissionNumberAsync(now, token);
            var mission = new Mission(
                missionNumber,
                title,
                requesterUsername,
                requesterName,
                requesterPhoneSnapshot,
                requesterOrganizationalUnit,
                requestingGroup,
                type,
                purpose,
                origin,
                destination,
                intermediateStops,
                plannedStartAtUtc,
                plannedEndAtUtc,
                description,
                passengerCount,
                missionOrderNumber,
                missionOrderDate);

            unitOfWork.Add(mission);
            return new CreatedMission(
                mission.Id,
                mission.MissionNumber,
                mission.Title,
                mission.RequesterName,
                mission.RequesterUsername,
                mission.PlannedStartAtUtc,
                mission.PlannedEndAtUtc,
                mission.Origin,
                mission.Destination);
        }, cancellationToken);

        await auditLog.RecordAsync(
            actor,
            "CreateRequest",
            "Mission",
            created.Id.ToString(),
            "Success",
            afterJson: JsonSerializer.Serialize(new
            {
                created.MissionNumber,
                created.Title,
                created.RequesterUsername,
                created.PlannedStartAtUtc,
                created.PlannedEndAtUtc,
                created.Origin,
                created.Destination
            }),
            cancellationToken: cancellationToken);

        await notificationSender.CreateForRolesAsync(
            ["FleetManager", "Operator"],
            "درخواست مأموریت جدید",
            $"درخواست {created.MissionNumber} توسط {created.RequesterName} ثبت شد و در انتظار بررسی است.",
            "MissionRequestCreated",
            "Mission",
            created.Id.ToString(),
            cancellationToken);

        return created.Id;
    }

    private sealed record CreatedMission(
        Guid Id,
        string MissionNumber,
        string Title,
        string RequesterName,
        string RequesterUsername,
        DateTimeOffset PlannedStartAtUtc,
        DateTimeOffset PlannedEndAtUtc,
        string Origin,
        string Destination);
}
