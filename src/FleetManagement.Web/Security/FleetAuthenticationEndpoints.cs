using System.Security.Claims;
using FleetManagement.Infrastructure.Configuration;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Server.IISIntegration;

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
        endpoints.MapPost("/auth/ad", SignInWithDirectoryPasswordAsync);
        endpoints.MapPost("/auth/local-admin", SignInWithLocalAdministratorAsync);
        endpoints.MapPost("/auth/windows", SignInWithWindowsAsync);
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
                return Results.Redirect($"/login?error={MapError(result.Status)}");
            }

            var provisionedUser = await userProvisioning.SynchronizeAsync(
                result.User,
                context.RequestAborted);

            if (!provisionedUser.CanSignIn)
            {
                return Results.Redirect("/login?error=app-disabled");
            }

            await IssueApplicationCookieAsync(context, result.User, provisionedUser.UserId);
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
        BootstrapSetupService setupService)
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
                return Results.Redirect("/login?error=local-admin-invalid");
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, "local-emergency-administrator"),
                new(ClaimTypes.Name, username),
                new("fleet:username", username),
                new("fleet:auth-source", "local-emergency"),
                new("fleet:session-start", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
                new(ClaimTypes.Role, "SystemAdministrator")
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
                    IssuedUtc = DateTimeOffset.UtcNow,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30)
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
            try
            {
                await context.ChallengeAsync(IISDefaults.AuthenticationScheme);
                return Results.Empty;
            }
            catch (InvalidOperationException)
            {
                return Results.Redirect("/login?error=windows-not-configured");
            }
        }

        try
        {
            var result = await authenticator.AuthenticateWindowsIdentityAsync(
                identity.Name ?? string.Empty,
                context.RequestAborted);

            if (!result.Succeeded || result.User is null)
            {
                return Results.Redirect($"/login?error={MapError(result.Status)}");
            }

            var provisionedUser = await userProvisioning.SynchronizeAsync(
                result.User,
                context.RequestAborted);

            if (!provisionedUser.CanSignIn)
            {
                return Results.Redirect("/login?error=app-disabled");
            }

            await IssueApplicationCookieAsync(context, result.User, provisionedUser.UserId);
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
        Guid applicationUserId)
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
            new("fleet:organizational-unit", user.OrganizationalUnit),
            new("fleet:directory-dn", user.DistinguishedName),
            new("fleet:session-start", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
        };

        foreach (var group in user.AllowedGroups)
        {
            claims.Add(new Claim("fleet:ad-group", group.DistinguishedName));
            claims.Add(new Claim("fleet:ad-group-name", group.Name));
        }

        // تا زمان تعریف نگاشت نقش‌های سازمانی، سطح دسترسی اولیه فقط مشاهده است.
        claims.Add(new Claim(ClaimTypes.Role, "Viewer"));

        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme);

        var principal = new ClaimsPrincipal(identity);
        var properties = new AuthenticationProperties
        {
            IsPersistent = false,
            AllowRefresh = true,
            IssuedUtc = DateTimeOffset.UtcNow,
            ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30)
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
        IAntiforgery antiforgery)
    {
        if (!await ValidateAntiforgeryAsync(context, antiforgery))
        {
            return Results.BadRequest("درخواست معتبر نیست.");
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
