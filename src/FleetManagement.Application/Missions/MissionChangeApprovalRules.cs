namespace FleetManagement.Application.Missions;

/// <summary>
/// بخش موردنیاز قواعد برنامه‌ریزی برای تأیید درخواست تغییر مأموریت.
/// </summary>
public sealed record MissionChangeApprovalRules(int MinimumGapHours);
