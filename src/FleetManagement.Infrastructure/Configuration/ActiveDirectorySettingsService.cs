using System.Text.Json;
using FleetManagement.Application.Configuration;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// خواندن و ذخیره تنظیمات Active Directory در مخزن تنظیمات SQL.
/// </summary>
public sealed class ActiveDirectorySettingsService(
    ISystemSettingsStore settingsStore,
    AuditLogService auditLog)
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

    /// <summary>ذخیره از مسیر راه‌اندازی اولیه با هویت مدیر فنی.</summary>
    public Task SaveAsync(
        ActiveDirectoryConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        return SaveAsync(configuration, "TechnicalAdministrator", cancellationToken);
    }

    /// <summary>اعتبارسنجی و ذخیره تنظیمات AD با ثبت عامل واقعی در ممیزی.</summary>
    public async Task SaveAsync(
        ActiveDirectoryConfiguration configuration,
        string actorUsername,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorUsername);

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

        if (string.IsNullOrWhiteSpace(configuration.DomainDnsName)
            || string.IsNullOrWhiteSpace(configuration.LdapServer)
            || string.IsNullOrWhiteSpace(configuration.BaseDistinguishedName)
            || string.IsNullOrWhiteSpace(configuration.ServiceAccountUsername)
            || string.IsNullOrWhiteSpace(configuration.ServiceAccountPassword))
        {
            throw new ArgumentException("اطلاعات اتصال و حساب سرویس AD باید کامل باشد.");
        }

        var previous = await GetAsync(cancellationToken);

        await settingsStore.SetAsync(
            SettingKey,
            configuration,
            cancellationToken);

        await settingsStore.SetAsync(
            SkippedSettingKey,
            false,
            cancellationToken);

        // رمز حساب سرویس AD هرگز در رویداد ممیزی قرار نمی‌گیرد.
        static object? SafeSummary(ActiveDirectoryConfiguration? value)
        {
            return value is null
                ? null
                : new
                {
                    value.DomainDnsName,
                    value.LdapServer,
                    value.Port,
                    value.UseLdaps,
                    value.BaseDistinguishedName,
                    ServiceAccountConfigured = !string.IsNullOrWhiteSpace(value.ServiceAccountUsername),
                    value.OrganizationalUnitAttribute,
                    value.UseWindowsIntegratedAuthentication,
                    value.AllowUsernamePasswordFallback,
                    value.IsConnectionVerified,
                    AllowedGroups = value.AllowedGroups.Select(group => new
                    {
                        group.Name,
                        group.DistinguishedName
                    }).ToArray(),
                    ServiceAccountPasswordConfigured =
                        !string.IsNullOrWhiteSpace(value.ServiceAccountPassword)
                };
        }

        await auditLog.RecordAsync(
            actorUsername,
            "UpdateActiveDirectorySettings",
            "ActiveDirectoryConfiguration",
            null,
            "Success",
            beforeJson: JsonSerializer.Serialize(SafeSummary(previous)),
            afterJson: JsonSerializer.Serialize(SafeSummary(configuration)),
            cancellationToken: cancellationToken);
    }
}
