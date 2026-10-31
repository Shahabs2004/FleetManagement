namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// مجوزهای مستقیم مدیر سامانه برای یک کاربر، مستقل از نقش‌های او.
/// </summary>
public sealed class ManualUserPermissionMapping
{
    /// <summary>نام کاربری دامنه.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>مجوزهای مستقیم اعطا یا ردشده.</summary>
    public List<RolePermissionConfiguration> Permissions { get; set; } = [];
}
