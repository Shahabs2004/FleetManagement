namespace FleetManagement.Infrastructure.Bootstrap;

/// <summary>
/// داده‌های حداقلی مورد نیاز برای شروع و بازیابی سامانه.
/// این مدل فقط در فایل محافظت‌شده محلی نگهداری می‌شود.
/// </summary>
public sealed class BootstrapState
{
    /// <summary>نسخه ساختار فایل برای پشتیبانی از ارتقای آینده.</summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>آیا ویزارد راه‌اندازی نهایی شده است؟</summary>
    public bool SetupCompleted { get; set; }

    /// <summary>هش کد یک‌بارمصرف راه‌اندازی.</summary>
    public string? SetupCodeHash { get; set; }

    /// <summary>زمان انقضای کد راه‌اندازی بر اساس UTC.</summary>
    public DateTimeOffset? SetupCodeExpiresAtUtc { get; set; }

    /// <summary>هش کد مستقل دسترسی مدیر فنی به تنظیمات حساس ویزارد.</summary>
    public string? TechnicalAdministratorCodeHash { get; set; }

    /// <summary>زمان انقضای کد دسترسی مدیر فنی بر اساس UTC.</summary>
    public DateTimeOffset? TechnicalAdministratorCodeExpiresAtUtc { get; set; }

    /// <summary>نام کاربری مدیر محلی اضطراری.</summary>
    public string? LocalAdministratorUsername { get; set; }

    /// <summary>هش رمز عبور مدیر محلی؛ هرگز رمز خام ذخیره نمی‌شود.</summary>
    public string? LocalAdministratorPasswordHash { get; set; }

    /// <summary>نمک مستقل هش رمز عبور مدیر محلی.</summary>
    public string? LocalAdministratorPasswordSalt { get; set; }

    /// <summary>رشته اتصال SQL برای شروع و بازیابی اضطراری.</summary>
    public string? SqlConnectionString { get; set; }

    /// <summary>آیا حساب مدیر محلی ساخته شده است؟</summary>
    public bool HasLocalAdministrator =>
        !string.IsNullOrWhiteSpace(LocalAdministratorUsername)
        && !string.IsNullOrWhiteSpace(LocalAdministratorPasswordHash)
        && !string.IsNullOrWhiteSpace(LocalAdministratorPasswordSalt);
}
