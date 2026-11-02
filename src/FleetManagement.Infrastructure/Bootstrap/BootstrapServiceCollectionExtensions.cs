using FleetManagement.Application.Auditing;
using FleetManagement.Application.Configuration;
using FleetManagement.Application.Missions;
using FleetManagement.Application.Notifications;
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
        services.AddScoped<SessionPolicySettingsService>();
        services.AddScoped<AuditLogService>();
        services.AddScoped<IAuditLogWriter>(provider => provider.GetRequiredService<AuditLogService>());
        services.AddScoped<DriverManagementService>();
        services.AddScoped<DriverVehiclePreferenceService>();
        services.AddScoped<VehicleManagementService>();
        services.AddScoped<IMissionChangeManagementService, MissionChangeManagementService>();
        services.AddScoped<IMissionOdometerService, MissionOdometerService>();
        services.AddScoped<IMissionSplitService, MissionSplitService>();
        services.AddScoped<IMissionWithdrawalService, MissionWithdrawalService>();
        services.AddScoped<IMissionRequestStore, EfMissionRequestStore>();
        services.AddScoped<IMissionReviewStore, EfMissionReviewStore>();
        services.AddScoped<FleetManagement.Application.Missions.MissionRequestService>();
        services.AddScoped<MissionReviewService>();
        services.AddScoped<IMissionPendingRequestStore, EfMissionPendingRequestStore>();
        services.AddScoped<MissionPendingRequestService>();
        services.AddScoped<MissionRequestWorkflowService>();
        services.AddScoped<IMissionRequestService, MissionRequestWorkflowService>();
        services.AddScoped<IMissionLifecycleProcessorService, MissionLifecycleProcessorService>();
        services.AddScoped<IMissionQueryService, MissionQueryService>();
        services.AddScoped<IMissionExtensionService, MissionExtensionService>();
        services.AddScoped<IMissionRulesService, MissionRulesService>();
        services.AddScoped<MissionManagementService>();
        services.AddScoped<MissionOrderTemplateService>();
        services.AddHostedService<MissionLifecycleHostedService>();
        services.AddScoped<LocationManagementService>();
        services.AddScoped<NotificationService>();
        services.AddScoped<IMissionNotificationSender>(provider => provider.GetRequiredService<NotificationService>());
        services.AddScoped<IMissionSmsScheduler, MissionSmsScheduler>();
        services.AddScoped<UserNotificationRecipientResolver>();
        services.AddScoped<MissionReportService>();
        services.AddScoped<FleetDashboardService>();
        services.AddScoped<DashboardLayoutSettingsService>();
        services.AddScoped<MissionAvailabilityService>();
        services.AddScoped<MissionReportExportService>();
        services.AddScoped<UserManagementService>();
        services.AddHostedService<SessionExpiryHostedService>();
        services.AddScoped<OrganizationSettingsService>();
        services.AddScoped<UserAppearanceSettingsService>();
        services.AddScoped<SmsSettingsService>();
        services.AddScoped<SmsQueueService>();
        services.AddSingleton<AdpSmsSoapClient>(_ =>
            new AdpSmsSoapClient(new HttpClient { Timeout = TimeSpan.FromSeconds(30) }));
        services.AddHostedService<SmsDispatchHostedService>();
        services.AddSingleton<ActiveDirectoryConnectionTester>();

        return services;
    }
}
