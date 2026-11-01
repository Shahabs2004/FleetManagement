using FleetManagement.Application.Missions;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// سازگارکننده قواعد مشترک برنامه‌ریزی با سناریوی تأیید تغییر مأموریت.
/// </summary>
public sealed class MissionChangeApprovalRulesProvider(IMissionPlanningRulesProvider rulesProvider)
    : IMissionChangeApprovalRulesProvider
{
    /// <inheritdoc />
    public async Task<MissionChangeApprovalRules> GetAsync(CancellationToken cancellationToken = default)
    {
        var rules = await rulesProvider.GetAsync(cancellationToken);
        return new MissionChangeApprovalRules(rules.MinimumGapHours);
    }
}
