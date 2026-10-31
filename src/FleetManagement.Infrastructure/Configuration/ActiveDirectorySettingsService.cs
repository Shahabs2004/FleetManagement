using FleetManagement.Application.Configuration;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// خواندن و ذخیره تنظیمات Active Directory در مخزن تنظیمات SQL.
/// </summary>
public sealed class ActiveDirectorySettingsService(ISystemSettingsStore settingsStore)
{
    private const string SettingKey = "Directory.ActiveDirectory";
    private const string SkippedSettingKey = "Directory.ActiveDirectory.SetupSkipped";

    /// <summary>خواندن تنظیمات فعلی AD.</summary>
    public Task<ActiveDirectoryConfiguration?> GetAsync(
        CancellationToken cancellationToken = default)
    {
        return settingsStore.GetAsync<ActiveDirectoryConfiguration>(
            SettingKey,
            cancellationToken);
    }

    /// <summary>بررسی اینکه کاربر در ویزارد از پیکربندی AD عبور کرده است.</summary>
    public async Task<bool> IsSkippedAsync(CancellationToken cancellationToken = default)
    {
        return await settingsStore.GetAsync<bool>(SkippedSettingKey, cancellationToken);
    }

    /// <summary>ثبت عبور موقت از مرحله AD؛ ورود عادی تا پیکربندی واقعی مسدود می‌ماند.</summary>
    public Task MarkSkippedAsync(CancellationToken cancellationToken = default)
    {
        return settingsStore.SetAsync(SkippedSettingKey, true, cancellationToken);
    }

    /// <summary>اعتبارسنجی اولیه و ذخیره تنظیمات AD.</summary>
    public async Task SaveAsync(
        ActiveDirectoryConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (configuration.Port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(
                nameof(configuration),
                "درگاه LDAP باید بین ۱ تا ۶۵۵۳۵ باشد.");
        }

        if (configuration.AllowedGroups.Count == 0)
        {
            throw new ArgumentException(
                "حداقل یک گروه مجاز AD باید تعریف شود.",
                nameof(configuration));
        }

        if (configuration.AllowedGroups.Any(group =>
                string.IsNullOrWhiteSpace(group.Name)
                || string.IsNullOrWhiteSpace(group.DistinguishedName)))
        {
            throw new ArgumentException(
                "نام و Distinguished Name همه گروه‌های مجاز باید تکمیل شود.",
                nameof(configuration));
        }

        await settingsStore.SetAsync(
            SettingKey,
            configuration,
            cancellationToken);

        await settingsStore.SetAsync(
            SkippedSettingKey,
            false,
            cancellationToken);
    }
}
