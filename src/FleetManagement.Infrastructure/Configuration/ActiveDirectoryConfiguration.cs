namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// تنظیمات اتصال و همگام‌سازی Active Directory.
/// این تنظیمات در SQL و مطابق تصمیم پروژه به‌صورت متن ساده نگهداری می‌شوند.
/// </summary>
public sealed class ActiveDirectoryConfiguration
{
    /// <summary>نام DNS دامنه سازمان.</summary>
    public string DomainDnsName { get; set; } = string.Empty;

    /// <summary>نام میزبان یا آدرس سرور LDAP.</summary>
    public string LdapServer { get; set; } = string.Empty;

    /// <summary>درگاه اتصال LDAP یا LDAPS.</summary>
    public int Port { get; set; } = 636;

    /// <summary>استفاده از اتصال امن LDAPS.</summary>
    public bool UseLdaps { get; set; } = true;

    /// <summary>مسیر پایه جست‌وجوی کاربران و گروه‌ها در دایرکتوری.</summary>
    public string BaseDistinguishedName { get; set; } = string.Empty;

    /// <summary>نام حساب سرویس برای جست‌وجوی اطلاعات دایرکتوری.</summary>
    public string ServiceAccountUsername { get; set; } = string.Empty;

    /// <summary>رمز حساب سرویس؛ مقدار در SQL بدون رمزنگاری ذخیره می‌شود.</summary>
    public string ServiceAccountPassword { get; set; } = string.Empty;

    /// <summary>نام ویژگی AD که واحد سازمانی کاربر از آن خوانده می‌شود.</summary>
    public string OrganizationalUnitAttribute { get; set; } = "department";

    /// <summary>فعال بودن احراز هویت یکپارچه ویندوز از طریق IIS.</summary>
    public bool UseWindowsIntegratedAuthentication { get; set; } = true;

    /// <summary>فعال بودن ورود جایگزین با نام کاربری و رمز عبور AD.</summary>
    public bool AllowUsernamePasswordFallback { get; set; } = true;

    /// <summary>آیا آزمون اتصال حساب سرویس و Base DN با موفقیت انجام شده است؟</summary>
    public bool IsConnectionVerified { get; set; }

    /// <summary>گروه‌های AD مجاز برای ورود به سامانه.</summary>
    public List<ActiveDirectoryGroup> AllowedGroups { get; set; } = [];
}

/// <summary>
/// یک گروه امنیتی مجاز در Active Directory.
/// </summary>
public sealed class ActiveDirectoryGroup
{
    /// <summary>نام نمایشی گروه.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Distinguished Name گروه در AD.</summary>
    public string DistinguishedName { get; set; } = string.Empty;
}
