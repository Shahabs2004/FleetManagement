using FleetManagement.Domain.Common;

namespace FleetManagement.Domain.Drivers;

/// <summary>
/// اطلاعات پایه راننده؛ فقط چهار فیلد کسب‌وکاری مورد توافق در این موجودیت نگهداری می‌شود.
/// </summary>
public sealed class Driver : Entity
{
    private Driver()
    {
        // سازنده خصوصی برای استفاده EF Core
        FirstName = string.Empty;
        LastName = string.Empty;
        MobilePhone = string.Empty;
    }

    /// <summary>نام راننده.</summary>
    public string FirstName { get; private set; }

    /// <summary>نام خانوادگی راننده.</summary>
    public string LastName { get; private set; }

    /// <summary>شماره تلفن همراه راننده.</summary>
    public string MobilePhone { get; private set; }

    /// <summary>وضعیت استخدامی راننده.</summary>
    public DriverEmploymentStatus EmploymentStatus { get; private set; }

    /// <summary>وضعیت بایگانی؛ راننده بایگانی‌شده مأموریت جدید نمی‌گیرد.</summary>
    public bool IsArchived { get; private set; }

    /// <summary>
    /// ایجاد راننده جدید با وضعیت استخدامی فعال.
    /// </summary>
    public Driver(string firstName, string lastName, string mobilePhone)
    {
        UpdateProfile(firstName, lastName, mobilePhone);
        EmploymentStatus = DriverEmploymentStatus.Active;
    }

    /// <summary>
    /// اصلاح اطلاعات چهارگانه راننده.
    /// </summary>
    public void UpdateProfile(string firstName, string lastName, string mobilePhone)
    {
        FirstName = RequiredText(firstName, nameof(firstName));
        LastName = RequiredText(lastName, nameof(lastName));
        MobilePhone = RequiredText(mobilePhone, nameof(mobilePhone));
    }

    /// <summary>
    /// تغییر وضعیت استخدامی راننده.
    /// </summary>
    public void ChangeEmploymentStatus(DriverEmploymentStatus status)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), "وضعیت استخدامی معتبر نیست.");
        }

        EmploymentStatus = status;
    }

    /// <summary>
    /// بایگانی راننده بدون حذف سوابق تاریخی او.
    /// کنترل نداشتن مأموریت فعال در لایه کاربرد انجام می‌شود.
    /// </summary>
    public void Archive()
    {
        IsArchived = true;
    }

    /// <summary>
    /// بازگرداندن راننده از بایگانی فقط در صورت فعال بودن وضعیت استخدامی.
    /// </summary>
    public void Reactivate()
    {
        if (EmploymentStatus != DriverEmploymentStatus.Active)
        {
            throw new InvalidOperationException("فقط راننده با وضعیت استخدامی فعال قابل بازگردانی است.");
        }

        IsArchived = false;
    }

    /// <summary>
    /// بررسی امکان تخصیص راننده به مأموریت جدید.
    /// </summary>
    public bool CanBeAssignedToMission()
    {
        return !IsArchived && EmploymentStatus == DriverEmploymentStatus.Active;
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
