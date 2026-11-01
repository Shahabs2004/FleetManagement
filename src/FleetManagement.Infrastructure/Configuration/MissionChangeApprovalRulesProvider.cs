using FleetManagement.Application.Missions;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// ارائه قواعد برنامه‌ریزی به سناریوی تأیید تغییر با استفاده از تنظیمات فعلی مأموریت.
/// </summary>
public sealed class MissionChangeApprovalRulesProvider(MissionManagementService missionManagementService)
    : IMissionChangeApprovalRulesProvider
{
    /// <inheritdoc />
    public async Task<MissionChangeApprovalRules> GetAsync(CancellationToken cancellationToken = default)
    {
        var rules = await missionManagementService.GetRulesAsync(cancellationToken);
        return new MissionChangeApprovalRules(rules.MinimumGapHours);
    }
}
