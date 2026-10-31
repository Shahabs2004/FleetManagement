using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FleetManagement.Infrastructure.Bootstrap;

/// <summary>
/// ثبت سرویس‌های مربوط به راه‌اندازی اولیه و بازیابی اضطراری.
/// </summary>
public static class BootstrapServiceCollectionExtensions
{
    /// <summary>
    /// سرویس‌های bootstrap و کارخانه اتصال پایگاه داده را ثبت می‌کند.
    /// </summary>
    public static IServiceCollection AddFleetBootstrap(this IServiceCollection services)
    {
        services.AddSingleton<IBootstrapStateStore, DpapiBootstrapStateStore>();
        services.AddSingleton<BootstrapSetupService>();
        services.AddSingleton<SqlConnectionSetupService>();
        services.AddSingleton<SqlDatabaseInitializer>();
        services.AddSingleton<SqlAvailabilityService>();
        services.AddSingleton<BootstrapDbContextFactory>();

        // اتصال از فایل محلی محافظت‌شده خوانده می‌شود تا پس از تکمیل ویزارد نیاز به Restart نباشد.
        services.AddSingleton<IDbContextFactory<FleetDbContext>>(
            provider => provider.GetRequiredService<BootstrapDbContextFactory>());

        return services;
    }
}
