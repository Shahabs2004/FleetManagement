namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// تعریف یک نقش سازمانی و مجوزهای آن.
/// </summary>
public sealed class RoleDefinitionConfiguration
{
    /// <summary>کلید فنی و یکتای نقش.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>عنوان فارسی نقش.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>شرح کاربرد نقش.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>نقش‌های پایه سامانه قابل حذف یا تغییر کلید نیستند.</summary>
    public bool IsSystemRole { get; set; }

    /// <summary>فهرست مجوزهای اعطا یا ردشده.</summary>
    public List<RolePermissionConfiguration> Permissions { get; set; } = [];
}
