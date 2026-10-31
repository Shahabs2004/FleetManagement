using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace FleetManagement.Web.Security;

/// <summary>
/// ساخت سیاست‌های مجوز در زمان اجرا بدون نیاز به ثبت تک‌تک عملیات در Program.
/// </summary>
public sealed class FleetAuthorizationPolicyProvider(
    IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    private const string PermissionPolicyPrefix = "Permission:";

    /// <summary>
    /// سیاست‌های Permission را به شرط مجوز متناظر تبدیل می‌کند.
    /// سایر سیاست‌ها به ارائه‌دهنده پیش‌فرض سپرده می‌شوند.
    /// </summary>
    public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(PermissionPolicyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return base.GetPolicyAsync(policyName);
        }

        var permission = policyName[PermissionPolicyPrefix.Length..];

        if (string.IsNullOrWhiteSpace(permission))
        {
            return Task.FromResult<AuthorizationPolicy?>(null);
        }

        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new FleetPermissionRequirement(permission))
            .Build();

        return Task.FromResult<AuthorizationPolicy?>(policy);
    }
}
