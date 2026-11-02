namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// سیاست عمر نشست‌های ورود کاربران.
/// </summary>
public sealed class SessionPolicyConfiguration
{
    /// <summary>مهلت بیکاری نشست بر حسب دقیقه.</summary>
    public int IdleTimeoutMinutes { get; set; } = 30;

    /// <summary>حداکثر عمر مطلق نشست بر حسب ساعت.</summary>
    public int AbsoluteTimeoutHours { get; set; } = 8;

    /// <summary>ساخت سیاست پیش‌فرض مطابق تصمیم پروژه.</summary>
    public static SessionPolicyConfiguration CreateDefault() => new();
}
