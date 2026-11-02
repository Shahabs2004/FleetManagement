namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// نگاشت یک گروه امنیتی Active Directory به نقش داخلی سامانه.
/// </summary>
public sealed class AdGroupRoleMapping
{
    /// <summary>Distinguished Name گروه در دایرکتوری.</summary>
    public string GroupDistinguishedName { get; set; } = string.Empty;

    /// <summary>کلید نقش داخلی.</summary>
    public string RoleKey { get; set; } = string.Empty;
}
