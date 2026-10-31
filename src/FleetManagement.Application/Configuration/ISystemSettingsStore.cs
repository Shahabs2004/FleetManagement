namespace FleetManagement.Application.Configuration;

/// <summary>
/// قرارداد خواندن و ذخیره تنظیمات تایپ‌شده سامانه.
/// پیاده‌سازی زیرساختی، مقادیر را در جدول SystemSettings نگهداری می‌کند.
/// </summary>
public interface ISystemSettingsStore
{
    /// <summary>خواندن تنظیم با کلید مشخص و تبدیل JSON به نوع درخواستی.</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>ذخیره یا جایگزینی تنظیم به‌صورت JSON.</summary>
    Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default);
}
