namespace FleetManagement.Infrastructure.Configuration;

/// <summary>نتیجه استانداردشده وضعیت تحویل؛ مقادیر ارائه‌دهنده بدون نمونه واقعی حدس زده نمی‌شوند.</summary>
public enum AdpDeliveryStatusKind
{
    /// <summary>وضعیت ارائه‌دهنده هنوز نگاشت و تأیید نشده است.</summary>
    Unknown = 0,

    /// <summary>تحویل نهایی با نگاشت صریح تأیید شده است.</summary>
    Delivered = 1
}

/// <summary>نگاشت وضعیت خام ADP به وضعیت داخلی قابل اتکا.</summary>
public static class AdpDeliveryStatusMapper
{
    /// <summary>وضعیت خام را فقط با نگاشت صریح و بدون حساسیت به بزرگی حروف تبدیل می‌کند.</summary>
    public static AdpDeliveryStatusKind Map(
        string? providerStatus,
        IReadOnlyDictionary<string, AdpDeliveryStatusKind>? mappings)
    {
        if (string.IsNullOrWhiteSpace(providerStatus) || mappings is null)
        {
            return AdpDeliveryStatusKind.Unknown;
        }

        foreach (var mapping in mappings)
        {
            if (string.Equals(mapping.Key, providerStatus.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return mapping.Value == AdpDeliveryStatusKind.Delivered
                    ? AdpDeliveryStatusKind.Delivered
                    : AdpDeliveryStatusKind.Unknown;
            }
        }

        return AdpDeliveryStatusKind.Unknown;
    }

    /// <summary>متن فارسی وضعیت تحویل برای گزارش پیامک.</summary>
    public static string GetPersianLabel(
        string? providerStatus,
        IReadOnlyDictionary<string, AdpDeliveryStatusKind>? mappings) =>
        Map(providerStatus, mappings) == AdpDeliveryStatusKind.Delivered
            ? "تحویل‌شده"
            : "نامشخص";
}
