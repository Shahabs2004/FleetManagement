namespace FleetManagement.Domain.Notifications;

/// <summary>
/// وضعیت ارسال و تحویل پیامک.
/// </summary>
public enum SmsQueueStatus
{
    /// <summary>در صف ارسال.</summary>
    Queued = 1,

    /// <summary>در حال ارسال به ارائه‌دهنده.</summary>
    Sending = 2,

    /// <summary>ارائه‌دهنده پیام را پذیرفته است؛ تحویل نهایی هنوز تأیید نشده.</summary>
    Accepted = 3,

    /// <summary>تحویل نهایی تأیید شده است.</summary>
    Delivered = 4,

    /// <summary>خطای موقت و زمان‌بندی مجدد.</summary>
    RetryScheduled = 5,

    /// <summary>خطای دائمی یا پایان تلاش‌های مجدد.</summary>
    Failed = 6,

    /// <summary>خطای دائمی ارائه‌دهنده.</summary>
    PermanentFailure = 7
}
