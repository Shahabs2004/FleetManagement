using System.Text.Json;
using FleetManagement.Application.Auditing;

namespace FleetManagement.Application.Configuration;

/// <summary>
/// اجرای سناریوهای کاربردی خواندن و ذخیره قواعد برنامه‌ریزی مأموریت.
/// </summary>
public sealed class MissionRulesService(
    ISystemSettingsStore settingsStore,
    IAuditLogWriter auditLogWriter) : IMissionRulesService
{
    private const string RulesKey = "Missions.Rules";

    /// <inheritdoc />
    public async Task<MissionRulesConfiguration> GetRulesAsync(
        CancellationToken cancellationToken = default)
    {
        var rules = await settingsStore.GetAsync<MissionRulesConfiguration>(
            RulesKey,
            cancellationToken) ?? MissionRulesConfiguration.CreateDefault();

        if (!IsValid(rules))
        {
            throw new InvalidOperationException("قواعد برنامه‌ریزی مأموریت در تنظیمات معتبر نیست.");
        }

        return rules;
    }

    /// <inheritdoc />
    public async Task SaveRulesAsync(
        MissionRulesConfiguration rules,
        string actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rules);

        if (!IsValid(rules))
        {
            throw new ArgumentOutOfRangeException(
                nameof(rules),
                "مقادیر قواعد مأموریت خارج از محدوده مجاز هستند.");
        }

        await settingsStore.SetAsync(RulesKey, rules, cancellationToken);

        await auditLogWriter.RecordAsync(
            actor,
            "UpdateRules",
            "MissionRules",
            null,
            "Success",
            afterJson: JsonSerializer.Serialize(rules),
            cancellationToken: cancellationToken);
    }

    /// <summary>محدوده مجاز مقادیر قواعد برنامه‌ریزی را بررسی می‌کند.</summary>
    private static bool IsValid(MissionRulesConfiguration rules)
        => rules.MinimumGapHours is >= 0 and <= 24
            && rules.BookingHorizonDays is >= 1 and <= 730;
}
