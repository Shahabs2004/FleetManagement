namespace FleetManagement.Infrastructure.Reporting;

/// <summary>
/// چیدمان شخصی کارت‌های داشبورد یک کاربر.
/// </summary>
public sealed class DashboardLayoutConfiguration
{
    /// <summary>ترتیب نمایش کارت‌ها.</summary>
    public List<string> CardOrder { get; set; } = [];

    /// <summary>کلید کارت‌های پنهان‌شده توسط کاربر.</summary>
    public List<string> HiddenCards { get; set; } = [];
}
