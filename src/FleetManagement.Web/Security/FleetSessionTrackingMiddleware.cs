using System.Security.Claims;
using FleetManagement.Infrastructure.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace FleetManagement.Web.Security;

/// <summary>
/// ثبت دوره‌ای فعالیت نشست کاربر برای محاسبه زمان بیکاری.
/// </summary>
public sealed class FleetSessionTrackingMiddleware(
    RequestDelegate next)
{
    /// <summary>
    /// در صورت احراز هویت، آخرین فعالیت نشست را با فاصله کنترل‌شده ثبت می‌کند.
    /// </summary>
    public async Task InvokeAsync(
        HttpContext context,
        SessionActivityTracker activityTracker,
        UserSessionService sessionService,
        ILogger<FleetSessionTrackingMiddleware> logger)
    {
        var rawSessionId = context.User.FindFirstValue("fleet:session-id");

        if (context.User.Identity?.IsAuthenticated == true
            && Guid.TryParse(rawSessionId, out var sessionId)
            && activityTracker.ShouldTouch(sessionId))
        {
            try
            {
                var sessionIsActive = await sessionService.TouchAsync(sessionId, context.RequestAborted);
                if (!sessionIsActive)
                {
                    activityTracker.Forget(sessionId);
                    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    context.Response.Redirect("/login?error=session-expired");
                    return;
                }
            }
            catch (Exception exception)
            {
                // خرابی ثبت سابقه نباید درخواست عملیاتی کاربر را متوقف کند.
                logger.LogWarning(exception, "ثبت آخرین فعالیت نشست کاربر انجام نشد.");
            }
        }

        await next(context);
    }
}
