namespace FleetManagement.Application.Missions;

/// <summary>
/// قرارداد دریافت قواعد زمان‌بندی لازم برای تأیید تغییر مأموریت.
/// </summary>
public interface IMissionChangeApprovalRulesProvider
{
    /// <summary>دریافت حداقل فاصله مجاز بین مأموریت‌های متوالی.</summary>
    Task<MissionChangeApprovalRules> GetAsync(CancellationToken cancellationToken = default);
}
