using FleetManagement.Application.Configuration;

namespace FleetManagement.Application.Missions;

/// <summary>
/// قرارداد مدیریت قواعد برنامه‌ریزی مأموریت.
/// </summary>
public interface IMissionRulesService
{
    Task<MissionRulesConfiguration> GetRulesAsync(CancellationToken cancellationToken = default);
    Task SaveRulesAsync(MissionRulesConfiguration rules, string actor, CancellationToken cancellationToken = default);
}
