namespace FleetManagement.Application.Missions;

/// <summary>
/// قواعد مشترک برنامه‌ریزی و ثبت مأموریت.
/// </summary>
public sealed record MissionPlanningRules(int MinimumGapHours, int BookingHorizonDays);
