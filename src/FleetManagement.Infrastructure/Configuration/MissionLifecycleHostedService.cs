using FleetManagement.Infrastructure.Bootstrap;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// زمان‌بند سبک برای شروع و پایان خودکار مأموریت‌ها.
/// </summary>
public sealed class MissionLifecycleHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<MissionLifecycleHostedService> logger) : BackgroundService
{
    /// <summary>بررسی مأموریت‌های سررسیدشده در بازه یک دقیقه‌ای.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var availability = scope.ServiceProvider.GetRequiredService<SqlAvailabilityService>();
                if (await availability.IsAvailableAsync())
                {
                    var missionService = scope.ServiceProvider.GetRequiredService<MissionManagementService>();
                    await missionService.ProcessDueMissionsAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "خطا در اجرای چرخه خودکار مأموریت‌ها.");
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                {
                    break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
