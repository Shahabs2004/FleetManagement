namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// قاعده اعطا یا رد یک مجوز برای یک نقش.
/// </summary>
public sealed class RolePermissionConfiguration
{
    /// <summary>شناسه عملیات مانند missions.create یا drivers.view.</summary>
    public string Permission { get; set; } = string.Empty;

    /// <summary>اعطا یا رد صریح مجوز.</summary>
    public PermissionEffect Effect { get; set; } = PermissionEffect.Grant;
}
