namespace FleetManagement.Application.Configuration;

/// <summary>
/// تنظیمات قابل پیکربندی برای برنامه‌ریزی مأموریت‌ها.
/// </summary>
public sealed class MissionRulesConfiguration
{
    /// <summary>فاصله حداقلی بین دو مأموریت متوالی برای راننده و خودرو.</summary>
    public int MinimumGapHours { get; set; }

    /// <summary>حداکثر فاصله مجاز رزرو از زمان جاری به روز.</summary>
    public int BookingHorizonDays { get; set; } = 90;

    /// <summary>قواعد پیش‌فرض برای نصب اولیه.</summary>
    public static MissionRulesConfiguration CreateDefault() => new();
}
