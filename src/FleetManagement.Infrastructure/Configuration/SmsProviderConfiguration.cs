using FleetManagement.Domain.Notifications;

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

    /// <summary>
    /// ساخت قالب‌های پیش‌فرض نه رویداد پیامکی مصوب.
    /// ارسال پیامک برای مأموریت تقسیم‌شده عمداً تعریف نشده است.
    /// </summary>
    public static SmsProviderConfiguration CreateDefault()
    {
        return new SmsProviderConfiguration
        {
            EventTemplates =
            [
                new() { EventType = SmsEventType.MissionApproved, Template = "مأموریت {MissionNumber} با عنوان {MissionTitle} تأیید شد. زمان شروع: {StartDateTime}" },
                new() { EventType = SmsEventType.MissionRejected, Template = "درخواست مأموریت {MissionNumber} رد شد. علت: {RejectionReason}" },
                new() { EventType = SmsEventType.MissionChanged, Template = "جزئیات مأموریت {MissionNumber} تغییر کرد. زمان جدید: {StartDateTime}" },
                new() { EventType = SmsEventType.DriverOrVehicleChanged, Template = "در مأموریت {MissionNumber} راننده یا خودرو تغییر کرده است." },
                new() { EventType = SmsEventType.MissionCancelled, Template = "مأموریت {MissionNumber} لغو شد. علت: {CancellationReason}" },
                new() { EventType = SmsEventType.WithdrawalApproved, Template = "درخواست انصراف از مأموریت {MissionNumber} تأیید شد." },
                new() { EventType = SmsEventType.WithdrawalRejected, Template = "درخواست انصراف از مأموریت {MissionNumber} رد شد." },
                new() { EventType = SmsEventType.MissionStartReminder, Template = "یادآوری: مأموریت {MissionNumber} در ساعت {StartDateTime} آغاز می‌شود." },
                new() { EventType = SmsEventType.MissionCompleted, Template = "مأموریت {MissionNumber} به پایان رسید." }
            ]
        };
    }
}
