namespace FleetManagement.Domain.Vehicles;

/// <summary>
/// وضعیت‌هایی که کاربر مجاز است به‌صورت دستی برای خودرو تعیین کند.
/// وضعیت «در مأموریت» از روی مأموریت فعال محاسبه می‌شود و دستی نیست.
/// </summary>
public enum ManualVehicleStatus
{
    /// <summary>خودرو آماده تخصیص است.</summary>
    Ready = 1,

    /// <summary>خودرو در تعمیرگاه است.</summary>
    RepairShop = 2,

    /// <summary>خودرو از سرویس خارج شده است.</summary>
    OutOfService = 3
}
