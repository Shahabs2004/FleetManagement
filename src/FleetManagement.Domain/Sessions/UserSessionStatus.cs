namespace FleetManagement.Domain.Sessions;

/// <summary>
/// وضعیت نهایی یا جاری یک تلاش ورود و نشست کاربر.
/// </summary>
public enum UserSessionStatus
{
    /// <summary>نشست فعال.</summary>
    Active = 1,

    /// <summary>خروج دستی کاربر.</summary>
    LoggedOut = 2,

    /// <summary>پایان نشست به علت ۳۰ دقیقه عدم فعالیت.</summary>
    IdleExpired = 3,

    /// <summary>پایان نشست به علت رسیدن به سقف هشت ساعت.</summary>
    AbsoluteExpired = 4,

    /// <summary>تلاش ورود ناموفق.</summary>
    Failed = 5,

    /// <summary>پایان نشست به علت غیرفعال‌شدن حساب یا حذف عضویت مجاز در AD.</summary>
    DirectoryRevoked = 6
}
