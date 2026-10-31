namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// انتخاب گیرندگان پیامک برای هر رویداد.
/// </summary>
public enum SmsRecipientRule
{
    /// <summary>فقط درخواست‌کننده مأموریت.</summary>
    Requester = 1,

    /// <summary>فقط راننده.</summary>
    Driver = 2,

    /// <summary>درخواست‌کننده و راننده.</summary>
    RequesterAndDriver = 3
}
