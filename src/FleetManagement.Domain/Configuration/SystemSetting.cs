using FleetManagement.Domain.Common;

namespace FleetManagement.Domain.Configuration;

/// <summary>
/// یک مقدار تنظیماتی سامانه که به‌صورت کلید و مقدار در SQL نگهداری می‌شود.
/// مقادیر تنظیمات مطابق تصمیم پروژه به‌صورت متن ساده ذخیره می‌شوند.
/// </summary>
public sealed class SystemSetting : Entity
{
    private SystemSetting()
    {
        // سازنده خصوصی برای استفاده EF Core
        Key = string.Empty;
        Value = string.Empty;
    }

    /// <summary>کلید یکتای تنظیم.</summary>
    public string Key { get; private set; } = string.Empty;

    /// <summary>مقدار تنظیم؛ قالب JSON نیز برای تنظیمات چندفیلدی پشتیبانی می‌شود.</summary>
    public string Value { get; private set; } = string.Empty;

    /// <summary>زمان آخرین تغییر مقدار تنظیم بر اساس UTC.</summary>
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>ایجاد تنظیم جدید.</summary>
    public SystemSetting(string key, string value)
    {
        Update(key, value);
    }

    /// <summary>اصلاح مقدار تنظیم موجود.</summary>
    public void Update(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        if (key.Trim().Length > 128)
        {
            throw new ArgumentOutOfRangeException(nameof(key), "کلید تنظیم حداکثر ۱۲۸ نویسه است.");
        }

        Key = key.Trim();
        Value = value;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
