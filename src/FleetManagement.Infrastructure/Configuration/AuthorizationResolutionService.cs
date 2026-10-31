namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// محاسبه نقش و مجوز مؤثر کاربر از گروه‌های AD و تخصیص‌های دستی.
/// </summary>
public sealed class AuthorizationResolutionService(
    AuthorizationSettingsService settingsService)
{
    /// <summary>
    /// نقش‌های گروهی و دستی را ادغام و مجوزهای صریح Deny را بر Grant مقدم می‌کند.
    /// </summary>
    public async Task<AuthorizationResolution> ResolveAsync(
        DirectoryUserProfile user,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var configuration = await settingsService.GetAsync(cancellationToken);
        var rolesByKey = configuration.Roles.ToDictionary(
            role => role.Key,
            StringComparer.OrdinalIgnoreCase);

        var userGroups = user.AllowedGroups
            .Select(group => group.DistinguishedName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var roleKeys = configuration.GroupMappings
            .Where(mapping => userGroups.Contains(mapping.GroupDistinguishedName))
            .Select(mapping => mapping.RoleKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var manualRoles = configuration.ManualUserRoles
            .Where(mapping => string.Equals(
                mapping.Username,
                user.Username,
                StringComparison.OrdinalIgnoreCase))
            .SelectMany(mapping => mapping.RoleKeys);

        roleKeys.UnionWith(manualRoles);

        // اگر هیچ نگاشتی وجود نداشته باشد، دسترسی فقط خواندنی پایه اعمال می‌شود.
        if (roleKeys.Count == 0)
        {
            roleKeys.Add("Viewer");
        }

        var granted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var denied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var roleKey in roleKeys)
        {
            if (!rolesByKey.TryGetValue(roleKey, out var role))
            {
                continue;
            }

            foreach (var permission in role.Permissions)
            {
                var target = permission.Effect == PermissionEffect.Deny
                    ? denied
                    : granted;

                target.Add(permission.Permission);
            }
        }

        // مجوزهای مستقیم کاربر با مجوزهای نقش‌ها ترکیب می‌شوند؛ Deny همچنان اولویت دارد.
        var directPermissions = configuration.ManualUserPermissions
            .Where(mapping => string.Equals(
                mapping.Username,
                user.Username,
                StringComparison.OrdinalIgnoreCase))
            .SelectMany(mapping => mapping.Permissions);

        foreach (var permission in directPermissions)
        {
            var target = permission.Effect == PermissionEffect.Deny
                ? denied
                : granted;

            target.Add(permission.Permission);
        }

        return new AuthorizationResolution
        {
            Roles = roleKeys,
            GrantedPermissions = granted,
            DeniedPermissions = denied
        };
    }
}
