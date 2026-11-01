using FleetManagement.Domain.Missions;

namespace FleetManagement.Application.Missions;

/// <summary>
/// قرارداد داده‌ای مقادیر پیشنهادی برای تغییر مأموریت؛ تا زمان تأیید به مأموریت اصلی اعمال نمی‌شود.
/// </summary>
public sealed record MissionChangeProposal(
    string Title,
    MissionType Type,
    string Purpose,
    string Origin,
    string Destination,
    IReadOnlyList<string> IntermediateStops,
    DateTimeOffset PlannedStartAtUtc,
    DateTimeOffset PlannedEndAtUtc,
    Guid DriverId,
    Guid VehicleId,
    string RequesterName,
    string RequesterPhoneSnapshot,
    string RequesterOrganizationalUnit,
    string RequestingGroup,
    string? Description,
    int? PassengerCount,
    string? MissionOrderNumber,
    DateOnly? MissionOrderDate,
    long? OdometerStartKm);
