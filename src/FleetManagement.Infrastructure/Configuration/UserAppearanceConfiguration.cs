namespace FleetManagement.Infrastructure.Configuration;

/// <summary>تنظیمات ظاهری شخصی کاربر.</summary>
public sealed class UserAppearanceConfiguration
{
    /// <summary>حالت نمایش روشن یا تاریک.</summary>
    public string Theme { get; set; } = "Light";
}
