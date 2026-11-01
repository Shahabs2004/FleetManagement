namespace FleetManagement.Application.Missions;

/// <summary>
/// قرارداد دریافت قواعد زمان‌بندی مأموریت بدون وابستگی به محل نگهداری تنظیمات.
/// </summary>
public interface IMissionPlanningRulesProvider
{
    /// <summary>دریافت قواعد معتبر برنامه‌ریزی مأموریت.</summary>
    Task<MissionPlanningRules> GetAsync(CancellationToken cancellationToken = default);
}
