namespace FleetManagement.Domain.Vehicles;

/// <summary>
/// وضعیت نهایی خودرو که در رابط کاربری و گزارش‌ها نمایش داده می‌شود.
/// </summary>
public enum VehicleEffectiveStatus
{
    /// <summary>خودرو آماده به کار است.</summary>
    Ready = 1,

    /// <summary>خودرو در مأموریت فعال قرار دارد.</summary>
    InMission = 2,

    /// <summary>خودرو در تعمیرگاه است.</summary>
    RepairShop = 3,

    /// <summary>خودرو خارج از سرویس است.</summary>
    OutOfService = 4
}
