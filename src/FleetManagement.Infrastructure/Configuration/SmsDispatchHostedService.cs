using FleetManagement.Domain.Notifications;
using FleetManagement.Infrastructure.Bootstrap;
using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>ارسال صف پیامک و استعلام وضعیت‌های پذیرفته‌شده.</summary>
public sealed class SmsDispatchHostedService(
    IServiceScopeFactory scopeFactory,
    IDbContextFactory<FleetDbContext> dbContextFactory,
    AdpSmsSoapClient client,
    ILogger<SmsDispatchHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var availability = scope.ServiceProvider.GetRequiredService<SqlAvailabilityService>();
                if (await availability.IsAvailableAsync(stoppingToken))
                {
                    var settings = scope.ServiceProvider.GetRequiredService<SmsSettingsService>();
                    var configuration = await settings.GetAsync(stoppingToken);
                    if (configuration is not null && !await settings.IsSkippedAsync(stoppingToken))
                    {
                        await SendDueAsync(configuration, stoppingToken);
                        await PollDueAsync(configuration, stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "خطا در پردازش صف پیامک.");
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task SendDueAsync(SmsProviderConfiguration configuration, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        List<Guid> ids;
        await using (var db = await dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            ids = await db.SmsQueueItems.AsNoTracking()
                .Where(item => (item.Status == SmsQueueStatus.Queued && item.ScheduledAtUtc <= now)
                    || (item.Status == SmsQueueStatus.RetryScheduled && item.NextAttemptAtUtc <= now))
                .OrderBy(item => item.ScheduledAtUtc)
                .Select(item => item.Id)
                .Take(20)
                .ToListAsync(cancellationToken);
        }

        foreach (var id in ids)
        {
            var claimedAt = DateTimeOffset.UtcNow;
            int claimed;
            await using (var claimDb = await dbContextFactory.CreateDbContextAsync(cancellationToken))
            {
                claimed = await claimDb.SmsQueueItems
                    .Where(item => item.Id == id
                        && ((item.Status == SmsQueueStatus.Queued && item.ScheduledAtUtc <= claimedAt)
                            || (item.Status == SmsQueueStatus.RetryScheduled && item.NextAttemptAtUtc <= claimedAt)))
                    .ExecuteUpdateAsync(update => update
                        .SetProperty(item => item.Status, SmsQueueStatus.Sending)
                        .SetProperty(item => item.AttemptCount, item => item.AttemptCount + 1)
                        .SetProperty(item => item.LastAttemptAtUtc, claimedAt)
                        .SetProperty(item => item.NextAttemptAtUtc, (DateTimeOffset?)null)
                        .SetProperty(item => item.ErrorReason, (string?)null), cancellationToken);
            }
            if (claimed == 0) continue;

            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var item = await db.SmsQueueItems.SingleAsync(row => row.Id == id, cancellationToken);
            try
            {
                var result = await client.SendAsync(configuration, item.RecipientPhone, item.Body, cancellationToken);
                if (!long.TryParse(result.Id, out var providerId) || providerId <= 0)
                    item.MarkPermanentFailure("ارائه‌دهنده پیامک شناسه معتبر برنگرداند.", DateTimeOffset.UtcNow);
                else
                    item.MarkAccepted(result.Id, DateTimeOffset.UtcNow, configuration.StatusPollingIntervalSeconds);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // پاسخ گمشده مبهم است؛ ارسال مجدد خودکار ممکن است پیامک تکراری بسازد.
                item.MarkPermanentFailure(
                    $"پاسخ ارسال نامشخص ({exception.GetType().Name})؛ بررسی دستی ارائه‌دهنده لازم است.",
                    DateTimeOffset.UtcNow);
            }

            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task PollDueAsync(SmsProviderConfiguration configuration, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        List<Guid> ids;
        await using (var db = await dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            ids = await db.SmsQueueItems.AsNoTracking()
                .Where(item => item.Status == SmsQueueStatus.Accepted && item.NextStatusPollAtUtc <= now)
                .OrderBy(item => item.NextStatusPollAtUtc)
                .Select(item => item.Id)
                .Take(250)
                .ToListAsync(cancellationToken);
        }

        foreach (var id in ids)
        {
            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var item = await db.SmsQueueItems.SingleOrDefaultAsync(row => row.Id == id
                && row.Status == SmsQueueStatus.Accepted
                && row.NextStatusPollAtUtc <= DateTimeOffset.UtcNow, cancellationToken);
            if (item is null) continue;

            if (!long.TryParse(item.ProviderMessageId, out var providerId) || providerId <= 0)
            {
                item.RecordDeliveryStatus("شناسه ارائه‌دهنده نامعتبر", false, DateTimeOffset.UtcNow,
                    configuration.StatusPollingIntervalSeconds);
                await db.SaveChangesAsync(cancellationToken);
                continue;
            }

            try
            {
                var statuses = await client.GetStatusesOriginalIdAsync(configuration, [providerId], cancellationToken);
                var result = statuses.FirstOrDefault(status => status.Id == item.ProviderMessageId);
                if (result is null || string.IsNullOrWhiteSpace(result.Status)) continue;

                item.RecordDeliveryStatus(result.Status, IsDeliveredStatus(result.Status), DateTimeOffset.UtcNow,
                    configuration.StatusPollingIntervalSeconds);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning("استعلام وضعیت پیامک {QueueItemId} ناموفق بود ({ErrorType}).",
                    item.Id, exception.GetType().Name);
                continue;
            }

            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public static bool IsDeliveredStatus(string status) =>
        string.Equals(status.Trim(), "Delivered", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status.Trim(), "DELIVRD", StringComparison.OrdinalIgnoreCase);
}
