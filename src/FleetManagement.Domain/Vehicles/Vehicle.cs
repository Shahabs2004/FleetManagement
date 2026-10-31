using FleetManagement.Domain.Common;

namespace FleetManagement.Domain.Vehicles;

/// <summary>
/// مشخصات اصلی خودرو و وضعیت دستی آن.
/// برند، مدل و پلاک تنها مشخصات پایه خودرو هستند.
/// </summary>
public sealed class Vehicle : Entity
{
    private Vehicle()
    {
        // سازنده خصوصی برای استفاده EF Core
    }

    /// <summary>برند خودرو.</summary>
    public string Brand { get; private set; } = string.Empty;

    /// <summary>مدل خودرو.</summary>
    public string Model { get; private set; } = string.Empty;

    /// <summary>نمایش ساختاریافته پلاک ملی؛ اعتبارسنجی تفصیلی در Value Object انجام می‌شود.</summary>
    public string LicensePlate { get; private set; } = string.Empty;

    /// <summary>وضعیت دستی خودرو؛ وضعیت مأموریت از این فیلد مستقل است.</summary>
    public ManualVehicleStatus ManualStatus { get; private set; } = ManualVehicleStatus.Ready;

    /// <summary>آیا خودرو بایگانی شده است؟</summary>
    public bool IsArchived { get; private set; }

    /// <summary>
    /// ایجاد خودرو با وضعیت اولیه آماده به کار.
    /// </summary>
    public Vehicle(string brand, string model, string licensePlate)
    {
        UpdateDetails(brand, model, licensePlate);
        ManualStatus = ManualVehicleStatus.Ready;
    }

    /// <summary>
    /// اصلاح مشخصات خودرو.
    /// اعتبارسنجی ساختار پلاک در مرحله بعد به Value Object اختصاصی منتقل می‌شود.
    /// </summary>
    public void UpdateDetails(string brand, string model, string licensePlate)
    {
        Brand = RequiredText(brand, nameof(brand));
        Model = RequiredText(model, nameof(model));
        LicensePlate = RequiredText(licensePlate, nameof(licensePlate));
    }

    /// <summary>
    /// تغییر وضعیت دستی خودرو؛ تا وقتی مأموریت فعال دارد، محدودیت تعمیرگاه/خارج از سرویس اعمال می‌شود.
    /// </summary>
    public void ChangeManualStatus(ManualVehicleStatus status, bool hasActiveMission)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), "وضعیت خودرو معتبر نیست.");
        }

        if (hasActiveMission && status != ManualVehicleStatus.Ready)
        {
            throw new InvalidOperationException("تا پایان مأموریت فعال، وضعیت خودرو قابل تغییر به تعمیرگاه یا خارج از سرویس نیست.");
        }

        ManualStatus = status;
    }

    /// <summary>
    /// محاسبه وضعیت نهایی؛ مأموریت فعال بر وضعیت دستی اولویت دارد.
    /// پس از پایان مأموریت، وضعیت دستی قبلی دوباره نمایش داده می‌شود.
    /// </summary>
    public VehicleEffectiveStatus GetEffectiveStatus(bool hasActiveMission)
    {
        if (hasActiveMission)
        {
            return VehicleEffectiveStatus.InMission;
        }

        return ManualStatus switch
        {
            ManualVehicleStatus.Ready => VehicleEffectiveStatus.Ready,
            ManualVehicleStatus.RepairShop => VehicleEffectiveStatus.RepairShop,
            ManualVehicleStatus.OutOfService => VehicleEffectiveStatus.OutOfService,
            _ => throw new InvalidOperationException("وضعیت دستی خودرو ناشناخته است.")
        };
    }

    /// <summary>
    /// بایگانی خودرو بدون حذف سوابق مأموریت.
    /// کنترل نداشتن مأموریت فعال در لایه کاربرد انجام می‌شود.
    /// </summary>
    public void Archive()
    {
        IsArchived = true;
    }

    /// <summary>
    /// بازگرداندن خودرو از بایگانی فقط در وضعیت آماده به کار.
    /// </summary>
    public void Reactivate()
    {
        if (ManualStatus != ManualVehicleStatus.Ready)
        {
            throw new InvalidOperationException("خودروی تعمیرگاهی یا خارج از سرویس قابل بازگردانی نیست.");
        }

        IsArchived = false;
    }

    /// <summary>
    /// بررسی امکان تخصیص خودرو به مأموریت جدید.
    /// </summary>
    public bool CanBeAssignedToMission(bool hasActiveMission)
    {
        return !IsArchived
            && !hasActiveMission
            && ManualStatus == ManualVehicleStatus.Ready;
    }

    /// <summary>
    /// حذف فاصله‌های اضافی و جلوگیری از ثبت متن خالی.
    /// </summary>
    private static string RequiredText(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim();
    }
}
