using System.Security.Claims;
using FleetManagement.Infrastructure.Configuration;
using FleetManagement.Infrastructure.Bootstrap;
using FleetManagement.Domain.Sessions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;

namespace FleetManagement.Web.Security;

/// <summary>
/// مسیرهای HTTP ورود، خروج و اعتبارسنجی هویت Active Directory.
/// </summary>
public static partial class FleetAuthenticationEndpoints
{
    /// <summary>
    /// مسیرهای احراز هویت را به برنامه اضافه می‌کند.
    /// </summary>
    public static IEndpointRouteBuilder MapFleetAuthentication(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/auth/ad", SignInWithDirectoryPasswordAsync)
            .RequireRateLimiting("authentication");
        endpoints.MapPost("/auth/local-admin", SignInWithLocalAdministratorAsync)
            .RequireRateLimiting("authentication");
        endpoints.MapPost("/auth/windows", SignInWithWindowsAsync)
            .RequireRateLimiting("authentication");
        endpoints.MapPost("/auth/logout", SignOutAsync);

        return endpoints;
    }







    /// <summary>
    /// صدور کوکی نشست پس از احراز هویت موفق دایرکتوری.
    /// </summary>
    private static async Task IssueApplicationCookieAsync(
        HttpContext context,
        DirectoryUserProfile user,
        Guid applicationUserId,
        AuthorizationResolution authorization,
        Guid sessionId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Username),
            new("fleet:user-id", applicationUserId.ToString()),
            new(ClaimTypes.Name, string.IsNullOrWhiteSpace(user.DisplayName)
                ? user.Username
                : user.DisplayName),
            new(ClaimTypes.GivenName, user.FirstName),
            new(ClaimTypes.Surname, user.LastName),
            new("fleet:username", user.Username),
            new("fleet:auth-source", "directory"),
            new("fleet:organizational-unit", user.OrganizationalUnit),
            new("fleet:directory-dn", user.DistinguishedName),
            new("fleet:session-start", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
            new("fleet:session-id", sessionId.ToString())
        };

        foreach (var group in user.AllowedGroups)
        {
            claims.Add(new Claim("fleet:ad-group", group.DistinguishedName));
            claims.Add(new Claim("fleet:ad-group-name", group.Name));
        }

        // نقش‌ها و مجوزها از نگاشت گروه‌های AD و تخصیص‌های دستی دریافت می‌شوند.
        foreach (var role in authorization.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        foreach (var permission in authorization.GrantedPermissions)
        {
            claims.Add(new Claim("fleet:permission", permission));
        }

        foreach (var permission in authorization.DeniedPermissions)
        {
            claims.Add(new Claim("fleet:permission-deny", permission));
        }

        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme);

        var principal = new ClaimsPrincipal(identity);
        var properties = new AuthenticationProperties
        {
            IsPersistent = false,
            AllowRefresh = true,
            IssuedUtc = DateTimeOffset.UtcNow
        };

        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            properties);
    }

    /// <summary>
    /// خروج از حساب و حذف کوکی نشست.
    /// </summary>
    private static async Task<IResult> SignOutAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        UserSessionService sessionService,
        SessionActivityTracker activityTracker,
        AuditLogService auditLog)
    {
        if (!await ValidateAntiforgeryAsync(context, antiforgery))
        {
            return Results.BadRequest("درخواست معتبر نیست.");
        }

        var rawSessionId = context.User.FindFirstValue("fleet:session-id");
        if (Guid.TryParse(rawSessionId, out var sessionId))
        {
            await sessionService.EndSessionAsync(
                sessionId,
                UserSessionStatus.LoggedOut,
                context.RequestAborted);

            activityTracker.Forget(sessionId);
            await auditLog.RecordAsync(
                context.User.Identity?.Name ?? "unknown",
                "Logout",
                "Authentication",
                sessionId.ToString(),
                "Success",
                clientIpAddress: context.Connection.RemoteIpAddress?.ToString(),
                cancellationToken: context.RequestAborted);
        }

        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.Redirect("/login");
    }

    /// <summary>
    /// بررسی توکن ضد جعل درخواست‌های فرم.
    /// </summary>
    private static async Task<bool> ValidateAntiforgeryAsync(
        HttpContext context,
        IAntiforgery antiforgery)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context);
            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }

    /// <summary>
    /// تبدیل وضعیت فنی احراز هویت به کد پیام فارسی در صفحه ورود.
    /// </summary>
    private static string MapError(DirectoryAuthenticationStatus status)
    {
        return status switch
        {
            DirectoryAuthenticationStatus.NotConfigured => "ad-not-configured",
            DirectoryAuthenticationStatus.InvalidCredentials => "invalid-credentials",
            DirectoryAuthenticationStatus.AccountDisabled => "account-disabled",
            DirectoryAuthenticationStatus.AccountLocked => "account-locked",
            DirectoryAuthenticationStatus.AccountExpired => "account-expired",
            DirectoryAuthenticationStatus.NotInAllowedGroup => "not-allowed",
            DirectoryAuthenticationStatus.DirectoryUnavailable => "directory-unavailable",
            DirectoryAuthenticationStatus.AuthenticationMethodDisabled => "method-disabled",
            _ => "login-failed"
        };
    }
}
