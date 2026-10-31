namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// نقش‌هایی که مدیر سامانه به‌صورت دستی به یک کاربر تخصیص داده است.
/// </summary>
public sealed class ManualUserRoleMapping
{
    /// <summary>نام کاربری دامنه.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>کلید نقش‌های دستی کاربر.</summary>
    public List<string> RoleKeys { get; set; } = [];
}
