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

    /// <summary>تعداد ستون‌های چیدمان محل امضا؛ فقط یک یا دو ستون مجاز است.</summary>
    public int SignatureColumns { get; set; } = 2;

    /// <summary>اعتبارسنجی مقادیر قابل ذخیره الگوی چاپ.</summary>
    public void Validate()
    {
        ValidateText(HeaderTitle, 100, nameof(HeaderTitle));
        ValidateText(OrganizationName, 200, nameof(OrganizationName), allowEmpty: true);
        ValidateText(IntroText, 1000, nameof(IntroText), allowEmpty: true);
        ValidateText(FooterText, 1000, nameof(FooterText), allowEmpty: true);
        ValidateText(RequesterSignatureLabel, 100, nameof(RequesterSignatureLabel));
        ValidateText(OperatorSignatureLabel, 100, nameof(OperatorSignatureLabel));
        ValidateText(ApproverSignatureLabel, 100, nameof(ApproverSignatureLabel));
        ValidateText(DriverSignatureLabel, 100, nameof(DriverSignatureLabel));

        if (SignatureColumns is not (1 or 2))
        {
            throw new ArgumentOutOfRangeException(
                nameof(SignatureColumns),
                "چیدمان محل امضا باید یک یا دو ستون باشد.");
        }
    }

    private static void ValidateText(string? value, int maxLength, string parameterName, bool allowEmpty = false)
    {
        if (!allowEmpty) ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value?.Length > maxLength) throw new ArgumentOutOfRangeException(parameterName);
    }

    /// <summary>مقادیر پیش‌فرض.</summary>
    public static MissionOrderTemplateConfiguration CreateDefault() => new();
}
