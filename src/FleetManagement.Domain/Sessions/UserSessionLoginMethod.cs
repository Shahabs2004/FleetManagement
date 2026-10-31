namespace FleetManagement.Domain.Sessions;

/// <summary>
/// روش احراز هویت برای ثبت در سابقه نشست‌ها.
/// </summary>
public enum UserSessionLoginMethod
{
    /// <summary>ورود با نام کاربری و رمز عبور Active Directory.</summary>
    DirectoryPassword = 1,

    /// <summary>ورود یکپارچه ویندوز از طریق IIS.</summary>
    WindowsIntegrated = 2,

    /// <summary>ورود اضطراری مدیر محلی مستقل از AD.</summary>
    LocalEmergencyAdministrator = 3
}
