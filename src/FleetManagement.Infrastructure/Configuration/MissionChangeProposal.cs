namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// تصویر کامل اطلاعات قابل تغییر مأموریت که تا زمان تأیید به رکورد اصلی اعمال نمی‌شود.
/// </summary>
public sealed record MissionChangeProposal(
    string Title,
    FleetManagement.Domain.Missions.MissionType Type,
    string Purpose,
    string Origin,
    string Destination,
    IReadOnlyList<string> IntermediateStops,
    DateTimeOffset PlannedStartAtUtc,
    DateTimeOffset PlannedEndAtUtc,
    Guid DriverId,
    Guid VehicleId,
    string RequesterPhoneSnapshot,
    string RequestingGroup,
    string? Description,
    int? PassengerCount,
    string? MissionOrderNumber,
    DateOnly? MissionOrderDate,
    long? OdometerStartKm);
