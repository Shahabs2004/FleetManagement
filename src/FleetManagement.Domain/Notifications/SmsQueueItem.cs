using FleetManagement.Domain.Common;

namespace FleetManagement.Domain.Notifications;

/// <summary>
/// رکورد صف و سابقه دائمی یک پیامک برای یک گیرنده.
/// </summary>
public sealed class SmsQueueItem : Entity
{
    private SmsQueueItem()
    {
        RecipientName = string.Empty;
        RecipientPhone = string.Empty;
        Body = string.Empty;
    }

    /// <summary>نوع رویداد پیامکی.</summary>
    public SmsEventType EventType { get; private set; }

    /// <summary>نام گیرنده.</summary>
    public string RecipientName { get; private set; }

    /// <summary>شماره همراه گیرنده.</summary>
    public string RecipientPhone { get; private set; }

    /// <summary>متن نهایی پیامک.</summary>
    public string Body { get; private set; }

    /// <summary>شناسه مأموریت مرتبط.</summary>
    public Guid MissionId { get; private set; }

    /// <summary>زمان برنامه‌ریزی ارسال.</summary>
    public DateTimeOffset ScheduledAtUtc { get; private set; }

    /// <summary>زمان ایجاد رکورد.</summary>
    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>وضعیت ارسال یا تحویل.</summary>
    public SmsQueueStatus Status { get; private set; }

    /// <summary>تعداد تلاش‌های ارسال.</summary>
    public int AttemptCount { get; private set; }

    /// <summary>حداکثر تلاش مجدد هنگام ایجاد.</summary>
    public int MaximumRetryCount { get; private set; }

    /// <summary>فاصله تلاش مجدد هنگام ایجاد.</summary>
    public int RetryIntervalSeconds { get; private set; }

    /// <summary>زمان تلاش بعدی.</summary>
    public DateTimeOffset? NextAttemptAtUtc { get; private set; }

    /// <summary>زمان آخرین تلاش ارسال.</summary>
    public DateTimeOffset? LastAttemptAtUtc { get; private set; }

    /// <summary>زمان پذیرش توسط ارائه‌دهنده.</summary>
    public DateTimeOffset? AcceptedAtUtc { get; private set; }

    /// <summary>زمان تأیید تحویل.</summary>
    public DateTimeOffset? DeliveredAtUtc { get; private set; }

    /// <summary>شناسه پیام نزد ارائه‌دهنده.</summary>
    public string? ProviderMessageId { get; private set; }

    /// <summary>وضعیت خام تحویل از ارائه‌دهنده.</summary>
    public string? ProviderDeliveryStatus { get; private set; }

    /// <summary>آخرین علت خطا.</summary>
    public string? ErrorReason { get; private set; }

    /// <summary>تعداد دفعات استعلام وضعیت تحویل.</summary>
    public int StatusPollCount { get; private set; }

    /// <summary>حداکثر دفعات استعلام وضعیت هنگام ایجاد.</summary>
    public int MaximumStatusPollingAttempts { get; private set; }

    /// <summary>زمان استعلام بعدی تحویل.</summary>
    public DateTimeOffset? NextStatusPollAtUtc { get; private set; }

    /// <summary>ساخت رکورد صف برای یک گیرنده.</summary>
    public SmsQueueItem(
        SmsEventType eventType,
        string recipientName,
        string recipientPhone,
        string body,
        Guid missionId,
        DateTimeOffset scheduledAtUtc,
        int maximumRetryCount,
        int retryIntervalSeconds,
        int maximumStatusPollingAttempts)
    {
        if (!Enum.IsDefined(eventType)) throw new ArgumentOutOfRangeException(nameof(eventType));
        if (missionId == Guid.Empty) throw new ArgumentException("شناسه مأموریت معتبر نیست.", nameof(missionId));

        EventType = eventType;
        RecipientName = Required(recipientName, 200, nameof(recipientName));
        RecipientPhone = Required(recipientPhone, 20, nameof(recipientPhone));
        Body = Required(body, 2000, nameof(body));
        MissionId = missionId;
        ScheduledAtUtc = scheduledAtUtc;
        CreatedAtUtc = DateTimeOffset.UtcNow;
        Status = SmsQueueStatus.Queued;
        MaximumRetryCount = Math.Clamp(maximumRetryCount, 0, 20);
        RetryIntervalSeconds = Math.Clamp(retryIntervalSeconds, 5, 86400);
        MaximumStatusPollingAttempts = Math.Clamp(maximumStatusPollingAttempts, 0, 100);
    }

    /// <summary>شروع یک تلاش ارسال.</summary>
    public void MarkSending(DateTimeOffset nowUtc)
    {
        if (Status is not (SmsQueueStatus.Queued or SmsQueueStatus.RetryScheduled))
        {
            throw new InvalidOperationException("وضعیت فعلی اجازه ارسال مجدد نمی‌دهد.");
        }

        Status = SmsQueueStatus.Sending;
        AttemptCount++;
        LastAttemptAtUtc = nowUtc;
        NextAttemptAtUtc = null;
        ErrorReason = null;
    }

    /// <summary>ثبت پذیرش پیام توسط ارائه‌دهنده بدون ادعای تحویل نهایی.</summary>
    public void MarkAccepted(string providerMessageId, DateTimeOffset nowUtc, int pollingIntervalSeconds)
    {
        if (Status != SmsQueueStatus.Sending)
        {
            throw new InvalidOperationException("پیام در وضعیت ارسال نیست.");
        }

        ProviderMessageId = Required(providerMessageId, 100, nameof(providerMessageId));
        AcceptedAtUtc = nowUtc;
        Status = SmsQueueStatus.Accepted;
        NextStatusPollAtUtc = MaximumStatusPollingAttempts > 0
            ? nowUtc.AddSeconds(Math.Clamp(pollingIntervalSeconds, 5, 86400))
            : null;
    }

    /// <summary>ثبت خطای موقت و زمان‌بندی تلاش مجدد.</summary>
    public void MarkTransientFailure(string error, DateTimeOffset nowUtc)
    {
        if (Status != SmsQueueStatus.Sending)
        {
            throw new InvalidOperationException("فقط پیام در حال ارسال قابل ثبت خطای موقت است.");
        }

        ErrorReason = Required(error, 1000, nameof(error));
        if (AttemptCount <= MaximumRetryCount)
        {
            Status = SmsQueueStatus.RetryScheduled;
            NextAttemptAtUtc = nowUtc.AddSeconds(RetryIntervalSeconds);
        }
        else
        {
            Status = SmsQueueStatus.Failed;
            NextAttemptAtUtc = null;
        }
    }

    /// <summary>ثبت خطای دائمی بدون تلاش مجدد خودکار.</summary>
    public void MarkPermanentFailure(string error, DateTimeOffset nowUtc)
    {
        if (Status != SmsQueueStatus.Sending)
        {
            throw new InvalidOperationException("فقط پیام در حال ارسال قابل ثبت خطای دائمی است.");
        }

        ErrorReason = Required(error, 1000, nameof(error));
        Status = SmsQueueStatus.PermanentFailure;
        NextAttemptAtUtc = null;
        LastAttemptAtUtc ??= nowUtc;
    }

    /// <summary>ثبت وضعیت خام تحویل ارائه‌دهنده.</summary>
    public void RecordDeliveryStatus(
        string providerStatus,
        bool delivered,
        DateTimeOffset nowUtc,
        int pollingIntervalSeconds)
    {
        if (Status != SmsQueueStatus.Accepted)
        {
            throw new InvalidOperationException("استعلام تحویل فقط پس از پذیرش پیام امکان‌پذیر است.");
        }

        ProviderDeliveryStatus = Required(providerStatus, 100, nameof(providerStatus));
        StatusPollCount++;

        if (delivered)
        {
            Status = SmsQueueStatus.Delivered;
            DeliveredAtUtc = nowUtc;
            NextStatusPollAtUtc = null;
            return;
        }

        NextStatusPollAtUtc = StatusPollCount < MaximumStatusPollingAttempts
            ? nowUtc.AddSeconds(Math.Clamp(pollingIntervalSeconds, 5, 86400))
            : null;
    }

    /// <summary>تلاش مجدد دستی پس از اصلاح علت خطا.</summary>
    public void RetryManually(DateTimeOffset nowUtc, string correctionReason)
    {
        if (Status is not (SmsQueueStatus.Failed or SmsQueueStatus.PermanentFailure))
        {
            throw new InvalidOperationException("ارسال دستی فقط برای پیام ناموفق مجاز است.");
        }

        _ = Required(correctionReason, 1000, nameof(correctionReason));
        Status = SmsQueueStatus.Queued;
        NextAttemptAtUtc = nowUtc;
        ErrorReason = null;
    }

    /// <summary>لغو یادآوری ارسال‌نشده پس از تغییر یا لغو زمان مأموریت.</summary>
    public void CancelScheduledReminder(string reason)
    {
        if (EventType != SmsEventType.MissionStartReminder
            || Status is not (SmsQueueStatus.Queued or SmsQueueStatus.RetryScheduled))
        {
            throw new InvalidOperationException("فقط یادآوری زمان‌بندی‌شده و ارسال‌نشده قابل لغو است.");
        }

        Status = SmsQueueStatus.Cancelled;
        NextAttemptAtUtc = null;
        ErrorReason = Required(reason, 1000, nameof(reason));
    }

    private static string Required(string value, int maxLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maxLength) throw new ArgumentOutOfRangeException(parameterName);
        return normalized;
    }
}
