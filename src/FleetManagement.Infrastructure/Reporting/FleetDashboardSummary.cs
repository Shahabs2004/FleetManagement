namespace FleetManagement.Infrastructure.Reporting;

/// <summary>
/// خلاصه آماری صفحه اصلی سامانه.
/// </summary>
public sealed record FleetDashboardSummary(
    int PendingMissions,
    int ConfirmedMissions,
    int RunningMissions,
    int CompletedMissions,
    int CancelledMissions,
    int TodayMissions,
    int? ActiveDrivers,
    int? TotalDrivers,
    int? ReadyVehicles,
    int? TotalVehicles);
