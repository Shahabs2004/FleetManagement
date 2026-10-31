using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// سرویس پس‌زمینه برای بستن نشست‌های منقضی‌شده و ثبت علت پایان آن‌ها.
/// </summary>
public sealed class SessionExpiryHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<SessionExpiryHostedService> logger) : BackgroundService
{
    /// <summary>
    /// هر دقیقه نشست‌های عبورکرده از مهلت بیکاری یا عمر مطلق را بررسی می‌کند.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var availability = scope.ServiceProvider
                    .GetRequiredService<SqlAvailabilityService>();

                // در قطعی SQL، سرویس پس‌زمینه منتظر بازیابی می‌ماند و خطای تکراری تولید نمی‌کند.
                if (await availability.IsAvailableAsync())
                {
                    var sessionService = scope.ServiceProvider
                        .GetRequiredService<UserSessionService>();

                    await sessionService.ExpireStaleSessionsAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "خطا در بررسی انقضای نشست‌های کاربری.");
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
