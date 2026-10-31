using FleetManagement.Application.Configuration;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// خواندن و ذخیره تنظیمات Active Directory در مخزن تنظیمات SQL.
/// </summary>
public sealed class ActiveDirectorySettingsService(ISystemSettingsStore settingsStore)
{
    private const string SettingKey = "Directory.ActiveDirectory";

    /// <summary>خواندن تنظیمات فعلی AD.</summary>
    public Task<ActiveDirectoryConfiguration?> GetAsync(
        CancellationToken cancellationToken = default)
    {
        return settingsStore.GetAsync<ActiveDirectoryConfiguration>(
            SettingKey,
            cancellationToken);
    }

    /// <summary>اعتبارسنجی اولیه و ذخیره تنظیمات AD.</summary>
    public Task SaveAsync(
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

        return settingsStore.SetAsync(
            SettingKey,
            configuration,
            cancellationToken);
    }
}
