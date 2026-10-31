using FleetManagement.Application.Configuration;
using FleetManagement.Infrastructure.Configuration;
using FleetManagement.Infrastructure.Persistence;
using FleetManagement.Infrastructure.Reporting;
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

        // تنظیمات عملیاتی و دایرکتوری در جدول SQL نگهداری می‌شوند.
        services.AddScoped<ISystemSettingsStore, SqlSystemSettingsStore>();
        services.AddScoped<ActiveDirectorySettingsService>();
        services.AddScoped<ActiveDirectoryAuthenticator>();
        services.AddScoped<DirectoryUserProvisioningService>();
        services.AddScoped<AuthorizationSettingsService>();
        services.AddScoped<AuthorizationResolutionService>();
        services.AddScoped<UserSessionService>();
        services.AddScoped<AuditLogService>();
        services.AddScoped<DriverManagementService>();
        services.AddScoped<VehicleManagementService>();
        services.AddScoped<MissionManagementService>();
        services.AddScoped<MissionOrderTemplateService>();
        services.AddHostedService<MissionLifecycleHostedService>();
        services.AddScoped<LocationManagementService>();
        services.AddScoped<NotificationService>();
        services.AddScoped<UserNotificationRecipientResolver>();
        services.AddScoped<MissionReportService>();
        services.AddScoped<MissionReportExportService>();
        services.AddScoped<UserManagementService>();
        services.AddHostedService<SessionExpiryHostedService>();
        services.AddScoped<OrganizationSettingsService>();
        services.AddScoped<SmsSettingsService>();
        services.AddSingleton<ActiveDirectoryConnectionTester>();

        return services;
    }
}
