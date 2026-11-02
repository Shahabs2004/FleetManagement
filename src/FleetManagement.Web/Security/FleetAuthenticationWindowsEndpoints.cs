using FleetManagement.Infrastructure.Configuration;
using FleetManagement.Domain.Sessions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace FleetManagement.Web.Security;

/// <summary>
/// بخشی از مسیرهای احراز هویت برای جداسازی مسئولیت‌ها.
/// </summary>
public static partial class FleetAuthenticationEndpoints
{
        /// <summary>
        /// ورود با هویت Windows که IIS قبلاً احراز کرده است.
        /// </summary>
        private static async Task<IResult> SignInWithWindowsAsync(
            HttpContext context,
            IAntiforgery antiforgery,
            ActiveDirectoryAuthenticator authenticator,
            DirectoryUserProvisioningService userProvisioning,
            AuthorizationResolutionService authorizationResolver,
            UserSessionService sessionService,
            AuditLogService auditLog,
            ILoggerFactory loggerFactory)
        {
            if (!await ValidateAntiforgeryAsync(context, antiforgery))
            {
                return Results.BadRequest("درخواست معتبر نیست. صفحه ورود را دوباره باز کنید.");
            }
    
            var identity = context.User.Identity;
    
            // اگر درخواست هنوز ناشناس است، IIS باید مذاکره Windows Authentication را آغاز کند.
            if (identity?.IsAuthenticated != true
                || string.Equals(
                    identity.AuthenticationType,
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    StringComparison.OrdinalIgnoreCase))
            {
                // پاسخ 401 همراه با چالش Negotiate/NTLM باعث شروع مذاکره Windows Authentication در IIS می‌شود.
                context.Response.Headers.WWWAuthenticate = "Negotiate, NTLM";
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Results.Empty;
            }
    
            try
            {
                var result = await authenticator.AuthenticateWindowsIdentityAsync(
                    identity.Name ?? string.Empty,
                    context.RequestAborted);
    
                if (!result.Succeeded || result.User is null)
                {
                    await sessionService.RecordFailedLoginAsync(
                        identity.Name ?? string.Empty,
                        UserSessionLoginMethod.WindowsIntegrated,
                        result.Status.ToString(),
                        context.Connection.RemoteIpAddress?.ToString(),
                        context.RequestAborted);
    
                    return Results.Redirect($"/login?error={MapError(result.Status)}");
                }
    
                var provisionedUser = await userProvisioning.SynchronizeAsync(
                    result.User,
                    context.RequestAborted);
    
                if (!provisionedUser.CanSignIn)
                {
                    await sessionService.RecordFailedLoginAsync(
                        result.User.Username,
                        UserSessionLoginMethod.WindowsIntegrated,
                        "ApplicationDisabled",
                        context.Connection.RemoteIpAddress?.ToString(),
                        context.RequestAborted);
    
                    return Results.Redirect("/login?error=app-disabled");
                }
    
                var authorization = await authorizationResolver.ResolveAsync(
                    result.User,
                    context.RequestAborted);
    
                var sessionId = await sessionService.RecordSuccessfulLoginAsync(
                    result.User.Username,
                    UserSessionLoginMethod.WindowsIntegrated,
                    context.Connection.RemoteIpAddress?.ToString(),
                    context.RequestAborted);
    
                await auditLog.RecordAsync(result.User.Username, "LoginSucceeded", "Authentication", sessionId.ToString(), "Success", "WindowsIntegrated", clientIpAddress: context.Connection.RemoteIpAddress?.ToString(), cancellationToken: context.RequestAborted);
    
                await IssueApplicationCookieAsync(
                    context,
                    result.User,
                    provisionedUser.UserId,
                    authorization,
                    sessionId);
    
                return Results.Redirect("/");
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                var trackingId = Guid.NewGuid().ToString("N");
                loggerFactory.CreateLogger("FleetAuthentication")
                    .LogError(exception, "خطا در ورود یکپارچه ویندوز. شناسه پیگیری: {TrackingId}", trackingId);
    
                return Results.Redirect($"/login?error=unexpected&tracking={trackingId}");
            }
        }
}
