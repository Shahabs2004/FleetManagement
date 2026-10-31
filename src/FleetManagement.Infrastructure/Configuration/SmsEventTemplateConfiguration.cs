namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// تنظیم فعال بودن، متن قالب و گیرندگان یک رویداد پیامکی.
/// </summary>
public sealed class SmsEventTemplateConfiguration
{
    /// <summary>شناسه رویداد پیامکی.</summary>
    public SmsEventType EventType { get; set; }

    /// <summary>فعال بودن ارسال پیامک برای این رویداد.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>قاعده انتخاب گیرنده.</summary>
    public SmsRecipientRule RecipientRule { get; set; } = SmsRecipientRule.RequesterAndDriver;

    /// <summary>متن قالب با متغیرهای مأموریت.</summary>
    public string Template { get; set; } = string.Empty;
}
