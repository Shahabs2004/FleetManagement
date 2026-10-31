using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// تعیین گیرندگان اعلان بر اساس نقش‌های دستی و گروه‌های AD ذخیره‌شده در پروفایل.
/// </summary>
public sealed class UserNotificationRecipientResolver(
    IDbContextFactory<FleetDbContext> dbContextFactory,
    AuthorizationSettingsService authorizationSettings)
{
    /// <summary>یافتن کاربران فعال دارای حداقل یکی از نقش‌های هدف.</summary>
    public async Task<IReadOnlyList<string>> ResolveAsync(
        IEnumerable<string> roleKeys,
        CancellationToken cancellationToken = default)
    {
        var targetRoles = roleKeys
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (targetRoles.Count == 0) return [];

        var configuration = await authorizationSettings.GetAsync(cancellationToken);
        var users = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using (users)
        {
            var profiles = await users.ApplicationUsers
                .AsNoTracking()
                .Where(user => !user.IsDisabled)
                .ToListAsync(cancellationToken);

            var recipients = new List<string>();
            foreach (var user in profiles)
            {
                var assignedRoles = configuration.ManualUserRoles
                    .Where(mapping => string.Equals(
                        mapping.Username,
                        user.Username,
                        StringComparison.OrdinalIgnoreCase))
                    .SelectMany(mapping => mapping.RoleKeys)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var groupDns = user.GetDirectoryGroupDistinguishedNames()
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var mapping in configuration.GroupMappings)
                {
                    if (groupDns.Contains(mapping.GroupDistinguishedName))
                    {
                        assignedRoles.Add(mapping.RoleKey);
                    }
                }

                if (assignedRoles.Overlaps(targetRoles))
                {
                    recipients.Add(user.Username);
                }
            }

            return recipients
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
