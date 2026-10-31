namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// پیکربندی نقش‌ها، مجوزها، نگاشت گروه‌های AD و نقش‌های دستی کاربران.
/// </summary>
public sealed class AuthorizationConfiguration
{
    /// <summary>نقش‌های قابل استفاده در سامانه.</summary>
    public List<RoleDefinitionConfiguration> Roles { get; set; } = [];

    /// <summary>نگاشت گروه‌های امنیتی AD به نقش داخلی.</summary>
    public List<AdGroupRoleMapping> GroupMappings { get; set; } = [];

    /// <summary>نقش‌های تکمیلی که مدیر سامانه به کاربران داده است.</summary>
    public List<ManualUserRoleMapping> ManualUserRoles { get; set; } = [];

    /// <summary>مجوزهای مستقیم هر کاربر مستقل از نقش‌های او.</summary>
    public List<ManualUserPermissionMapping> ManualUserPermissions { get; set; } = [];

    /// <summary>
    /// ساخت نقش‌های پایه و مجوزهای پیش‌فرض با اصل حداقل دسترسی.
    /// </summary>
    public static AuthorizationConfiguration CreateDefault()
    {
        return new AuthorizationConfiguration
        {
            Roles =
            [
                new()
                {
                    Key = "SystemAdministrator",
                    Name = "مدیر سامانه",
                    Description = "دسترسی کامل به همه بخش‌ها و تنظیمات سامانه.",
                    IsSystemRole = true,
                    Permissions = [new() { Permission = "*", Effect = PermissionEffect.Grant }]
                },
                new()
                {
                    Key = "FleetManager",
                    Name = "مدیر ناوگان",
                    Description = "مدیریت رانندگان، خودروها، مأموریت‌ها و گزارش‌های ناوگان.",
                    IsSystemRole = true,
                    Permissions =
                    [
                        new() { Permission = "drivers.*" },
                        new() { Permission = "vehicles.*" },
                        new() { Permission = "missions.*" },
                        new() { Permission = "reports.view" },
                        new() { Permission = "reports.export" },
                        new() { Permission = "dashboard.view" }
                    ]
                },
                new()
                {
                    Key = "Operator",
                    Name = "اپراتور",
                    Description = "ثبت و پیگیری درخواست‌ها و مدیریت عملیات مجاز مأموریت.",
                    IsSystemRole = true,
                    Permissions =
                    [
                        new() { Permission = "drivers.view" },
                        new() { Permission = "vehicles.view" },
                        new() { Permission = "missions.view" },
                        new() { Permission = "missions.create" },
                        new() { Permission = "missions.edit" },
                        new() { Permission = "missions.approve" },
                        new() { Permission = "missions.reject" },
                        new() { Permission = "missions.cancel" },
                        new() { Permission = "missions.extend" },
                        new() { Permission = "missions.split" },
                        new() { Permission = "notifications.view" },
                        new() { Permission = "dashboard.view" }
                    ]
                },
                new()
                {
                    Key = "Viewer",
                    Name = "مشاهده‌گر",
                    Description = "دسترسی فقط خواندنی به اطلاعات مجاز.",
                    IsSystemRole = true,
                    Permissions =
                    [
                        new() { Permission = "drivers.view" },
                        new() { Permission = "vehicles.view" },
                        new() { Permission = "missions.view" },
                        new() { Permission = "dashboard.view" }
                    ]
                }
            ]
        };
    }
}
