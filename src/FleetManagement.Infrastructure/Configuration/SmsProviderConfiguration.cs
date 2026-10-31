namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// اطلاعات اتصال و سیاست ارسال سرویس پیامک آتیه داده‌پرداز.
/// این مقادیر در SQL طبق تصمیم پروژه به‌صورت متن ساده نگهداری می‌شوند.
/// </summary>
public sealed class SmsProviderConfiguration
{
    /// <summary>نام سازمان در پنل ارائه‌دهنده.</summary>
    public string Organization { get; set; } = string.Empty;

    /// <summary>نام کاربری سرویس پیامک.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>رمز سرویس پیامک.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>شماره فرستنده ثابت.</summary>
    public string SourceNumber { get; set; } = string.Empty;

    /// <summary>تعداد تلاش مجدد برای خطاهای موقت.</summary>
    public int MaximumRetryCount { get; set; } = 3;

    /// <summary>فاصله تلاش مجدد بر حسب ثانیه.</summary>
    public int RetryIntervalSeconds { get; set; } = 60;

    /// <summary>فاصله بررسی وضعیت تحویل بر حسب ثانیه.</summary>
    public int StatusPollingIntervalSeconds { get; set; } = 60;

    /// <summary>حداکثر دفعات بررسی وضعیت تحویل.</summary>
    public int MaximumStatusPollingAttempts { get; set; } = 10;

    /// <summary>زمان یادآوری پیش از شروع مأموریت بر حسب دقیقه.</summary>
    public int ReminderLeadMinutes { get; set; } = 60;

    /// <summary>قالب‌های قابل تنظیم برای رویدادهای پیامکی.</summary>
    public List<SmsEventTemplateConfiguration> EventTemplates { get; set; } = [];
}
