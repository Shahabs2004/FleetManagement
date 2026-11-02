using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace FleetManagement.Web.Security;

/// <summary>
/// بررسی مجوزهای Claimمحور با پشتیبانی از wildcard و اولویت Deny.
/// </summary>
public sealed class FleetPermissionAuthorizationHandler
    : AuthorizationHandler<FleetPermissionRequirement>
{
    /// <summary>
    /// ابتدا رد صریح و سپس اعطای مجوز دقیق یا wildcard را بررسی می‌کند.
    /// </summary>
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        FleetPermissionRequirement requirement)
    {
        var denied = context.User.FindAll("fleet:permission-deny")
            .Select(claim => claim.Value);

        if (denied.Any(permission => Matches(permission, requirement.Permission)))
        {
            return Task.CompletedTask;
        }

        var granted = context.User.FindAll("fleet:permission")
            .Select(claim => claim.Value);

        if (granted.Any(permission => Matches(permission, requirement.Permission)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// تطبیق مجوز دقیق، مجوز کلی یا wildcard سطح ماژول.
    /// </summary>
    private static bool Matches(string configuredPermission, string requestedPermission)
    {
        if (string.Equals(configuredPermission, "*", StringComparison.OrdinalIgnoreCase)
            || string.Equals(configuredPermission, requestedPermission, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!configuredPermission.EndsWith(".*", StringComparison.Ordinal))
        {
            return false;
        }

        var prefix = configuredPermission[..^1];

        return requestedPermission.StartsWith(
            prefix,
            StringComparison.OrdinalIgnoreCase);
    }
}
