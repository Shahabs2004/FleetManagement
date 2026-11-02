using System.Text.Json;
using FleetManagement.Application.Auditing;
using FleetManagement.Domain.Missions;

namespace FleetManagement.Application.Missions;

/// <summary>
/// سناریوی ویرایش درخواست مأموریت تا پیش از آغاز بررسی.
/// </summary>
public sealed class MissionPendingRequestService(
    IMissionRulesService rulesService,
    IMissionPendingRequestStore store,
    IAuditLogWriter auditLogWriter)
{
    public async Task UpdatePendingRequestAsync(
        Guid missionId,
        string requesterUsername,
        string title,
        string requesterPhoneSnapshot,
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

        var stops = intermediateStops?.Select(value => value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray() ?? [];

        var requestedLocations = stops
            .Append(origin.Trim())
            .Append(destination.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var result = await store.ExecuteSerializableAsync(async (unitOfWork, token) =>
        {
            var mission = await unitOfWork.GetMissionAsync(missionId, token)
                ?? throw new KeyNotFoundException("درخواست مأموریت پیدا نشد.");

            if (!string.Equals(
                mission.RequesterUsername,
                requesterUsername,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("فقط درخواست‌کننده می‌تواند درخواست خود را ویرایش کند.");
            }

            var approvedLocations = await unitOfWork.GetApprovedLocationNamesAsync(
                requestedLocations,
                token);

            if (approvedLocations.Length != requestedLocations.Length)
            {
                throw new InvalidOperationException("مبدأ، مقصد و توقف‌ها باید از فهرست مکان‌های تأییدشده انتخاب شوند.");
            }

            var before = SerializeSnapshot(mission);
            mission.EditPendingRequest(
                title,
                requesterPhoneSnapshot,
                requestingGroup,
                type,
                purpose,
                origin,
                destination,
                stops,
                plannedStartAtUtc,
                plannedEndAtUtc,
                description,
                passengerCount,
                missionOrderNumber,
                missionOrderDate);

            return new PendingRequestUpdateResult(
                mission.Id,
                before,
                SerializeSnapshot(mission));
        }, cancellationToken);

        await auditLogWriter.RecordAsync(
            requesterUsername,
            "EditPendingRequest",
            "Mission",
            result.MissionId.ToString(),
            "Success",
            beforeJson: result.BeforeJson,
            afterJson: result.AfterJson,
            cancellationToken: cancellationToken);
    }

    private static string SerializeSnapshot(Mission mission) => JsonSerializer.Serialize(new
    {
        mission.Title,
        mission.RequesterPhoneSnapshot,
        mission.RequestingGroup,
        mission.Type,
        mission.Purpose,
        mission.Origin,
        mission.Destination,
        mission.RouteJson,
        mission.PlannedStartAtUtc,
        mission.PlannedEndAtUtc,
        mission.Description,
        mission.PassengerCount,
        mission.MissionOrderNumber,
        mission.MissionOrderDate
    });

    private sealed record PendingRequestUpdateResult(Guid MissionId, string BeforeJson, string AfterJson);
}
