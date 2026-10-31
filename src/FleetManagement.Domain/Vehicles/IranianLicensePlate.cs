using System.Text;
using System.Text.RegularExpressions;

namespace FleetManagement.Domain.Vehicles;

/// <summary>
/// نرمال‌سازی و اعتبارسنجی پلاک ملی خودروهای ایران.
/// قالب ذخیره‌سازی: دو رقم، حرف پلاک، سه رقم و دو رقم منطقه.
/// </summary>
public static partial class IranianLicensePlate
{
    private static readonly IReadOnlyDictionary<char, char> DigitMap = new Dictionary<char, char>
    {
        ['۰'] = '0', ['۱'] = '1', ['۲'] = '2', ['۳'] = '3', ['۴'] = '4',
        ['۵'] = '5', ['۶'] = '6', ['۷'] = '7', ['۸'] = '8', ['۹'] = '9',
        ['٠'] = '0', ['١'] = '1', ['٢'] = '2', ['٣'] = '3', ['٤'] = '4',
        ['٥'] = '5', ['٦'] = '6', ['٧'] = '7', ['٨'] = '8', ['٩'] = '9'
    };

    /// <summary>تبدیل ارقام فارسی و عربی به لاتین و یکسان‌سازی حروف عربی.</summary>
    public static string Normalize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var builder = new StringBuilder(value.Length);

        var normalizedInput = value.Trim().Replace("ایران", string.Empty, StringComparison.OrdinalIgnoreCase);

        foreach (var character in normalizedInput)
        {
            if (char.IsWhiteSpace(character) || character is '-' or 'ـ')
            {
                continue;
            }

            var normalized = DigitMap.TryGetValue(character, out var digit)
                ? digit
                : character switch
                {
                    'ي' or 'ى' => 'ی',
                    'ك' => 'ک',
                    _ => character
                };

            builder.Append(normalized);
        }

        var result = builder.ToString();
        if (!PlatePattern().IsMatch(result))
        {
            throw new ArgumentException("ساختار پلاک ملی ایران معتبر نیست.", nameof(value));
        }

        return result;
    }

    /// <summary>اعتبارسنجی بدون پرتاب استثنا.</summary>
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            _ = Normalize(value);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>الگوی ساختار پلاک ملی خودرو.</summary>
    [GeneratedRegex(@"^\d{2}(?:الف|[ابپتثجچحخدذرزژسشصضطظعغفقکگلمنوهی])\d{3}\d{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex PlatePattern();
}
