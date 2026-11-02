using FleetManagement.Infrastructure.Configuration;
using FleetManagement.Domain.Sessions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.RateLimiting;

namespace FleetManagement.Web.Security;

/// <summary>
/// بخشی از مسیرهای احراز هویت برای جداسازی مسئولیت‌ها.
/// </summary>
public static partial class FleetAuthenticationEndpoints
{
        /// <summary>
        /// ورود با نام کاربری و رمز عبور Active Directory.
        /// </summary>
        private static async Task<IResult> SignInWithDirectoryPasswordAsync(
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
    
            var form = await context.Request.ReadFormAsync();
            var username = form["username"].ToString().Trim();
            var password = form["password"].ToString();
    
            try
            {
                var result = await authenticator.AuthenticateWithPasswordAsync(
                    username,
                    password,
                    context.RequestAborted);
    
                if (!result.Succeeded || result.User is null)
                {
                    await sessionService.RecordFailedLoginAsync(
                        username,
                        UserSessionLoginMethod.DirectoryPassword,
                        result.Status.ToString(),
                        context.Connection.RemoteIpAddress?.ToString(),
                        context.RequestAborted);
    
                    await auditLog.RecordAsync(username, "LoginFailed", "Authentication", null, "Failed", result.Status.ToString(), clientIpAddress: context.Connection.RemoteIpAddress?.ToString(), cancellationToken: context.RequestAborted);
    
                    return Results.Redirect($"/login?error={MapError(result.Status)}");
                }
    
                var provisionedUser = await userProvisioning.SynchronizeAsync(
                    result.User,
                    context.RequestAborted);
    
                if (!provisionedUser.CanSignIn)
                {
                    await sessionService.RecordFailedLoginAsync(
                        username,
                        UserSessionLoginMethod.DirectoryPassword,
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
                    UserSessionLoginMethod.DirectoryPassword,
                    context.Connection.RemoteIpAddress?.ToString(),
                    context.RequestAborted);
    
                await auditLog.RecordAsync(result.User.Username, "LoginSucceeded", "Authentication", sessionId.ToString(), "Success", "DirectoryPassword", clientIpAddress: context.Connection.RemoteIpAddress?.ToString(), cancellationToken: context.RequestAborted);
    
                await IssueApplicationCookieAsync(
                    context,
                    result.User,
                    provisionedUser.UserId,
                    authorization,
                    sessionId);
                return Results.Redirect("/");
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                return Results.StatusCode(StatusCodes.Status499ClientClosedRequest);
            }
            catch (Exception exception)
            {
                var trackingId = Guid.NewGuid().ToString("N");
                loggerFactory.CreateLogger("FleetAuthentication")
                    .LogError(exception, "خطا در ورود AD. شناسه پیگیری: {TrackingId}", trackingId);
    
                return Results.Redirect($"/login?error=unexpected&tracking={trackingId}");
            }
            finally
            {
                // تلاش می‌شود ارجاع محلی رمز عبور پس از پایان پردازش آزاد شود.
                password = string.Empty;
            }
        }
}
