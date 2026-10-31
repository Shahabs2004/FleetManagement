using FleetManagement.Application.Configuration;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// مدیریت تنظیمات نقش‌ها و نگاشت‌های مجوز در SQL.
/// </summary>
public sealed class AuthorizationSettingsService(ISystemSettingsStore settingsStore)
{
    private const string SettingKey = "Security.Authorization";

    /// <summary>خواندن تنظیمات؛ در نبود تنظیمات، نقش‌های پایه بازگردانده می‌شوند.</summary>
    public async Task<AuthorizationConfiguration> GetAsync(
        CancellationToken cancellationToken = default)
    {
        return await settingsStore.GetAsync<AuthorizationConfiguration>(
            SettingKey,
            cancellationToken)
            ?? AuthorizationConfiguration.CreateDefault();
    }

    /// <summary>اعتبارسنجی و ذخیره تنظیمات نقش و مجوز.</summary>
    public async Task SaveAsync(
        AuthorizationConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var requiredSystemRoles = new[] 
        {
            "SystemAdministrator",
            "FleetManager",
            "Operator",
            "Viewer"
        };

        var roleKeys = configuration.Roles
            .Select(role => role.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var requiredRole in requiredSystemRoles)
        {
            var systemRole = configuration.Roles.SingleOrDefault(role =>
                string.Equals(role.Key, requiredRole, StringComparison.OrdinalIgnoreCase));

            if (systemRole is null || !systemRole.IsSystemRole)
            {
                throw new ArgumentException(
                    $"نقش پایه {requiredRole} باید حفظ شود و قابل تبدیل به نقش عادی نیست.",
                    nameof(configuration));
            }
        }

        if (configuration.Roles.Any(role =>
                string.IsNullOrWhiteSpace(role.Key)
                || string.IsNullOrWhiteSpace(role.Name)
                || role.Key.Length > 64
                || role.Name.Length > 100))
        {
            throw new ArgumentException("اطلاعات یکی از نقش‌ها معتبر نیست.", nameof(configuration));
        }

        if (configuration.Roles
            .GroupBy(role => role.Key, StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Count() > 1))
        {
            throw new ArgumentException("کلید نقش‌ها باید یکتا باشد.", nameof(configuration));
        }

        if (configuration.Roles.Any(role => role.Permissions.Any(permission =>
                string.IsNullOrWhiteSpace(permission.Permission)
                || !Enum.IsDefined(permission.Effect))))
        {
            throw new ArgumentException("قاعده مجوز یکی از نقش‌ها معتبر نیست.", nameof(configuration));
        }

        if (configuration.GroupMappings.Any(mapping =>
                string.IsNullOrWhiteSpace(mapping.GroupDistinguishedName)
                || !roleKeys.Contains(mapping.RoleKey)))
        {
            throw new ArgumentException("نگاشت گروه AD به نقش نامعتبر است.", nameof(configuration));
        }

        if (configuration.ManualUserRoles.Any(mapping =>
                string.IsNullOrWhiteSpace(mapping.Username)
                || mapping.RoleKeys.Count == 0
                || mapping.RoleKeys.Any(role => !roleKeys.Contains(role))))
        {
            throw new ArgumentException("تخصیص دستی نقش به کاربر نامعتبر است.", nameof(configuration));
        }

        await settingsStore.SetAsync(
            SettingKey,
            configuration,
            cancellationToken);
    }
}
