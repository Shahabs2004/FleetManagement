using FleetManagement.Application.Notifications;
using FleetManagement.Domain.Missions;
using FleetManagement.Domain.Notifications;
using Microsoft.Extensions.Logging;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// آداپتور صف‌بندی پیامک مأموریت؛ خطای پیامک نباید تراکنش اصلی را ناموفق کند.
/// </summary>
public sealed class MissionSmsScheduler(
    SmsQueueService smsQueueService,
    ILogger<MissionSmsScheduler> logger) : IMissionSmsScheduler
{
    /// <inheritdoc />
    public async Task QueueSafelyAsync(
        Mission mission,
        SmsEventType eventType,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await smsQueueService.EnqueueForMissionAsync(
                mission,
                eventType,
                reason,
                cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "صف‌بندی پیامک رویداد {EventType} برای مأموریت {MissionNumber} انجام نشد.",
                eventType,
                mission.MissionNumber);
        }
    }
    /// <inheritdoc />
    public async Task CancelUnsentRemindersSafelyAsync(
        Guid missionId,
        string reason,
        string actor,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await smsQueueService.CancelUnsentRemindersAsync(
                missionId,
                reason,
                actor,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "لغو پیامک‌های یادآوری ارسال‌نشده برای مأموریت {MissionId} انجام نشد.",
                missionId);
        }
    }
}
