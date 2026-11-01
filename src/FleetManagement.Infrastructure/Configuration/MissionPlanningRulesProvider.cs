using FleetManagement.Application.Configuration;
using FleetManagement.Application.Missions;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// خواندن و اعتبارسنجی قواعد برنامه‌ریزی از مخزن تنظیمات بدون وابستگی به سرویس مدیریت مأموریت.
/// </summary>
public sealed class MissionPlanningRulesProvider(ISystemSettingsStore settingsStore)
    : IMissionPlanningRulesProvider
{
    private const string RulesKey = "Missions.Rules";

    /// <inheritdoc />
    public async Task<MissionPlanningRules> GetAsync(CancellationToken cancellationToken = default)
    {
        var rules = await settingsStore.GetAsync<MissionRulesConfiguration>(RulesKey, cancellationToken)
            ?? MissionRulesConfiguration.CreateDefault();

        if (rules.MinimumGapHours is < 0 or > 24 || rules.BookingHorizonDays is < 1 or > 730)
        {
            throw new InvalidOperationException("قواعد برنامه‌ریزی مأموریت در تنظیمات معتبر نیست.");
        }

        return new MissionPlanningRules(rules.MinimumGapHours, rules.BookingHorizonDays);
    }
}
