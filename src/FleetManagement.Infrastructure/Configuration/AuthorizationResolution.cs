namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// نقش‌ها و مجوزهای مؤثر کاربر پس از ترکیب عضویت AD و نقش‌های دستی.
/// </summary>
public sealed class AuthorizationResolution
{
    /// <summary>کلید نقش‌های مؤثر کاربر.</summary>
    public IReadOnlyCollection<string> Roles { get; init; } = [];

    /// <summary>مجوزهای اعطاشده.</summary>
    public IReadOnlyCollection<string> GrantedPermissions { get; init; } = [];

    /// <summary>مجوزهای ردشده به‌صورت صریح.</summary>
    public IReadOnlyCollection<string> DeniedPermissions { get; init; } = [];
}
