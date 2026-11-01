using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace FleetManagement.Web.Security;

/// <summary>
/// ثبت احراز هویت مبتنی بر کوکی و سیاست‌های نشست سامانه.
/// </summary>
public static class FleetAuthenticationExtensions
{
    /// <summary>
    /// احراز هویت کوکی را ثبت می‌کند؛ کنترل مهلت نشست به سیاست ذخیره‌شده در SQL واگذار می‌شود.
    /// </summary>
    public static IServiceCollection AddFleetAuthentication(this IServiceCollection services)
    {
        services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = ".FleetManagement.Authentication";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.LoginPath = "/login";
                options.AccessDeniedPath = "/login?error=denied";
                // بیشترین مهلت بیکاری قابل تنظیم دو ساعت است؛ middleware مهلت دقیق را از SQL کنترل می‌کند.
                options.ExpireTimeSpan = TimeSpan.FromHours(2);
                options.SlidingExpiration = true;

                // اعتبار واقعی نشست با شناسه ثبت‌شده در SQL و سیاست جاری بررسی می‌شود.
                options.Events.OnValidatePrincipal = async context =>
                {
                    var rawSessionId = context.Principal?.FindFirstValue("fleet:session-id");
                    if (!Guid.TryParse(rawSessionId, out _))
                    {
                        context.RejectPrincipal();
                        await context.HttpContext.SignOutAsync(
                            CookieAuthenticationDefaults.AuthenticationScheme);
                    }
                };
            });

        services.AddAuthorization();
        services.AddSingleton<IAuthorizationPolicyProvider, FleetAuthorizationPolicyProvider>();
        services.AddSingleton<IAuthorizationHandler, FleetPermissionAuthorizationHandler>();
        services.AddCascadingAuthenticationState();
        services.AddSingleton<SessionActivityTracker>();
        services.AddHttpContextAccessor();
        services.AddAntiforgery();

        return services;
    }
}
