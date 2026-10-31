using FleetManagement.Application.Configuration;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// مدیریت نام و نشان سازمان در تنظیمات SQL.
/// </summary>
public sealed class OrganizationSettingsService(ISystemSettingsStore settingsStore)
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

    /// <summary>اعتبارسنجی و ذخیره نام و نشان سازمان.</summary>
    public Task SaveAsync(
        OrganizationProfileConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(configuration.OrganizationName);

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

            if (configuration.LogoContentType is not ("image/png" or "image/jpeg" or "image/svg+xml"))
            {
                throw new ArgumentException("قالب نشان سازمان باید PNG، JPEG یا SVG باشد.");
            }
        }

        configuration.OrganizationName = configuration.OrganizationName.Trim();

        return settingsStore.SetAsync(
            SettingKey,
            configuration,
            cancellationToken);
    }
}
