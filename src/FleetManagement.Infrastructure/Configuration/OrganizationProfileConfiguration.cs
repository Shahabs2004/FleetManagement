namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// اطلاعات هویتی سازمان که در سربرگ، صفحه ورود و برگه مأموریت استفاده می‌شود.
/// </summary>
public sealed class OrganizationProfileConfiguration
{
    /// <summary>نام نمایشی سازمان.</summary>
    public string OrganizationName { get; set; } = string.Empty;

    /// <summary>نوع محتوای فایل نشان سازمان.</summary>
    public string? LogoContentType { get; set; }

    /// <summary>محتوای تصویر نشان به‌صورت Base64 برای نگهداری در SQL.</summary>
    public string? LogoBase64 { get; set; }
}
