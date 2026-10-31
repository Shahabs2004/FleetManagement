namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// اطلاعات هویتی کاربر که پس از احراز هویت از Active Directory دریافت می‌شود.
/// </summary>
public sealed class DirectoryUserProfile
{
    /// <summary>نام کاربری کوتاه در دامنه.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>نام.</summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>نام خانوادگی.</summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>نام نمایشی.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>واحد سازمانی طبق ویژگی انتخاب‌شده در تنظیمات.</summary>
    public string OrganizationalUnit { get; set; } = string.Empty;

    /// <summary>شناسه یکتای کاربر در دایرکتوری.</summary>
    public string DistinguishedName { get; set; } = string.Empty;

    /// <summary>گروه‌های مجاز کاربر در AD.</summary>
    public List<ActiveDirectoryGroup> AllowedGroups { get; set; } = [];
}
