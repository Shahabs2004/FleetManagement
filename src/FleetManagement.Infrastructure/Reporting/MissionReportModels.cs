namespace FleetManagement.Infrastructure.Reporting;

/// <summary>
/// فیلترهای مشترک گزارش مأموریت.
/// </summary>
public sealed class MissionReportFilter
{
    /// <summary>ابتدای بازه زمانی UTC.</summary>
    public DateTimeOffset? FromUtc { get; set; }

    /// <summary>انتهای بازه زمانی UTC.</summary>
    public DateTimeOffset? ToUtc { get; set; }

    /// <summary>شناسه راننده.</summary>
    public Guid? DriverId { get; set; }

    /// <summary>شناسه خودرو.</summary>
    public Guid? VehicleId { get; set; }

    /// <summary>نوع مأموریت.</summary>
    public FleetManagement.Domain.Missions.MissionType? Type { get; set; }

    /// <summary>واحد سازمانی درخواست‌کننده.</summary>
    public string? OrganizationalUnit { get; set; }

    /// <summary>وضعیت مأموریت.</summary>
    public FleetManagement.Domain.Missions.MissionStatus? Status { get; set; }
}

/// <summary>عملکرد تجمیعی راننده؛ سه شاخص مسافت، تعداد و ساعت کنار هم.</summary>
public sealed record DriverPerformanceRow(
    Guid DriverId,
    string DriverName,
    long? TotalKilometers,
    int MissionCount,
    decimal TotalHours);

/// <summary>عملکرد تجمیعی خودرو.</summary>
public sealed record VehiclePerformanceRow(
    Guid VehicleId,
    string VehicleName,
    long? TotalKilometers,
    int MissionCount);

/// <summary>مسافت پیموده‌شده راننده در ماه جلالی.</summary>
public sealed record MonthlyDriverMileageRow(
    Guid DriverId,
    string DriverName,
    int JalaliYear,
    int JalaliMonth,
    long TotalKilometers);

/// <summary>گزارش مأموریت لغوشده و علت آن.</summary>
public sealed record CancelledMissionRow(
    string MissionNumber,
    string Title,
    string? Reason,
    DateTimeOffset CreatedAtUtc,
    string RequesterUsername);

/// <summary>تعداد درخواست‌های تأییدشده و ردشده.</summary>
public sealed record MissionDecisionSummary(int ApprovedCount, int RejectedCount);

/// <summary>خروجی یکپارچه گزارش‌های عملکرد ناوگان.</summary>
public sealed record MissionReportResult(
    IReadOnlyList<DriverPerformanceRow> Drivers,
    IReadOnlyList<VehiclePerformanceRow> Vehicles,
    IReadOnlyList<MonthlyDriverMileageRow> MonthlyDriverMileage,
    IReadOnlyList<CancelledMissionRow> CancelledMissions,
    MissionDecisionSummary Decisions,
    int FreightMissionCount,
    int PassengerMissionCount);
