namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// الگوی قابل تنظیم دستور مأموریت برای چاپ A4.
/// </summary>
public sealed class MissionOrderTemplateConfiguration
{
    /// <summary>عنوان سربرگ.</summary>
    public string HeaderTitle { get; set; } = "دستور مأموریت";

    /// <summary>نام سازمان چاپ‌شده در سربرگ.</summary>
    public string OrganizationName { get; set; } = string.Empty;

    /// <summary>متن مقدمه بالای فرم.</summary>
    public string IntroText { get; set; } = "بدین‌وسیله مأموریت زیر ابلاغ می‌گردد.";

    /// <summary>متن پایانی دستور.</summary>
    public string FooterText { get; set; } = string.Empty;

    /// <summary>عنوان محل امضای درخواست‌کننده.</summary>
    public string RequesterSignatureLabel { get; set; } = "امضای درخواست‌کننده";

    /// <summary>عنوان محل امضای مدیر ناوگان یا اپراتور.</summary>
    public string OperatorSignatureLabel { get; set; } = "امضای مسئول ناوگان";

    /// <summary>عنوان محل امضای مدیر تأییدکننده.</summary>
    public string ApproverSignatureLabel { get; set; } = "امضای مقام تأییدکننده";

    /// <summary>عنوان محل امضای راننده.</summary>
    public string DriverSignatureLabel { get; set; } = "امضای راننده";

    /// <summary>مقادیر پیش‌فرض.</summary>
    public static MissionOrderTemplateConfiguration CreateDefault() => new();
}
