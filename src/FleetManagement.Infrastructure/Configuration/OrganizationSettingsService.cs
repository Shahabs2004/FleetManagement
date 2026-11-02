using System.Text.Json;
using FleetManagement.Application.Configuration;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// مدیریت نام و نشان سازمان در تنظیمات SQL.
/// </summary>
public sealed class OrganizationSettingsService(
    ISystemSettingsStore settingsStore,
    AuditLogService auditLog)
{
    private const string SettingKey = "Organization.Profile";

    /// <summary>خواندن اطلاعات هویتی سازمان.</summary>
    public Task<OrganizationProfileConfiguration?> GetAsync(
        CancellationToken cancellationToken = default)
    {
        return settingsStore.GetAsync<OrganizationProfileConfiguration>(
            SettingKey,
            cancellationToken);
    }

    /// <summary>ذخیره از مسیر راه‌اندازی اولیه با هویت مدیر فنی.</summary>
    public Task SaveAsync(
        OrganizationProfileConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        return SaveAsync(configuration, "TechnicalAdministrator", cancellationToken);
    }

    /// <summary>ذخیره نام و نشان سازمان و ثبت عامل واقعی در ممیزی.</summary>
    public async Task SaveAsync(
        OrganizationProfileConfiguration configuration,
        string actorUsername,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(configuration.OrganizationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorUsername);

        if (configuration.OrganizationName.Trim().Length > 200)
        {
            throw new ArgumentOutOfRangeException(
                nameof(configuration),
                "نام سازمان حداکثر ۲۰۰ نویسه است.");
        }

        if (!string.IsNullOrWhiteSpace(configuration.LogoBase64))
        {
            if (configuration.LogoBase64.Length > 2_000_000)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(configuration),
                    "حجم نشان سازمان بیش از حد مجاز است.");
            }

            if (configuration.LogoContentType is not ("image/png" or "image/jpeg"))
            {
                throw new ArgumentException("قالب نشان سازمان باید PNG یا JPEG باشد.");
            }
        }
        else
        {
            configuration.LogoContentType = null;
        }

        configuration.OrganizationName = configuration.OrganizationName.Trim();

        var previous = await GetAsync(cancellationToken);

        await settingsStore.SetAsync(
            SettingKey,
            configuration,
            cancellationToken);

        await auditLog.RecordAsync(
            actorUsername,
            "UpdateOrganizationProfile",
            "OrganizationProfile",
            null,
            "Success",
            beforeJson: JsonSerializer.Serialize(previous is null
                ? null
                : new { previous.OrganizationName, previous.LogoContentType, HasLogo = !string.IsNullOrWhiteSpace(previous.LogoBase64) }),
            afterJson: JsonSerializer.Serialize(new
            {
                configuration.OrganizationName,
                configuration.LogoContentType,
                HasLogo = !string.IsNullOrWhiteSpace(configuration.LogoBase64)
            }),
            cancellationToken: cancellationToken);
    }
}
