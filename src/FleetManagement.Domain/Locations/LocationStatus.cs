namespace FleetManagement.Domain.Locations;

/// <summary>
/// وضعیت پیشنهاد مکان در چرخه بررسی مدیر سامانه.
/// </summary>
public enum LocationStatus
{
    /// <summary>پیشنهاد در انتظار بررسی است.</summary>
    Pending = 1,

    /// <summary>مکان تأیید شده و قابل انتخاب است.</summary>
    Approved = 2,

    /// <summary>پیشنهاد مکان رد شده است.</summary>
    Rejected = 3,

    /// <summary>مکان تأییدشده غیرفعال شده است.</summary>
    Inactive = 4
}
