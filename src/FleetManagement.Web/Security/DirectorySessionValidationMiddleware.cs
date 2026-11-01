using System.Security.Claims;
using FleetManagement.Domain.Sessions;
using FleetManagement.Infrastructure.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace FleetManagement.Web.Security;

/// <summary>
/// بازبینی دوره‌ای حساب‌های AD در نشست‌های فعال و اعمال تغییر عضویت گروه‌ها.
/// </summary>
public sealed class DirectorySessionValidationMiddleware(RequestDelegate next)
{
    /// <summary>اجرای بازبینی AD پیش از دسترسی به صفحات محافظت‌شده.</summary>
    public async Task InvokeAsync(
        HttpContext context,
        ActiveDirectoryAuthenticator authenticator,
        DirectoryUserProvisioningService userProvisioning,
        AuthorizationResolutionService authorizationResolver,
        UserSessionService sessionService,
        DirectorySessionValidationTracker tracker,
        AuditLogService auditLog,
        ILogger<DirectorySessionValidationMiddleware> logger)
    {
        var principal = context.User;
        if (principal.Identity?.IsAuthenticated != true
            || !string.Equals(
                principal.FindFirstValue("fleet:auth-source"),
                "directory",
                StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var username = principal.FindFirstValue("fleet:username")
            ?? principal.Identity.Name
            ?? string.Empty;

        if (string.IsNullOrWhiteSpace(username)
            || !tracker.TryBegin(username, DateTimeOffset.UtcNow))
        {
            await next(context);
            return;
        }

        try
        {
            var result = await authenticator.ValidateExistingUserAsync(
                username,
                context.RequestAborted);

            if (result.Status is DirectoryAuthenticationStatus.DirectoryUnavailable
                or DirectoryAuthenticationStatus.NotConfigured)
            {
                // هنگام قطعی AD نشست فعلی تا زمان انقضای خودش معتبر می‌ماند.
                tracker.RetrySoon(username, DateTimeOffset.UtcNow);
                await next(context);
                return;
            }

            if (!result.Succeeded || result.User is null)
            {
                await RevokeSessionAsync(
                    context,
                    username,
                    result.Status.ToString(),
                    sessionService,
                    tracker,
                    auditLog);
                return;
            }

            var provisioned = await userProvisioning.SynchronizeAsync(
                result.User,
                context.RequestAborted);

            if (!provisioned.CanSignIn)
            {
                await RevokeSessionAsync(
                    context,
                    username,
                    "ApplicationDisabled",
                    sessionService,
                    tracker,
                    auditLog);
                return;
            }

            var authorization = await authorizationResolver.ResolveAsync(
                result.User,
                context.RequestAborted);

            var sessionIdValue = principal.FindFirstValue("fleet:session-id");
            if (!Guid.TryParse(sessionIdValue, out var sessionId))
            {
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                tracker.Forget(username);
                context.Response.Redirect("/login?error=session-invalid");
                return;
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, result.User.Username),
                new(ClaimTypes.Name, string.IsNullOrWhiteSpace(result.User.DisplayName)
                    ? result.User.Username
                    : result.User.DisplayName),
                new(ClaimTypes.GivenName, result.User.FirstName),
                new(ClaimTypes.Surname, result.User.LastName),
                new("fleet:user-id", provisioned.UserId.ToString()),
                new("fleet:username", result.User.Username),
                new("fleet:display-name", result.User.DisplayName),
                new("fleet:organizational-unit", result.User.OrganizationalUnit),
                new("fleet:directory-dn", result.User.DistinguishedName),
                new("fleet:auth-source", "directory"),
                new("fleet:session-start", principal.FindFirstValue("fleet:session-start") ?? string.Empty),
                new("fleet:session-id", sessionId.ToString())
            };

            foreach (var group in result.User.AllowedGroups)
            {
                claims.Add(new Claim("fleet:ad-group", group.DistinguishedName));
                claims.Add(new Claim("fleet:ad-group-name", group.Name));
            }

            claims.AddRange(authorization.Roles.Select(role =>
                new Claim(ClaimTypes.Role, role)));
            claims.AddRange(authorization.GrantedPermissions.Select(permission =>
                new Claim("fleet:permission", permission)));
            claims.AddRange(authorization.DeniedPermissions.Select(permission =>
                new Claim("fleet:permission-deny", permission)));

            var refreshedPrincipal = new ClaimsPrincipal(
                new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

            await context.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                refreshedPrincipal,
                new AuthenticationProperties
                {
                    IsPersistent = false,
                    AllowRefresh = true,
                    IssuedUtc = DateTimeOffset.UtcNow
                });

            context.User = refreshedPrincipal;
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            tracker.RetrySoon(username, DateTimeOffset.UtcNow);
            logger.LogWarning(
                exception,
                "بازبینی نشست AD برای {Username} انجام نشد؛ نشست تا انقضای عادی ادامه می‌یابد.",
                username);
        }

        await next(context);
    }

    /// <summary>خاتمه نشست در صورت غیرفعال‌شدن حساب یا حذف عضویت مجاز AD.</summary>
    private static async Task RevokeSessionAsync(
        HttpContext context,
        string username,
        string reason,
        UserSessionService sessionService,
        DirectorySessionValidationTracker tracker,
        AuditLogService auditLog)
    {
        var sessionIdValue = context.User.FindFirstValue("fleet:session-id");
        if (Guid.TryParse(sessionIdValue, out var sessionId))
        {
            await sessionService.EndSessionAsync(
                sessionId,
                UserSessionStatus.DirectoryRevoked,
                context.RequestAborted);
        }

        await auditLog.RecordAsync(
            username,
            "DirectorySessionRevoked",
            "Authentication",
            sessionIdValue,
            "Success",
            reason,
            clientIpAddress: context.Connection.RemoteIpAddress?.ToString(),
            cancellationToken: context.RequestAborted);

        tracker.Forget(username);
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        context.Response.Redirect($"/login?error=directory-revoked");
    }
}
