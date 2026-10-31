using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FleetManagement.Infrastructure;

/// <summary>
/// ثبت سرویس‌های زیرساختی سامانه.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// سرویس‌های زیرساختی را به ظرف تزریق وابستگی اضافه می‌کند.
    /// </summary>
    public static IServiceCollection AddFleetInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // در اجرای نخست ممکن است هنوز SQL تنظیم نشده باشد؛ در این حالت سایت باید بالا بیاید
        // تا مدیر محلی بتواند ویزارد راه‌اندازی را تکمیل کند.
        var connectionString = configuration.GetConnectionString("FleetManagement");

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContext<FleetDbContext>(options =>
                options.UseSqlServer(connectionString));
        }

        return services;
    }
}
