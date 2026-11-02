using FleetManagement.Infrastructure.Bootstrap;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FleetManagement.Infrastructure;

/// <summary>
/// ثبت سرویس‌های زیرساختی سامانه.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// سرویس‌های زیرساختی و راه‌اندازی را ثبت می‌کند.
    /// </summary>
    public static IServiceCollection AddFleetInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // در اجرای نخست اتصال SQL وجود ندارد؛ سایت باید برای نمایش ویزارد بالا بیاید.
        services.AddFleetBootstrap();

        return services;
    }
}
