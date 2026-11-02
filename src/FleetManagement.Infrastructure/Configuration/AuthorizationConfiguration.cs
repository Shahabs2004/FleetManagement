using FleetManagement.Application.Authorization;

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
                    Permissions = [new() { Permission = PermissionCatalog.All, Effect = PermissionEffect.Grant }]
                },
                new()
                {
                    Key = "FleetManager",
                    Name = "مدیر ناوگان",
                    Description = "مدیریت رانندگان، خودروها، مأموریت‌ها و گزارش‌های ناوگان.",
                    IsSystemRole = true,
                    Permissions =
                    [
                        new() { Permission = PermissionCatalog.Drivers.All },
                        new() { Permission = PermissionCatalog.Drivers.DefaultVehicleManage },
                        new() { Permission = PermissionCatalog.Vehicles.All },
                        new() { Permission = PermissionCatalog.Missions.All },
                        new() { Permission = PermissionCatalog.Missions.SplitScheduleEdit },
                        new() { Permission = PermissionCatalog.Reports.View },
                        new() { Permission = PermissionCatalog.Reports.Export },
                        new() { Permission = PermissionCatalog.Missions.TimelineView },
                        new() { Permission = PermissionCatalog.Notifications.View },
                        new() { Permission = PermissionCatalog.Dashboard.View }
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
                        new() { Permission = PermissionCatalog.Drivers.View },
                        new() { Permission = PermissionCatalog.Vehicles.View },
                        new() { Permission = PermissionCatalog.Missions.View },
                        new() { Permission = PermissionCatalog.Missions.ViewOwn },
                        new() { Permission = PermissionCatalog.Missions.Review },
                        new() { Permission = PermissionCatalog.Missions.Print },
                        new() { Permission = PermissionCatalog.Missions.Create },
                        new() { Permission = PermissionCatalog.Missions.Edit },
                        new() { Permission = PermissionCatalog.Missions.Approve },
                        new() { Permission = PermissionCatalog.Missions.Reject },
                        new() { Permission = PermissionCatalog.Missions.Cancel },
                        new() { Permission = PermissionCatalog.Missions.Extend },
                        new() { Permission = PermissionCatalog.Missions.Split },
                        new() { Permission = PermissionCatalog.Missions.TimelineView },
                        new() { Permission = PermissionCatalog.Missions.OdometerManage },
                        new() { Permission = PermissionCatalog.Missions.ChangeReview },
                        new() { Permission = PermissionCatalog.Notifications.View },
                        new() { Permission = PermissionCatalog.Dashboard.View }
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
                        new() { Permission = PermissionCatalog.Drivers.View },
                        new() { Permission = PermissionCatalog.Vehicles.View },
                        new() { Permission = PermissionCatalog.Missions.View },
                        new() { Permission = PermissionCatalog.Dashboard.View }
                    ]
                },
                new()
                {
                    Key = "Requester",
                    Name = "درخواست‌کننده مأموریت",
                    Description = "ثبت، پیگیری، چاپ و درخواست تغییر مأموریت‌های خود.",
                    IsSystemRole = true,
                    Permissions =
                    [
                        new() { Permission = PermissionCatalog.Missions.ViewOwn },
                        new() { Permission = PermissionCatalog.Missions.Create },
                        new() { Permission = PermissionCatalog.Missions.Print },
                        new() { Permission = PermissionCatalog.Notifications.View },
                        new() { Permission = PermissionCatalog.Dashboard.View }
                    ]
                }
            ]
        };
    }
}
