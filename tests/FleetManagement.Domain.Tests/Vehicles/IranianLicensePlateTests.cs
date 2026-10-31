using FleetManagement.Domain.Vehicles;

namespace FleetManagement.Domain.Tests.Vehicles;

/// <summary>
/// آزمون نرمال‌سازی و اعتبارسنجی پلاک ملی ایران.
/// </summary>
public sealed class IranianLicensePlateTests
{
    [Theory]
    [InlineData("۱۲ ب ۳۴۵ ۶۷", "12ب34567")]
    [InlineData("12ب34567", "12ب34567")]
    [InlineData("۱۲-ب-۳۴۵-۶۷", "12ب34567")]
    public void پلاک_معتبر_به_قالب_یکسان_تبدیل_می_شود(string input, string expected)
    {
        Assert.Equal(expected, IranianLicensePlate.Normalize(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1234567")]
    [InlineData("12A34567")]
    [InlineData("12ب3456")]
    public void پلاک_نامعتبر_رد_می_شود(string input)
    {
        Assert.False(IranianLicensePlate.IsValid(input));
    }
}
