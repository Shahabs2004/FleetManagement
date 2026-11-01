namespace FleetManagement.Application.Configuration;

/// <summary>
/// قرارداد سناریوهای کاربردی مدیریت قواعد برنامه‌ریزی مأموریت.
/// </summary>
public interface IMissionRulesService
{
    /// <summary>قواعد فعلی برنامه‌ریزی مأموریت را بارگذاری می‌کند.</summary>
    Task<MissionRulesConfiguration> GetRulesAsync(CancellationToken cancellationToken = default);

    /// <summary>قواعد برنامه‌ریزی مأموریت را اعتبارسنجی و ذخیره می‌کند.</summary>
    Task SaveRulesAsync(
        MissionRulesConfiguration rules,
        string actor,
        CancellationToken cancellationToken = default);
}
