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
public static class FleetAuthenticationEndpoints
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
