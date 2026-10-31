using Microsoft.Extensions.DependencyInjection;

namespace FleetManagement.Infrastructure.Bootstrap;

/// <summary>
/// ثبت سرویس‌های مربوط به راه‌اندازی اولیه و بازیابی اضطراری.
/// </summary>
public static class BootstrapServiceCollectionExtensions
{
    /// <summary>
    /// سرویس‌های bootstrap را ثبت می‌کند.
    /// </summary>
    public static IServiceCollection AddFleetBootstrap(this IServiceCollection services)
    {
        services.AddSingleton<IBootstrapStateStore, DpapiBootstrapStateStore>();
        services.AddSingleton<BootstrapSetupService>();
        services.AddSingleton<SqlConnectionSetupService>();
        services.AddSingleton<SqlDatabaseInitializer>();

        return services;
    }
}
