using System.Security.Claims;
using FleetManagement.Infrastructure.Bootstrap;
using FleetManagement.Infrastructure.Configuration;
using FleetManagement.Domain.Sessions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace FleetManagement.Web.Security;

/// <summary>
/// بخشی از مسیرهای احراز هویت برای جداسازی مسئولیت‌ها.
/// </summary>
public static partial class FleetAuthenticationEndpoints
{
    /// <summary>
    /// ورود اضطراری مدیر محلی مستقل از Active Directory.
    /// </summary>
    private static async Task<IResult> SignInWithLocalAdministratorAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        IBootstrapStateStore stateStore,
        BootstrapSetupService setupService,
        UserSessionService sessionService,
        AuditLogService auditLog)
    {
        if (!await ValidateAntiforgeryAsync(context, antiforgery))
        {
            return Results.BadRequest("درخواست معتبر نیست. صفحه ورود را دوباره باز کنید.");
        }

        var state = await stateStore.ReadAsync(context.RequestAborted);
        if (!state.SetupCompleted)
        {
            return Results.Redirect("/setup");
        }

        var form = await context.Request.ReadFormAsync();
        var username = form["username"].ToString().Trim();
        var password = form["password"].ToString();

        try
        {
            var isValid = await setupService.VerifyLocalAdministratorAsync(
                username,
                password,
                context.RequestAborted);

            if (!isValid)
            {
                await sessionService.RecordFailedLoginAsync(
                    username,
                    UserSessionLoginMethod.LocalEmergencyAdministrator,
                    "InvalidLocalAdministratorCredentials",
                    context.Connection.RemoteIpAddress?.ToString(),
                    context.RequestAborted);

                await auditLog.RecordAsync(username, "LoginFailed", "Authentication", null, "Failed", "InvalidLocalAdministratorCredentials", clientIpAddress: context.Connection.RemoteIpAddress?.ToString(), cancellationToken: context.RequestAborted);

                return Results.Redirect("/login?error=local-admin-invalid");
            }

            var sessionId = await sessionService.RecordSuccessfulLoginAsync(
                username,
                UserSessionLoginMethod.LocalEmergencyAdministrator,
                context.Connection.RemoteIpAddress?.ToString(),
                context.RequestAborted);

            await auditLog.RecordAsync(username, "LoginSucceeded", "Authentication", sessionId.ToString(), "Success", "LocalEmergencyAdministrator", clientIpAddress: context.Connection.RemoteIpAddress?.ToString(), cancellationToken: context.RequestAborted);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, "local-emergency-administrator"),
                new(ClaimTypes.Name, username),
                new("fleet:username", username),
                new("fleet:auth-source", "local-emergency"),
                new("fleet:session-start", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
                new("fleet:session-id", sessionId.ToString()),
                new(ClaimTypes.Role, "SystemAdministrator"),
                new("fleet:permission", "*")
            };

            var principal = new ClaimsPrincipal(
                new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

            await context.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = false,
                    AllowRefresh = true,
                    IssuedUtc = DateTimeOffset.UtcNow
                });

            return Results.Redirect("/");
        }
        finally
        {
            password = string.Empty;
        }
    }
}
