using FleetManagement.Domain.Missions;

namespace FleetManagement.Application.Missions;

/// <summary>
/// تصویر کامل اطلاعات قابل تغییر مأموریت که تا زمان تأیید به رکورد اصلی اعمال نمی‌شود.
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
