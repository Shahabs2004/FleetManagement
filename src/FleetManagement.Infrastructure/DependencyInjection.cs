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
        // ثبت DbContext پس از تکمیل ویزارد و ذخیره تنظیم اتصال انجام می‌شود.
        // در این مرحله برنامه باید بدون اتصال SQL نیز بتواند صفحه راه‌اندازی را نمایش دهد.
        _ = configuration;

        return services;
    }
}
