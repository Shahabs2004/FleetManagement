using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace FleetManagement.Web.Security;

/// <summary>
/// ثبت احراز هویت مبتنی بر کوکی و سیاست‌های نشست سامانه.
/// </summary>
public static class FleetAuthenticationExtensions
{
    /// <summary>
    /// احراز هویت کوکی با زمان بیکاری ۳۰ دقیقه و حداکثر عمر مطلق ۸ ساعت را ثبت می‌کند.
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
                options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
                options.SlidingExpiration = true;

                // نشست با تمدید فعالیت، از سقف عمر مطلق هشت ساعت عبور نمی‌کند.
                options.Events.OnValidatePrincipal = async context =>
                {
                    var sessionStart = context.Principal?.FindFirstValue("fleet:session-start");

                    if (!long.TryParse(sessionStart, out var startedAtUnix))
                    {
                        context.RejectPrincipal();
                        await context.HttpContext.SignOutAsync(
                            CookieAuthenticationDefaults.AuthenticationScheme);
                        return;
                    }

                    var startedAt = DateTimeOffset.FromUnixTimeSeconds(startedAtUnix);

                    if (DateTimeOffset.UtcNow - startedAt > TimeSpan.FromHours(8))
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
        services.AddHttpContextAccessor();
        services.AddAntiforgery();

        return services;
    }
}
