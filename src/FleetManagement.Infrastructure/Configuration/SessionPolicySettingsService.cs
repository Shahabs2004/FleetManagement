using System.Text.Json;
using FleetManagement.Application.Configuration;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// خواندن و ذخیره سیاست نشست‌ها با ثبت ممیزی تغییرات.
/// </summary>
public sealed class SessionPolicySettingsService(
    ISystemSettingsStore settingsStore,
    AuditLogService auditLog)
{
    private const string SettingKey = "Security.SessionPolicy";

    /// <summary>دریافت سیاست جاری یا مقادیر پیش‌فرض.</summary>
    public async Task<SessionPolicyConfiguration> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var configuration = await settingsStore.GetAsync<SessionPolicyConfiguration>(
            SettingKey,
            cancellationToken) ?? SessionPolicyConfiguration.CreateDefault();

        Validate(configuration);
        return configuration;
    }

    /// <summary>ذخیره سیاست نشست و ثبت مقدارهای قبل و بعد.</summary>
    public async Task SaveAsync(
        SessionPolicyConfiguration configuration,
        string actorUsername,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorUsername);
        Validate(configuration);

        var previous = await GetAsync(cancellationToken);
        await settingsStore.SetAsync(SettingKey, configuration, cancellationToken);

        await auditLog.RecordAsync(
            actorUsername,
            "UpdateSessionPolicy",
            "SessionPolicy",
            null,
            "Success",
            beforeJson: JsonSerializer.Serialize(previous),
            afterJson: JsonSerializer.Serialize(configuration),
            cancellationToken: cancellationToken);
    }

    /// <summary>اعتبارسنجی محدوده امن تنظیمات نشست.</summary>
    private static void Validate(SessionPolicyConfiguration configuration)
    {
        if (configuration.IdleTimeoutMinutes is < 5 or > 120)
        {
            throw new ArgumentOutOfRangeException(
                nameof(configuration),
                "مهلت بیکاری باید بین ۵ تا ۱۲۰ دقیقه باشد.");
        }

        if (configuration.AbsoluteTimeoutHours is < 1 or > 24)
        {
            throw new ArgumentOutOfRangeException(
                nameof(configuration),
                "حداکثر عمر نشست باید بین ۱ تا ۲۴ ساعت باشد.");
        }

        if (configuration.IdleTimeoutMinutes >= configuration.AbsoluteTimeoutHours * 60)
        {
            throw new ArgumentException(
                "مهلت بیکاری باید از حداکثر عمر نشست کوتاه‌تر باشد.",
                nameof(configuration));
        }
    }
}
