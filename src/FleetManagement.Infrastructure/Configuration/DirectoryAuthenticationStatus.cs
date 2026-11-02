namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// نتیجه بررسی هویت و مجوز ورود کاربر در Active Directory.
/// </summary>
public enum DirectoryAuthenticationStatus
{
    /// <summary>ورود موفق و عضویت در گروه مجاز.</summary>
    Success = 1,

    /// <summary>تنظیمات AD هنوز ثبت نشده است.</summary>
    NotConfigured = 2,

    /// <summary>نام کاربری یا رمز عبور نامعتبر است.</summary>
    InvalidCredentials = 3,

    /// <summary>حساب در دایرکتوری غیرفعال است.</summary>
    AccountDisabled = 4,

    /// <summary>حساب قفل شده است.</summary>
    AccountLocked = 5,

    /// <summary>اعتبار زمانی حساب پایان یافته است.</summary>
    AccountExpired = 6,

    /// <summary>کاربر عضو گروه مجاز نیست.</summary>
    NotInAllowedGroup = 7,

    /// <summary>سرویس یا اتصال Active Directory در دسترس نیست.</summary>
    DirectoryUnavailable = 8,

    /// <summary>روش ورود در تنظیمات سامانه غیرفعال است.</summary>
    AuthenticationMethodDisabled = 9
}
