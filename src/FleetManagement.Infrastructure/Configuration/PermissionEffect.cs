namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// اثر یک قاعده مجوز روی عملیات سامانه.
/// </summary>
public enum PermissionEffect
{
    /// <summary>اعطای مجوز.</summary>
    Grant = 1,

    /// <summary>رد صریح مجوز؛ بر تمام اعطاها اولویت دارد.</summary>
    Deny = 2
}
