using FleetManagement.Domain.Users;

namespace FleetManagement.Domain.Tests.Users;

/// <summary>
/// آزمون قواعد پروفایل کاربر سازمانی و همگام‌سازی Active Directory.
/// </summary>
public sealed class ApplicationUserTests
{
    [Fact]
    public void همگام_سازی_اطلاعات_کاربر_را_به_روز_می_کند()
    {
        // Arrange
        var user = new ApplicationUser(
            "a.rezaei",
            "علی",
            "رضایی",
            "امور مالی",
            "CN=Ali Rezaei,OU=Finance,DC=corp,DC=local");

        // Act
        user.SynchronizeFromDirectory(
            "علی‌اکبر",
            "رضایی",
            "امور مشتریان",
            "CN=Ali Rezaei,OU=Customers,DC=corp,DC=local");

        // Assert
        Assert.Equal("علی‌اکبر", user.FirstName);
        Assert.Equal("امور مشتریان", user.OrganizationalUnit);
        Assert.Contains("OU=Customers", user.DirectoryDistinguishedName);
    }

    [Fact]
    public void واحد_سازمانی_دستی_با_همگام_سازی_AD_بازنویسی_نمی_شود()
    {
        // Arrange
        var user = new ApplicationUser(
            "a.rezaei",
            "علی",
            "رضایی",
            "امور مالی",
            "CN=Ali Rezaei,OU=Finance,DC=corp,DC=local");

        user.OverrideOrganizationalUnit("مدیریت مرکزی");

        // Act
        user.SynchronizeFromDirectory(
            "علی",
            "رضایی",
            "امور مشتریان",
            "CN=Ali Rezaei,OU=Customers,DC=corp,DC=local");

        // Assert
        Assert.Equal("مدیریت مرکزی", user.OrganizationalUnit);
        Assert.True(user.IsOrganizationalUnitManuallyOverridden);
    }

    [Fact]
    public void غیرفعال_سازی_داخلی_با_همگام_سازی_AD_رفع_نمی_شود()
    {
        // Arrange
        var user = new ApplicationUser(
            "a.rezaei",
            "علی",
            "رضایی",
            "امور مالی",
            "CN=Ali Rezaei,OU=Finance,DC=corp,DC=local");

        user.Disable();

        // Act
        user.SynchronizeFromDirectory(
            "علی",
            "رضایی",
            "امور مالی",
            "CN=Ali Rezaei,OU=Finance,DC=corp,DC=local");

        // Assert
        Assert.True(user.IsDisabled);
    }

    [Fact]
    public void فقط_اقدام_صریح_مدیر_حساب_داخلی_را_فعال_می_کند()
    {
        // Arrange
        var user = new ApplicationUser(
            "a.rezaei",
            "علی",
            "رضایی",
            "امور مالی",
            "CN=Ali Rezaei,OU=Finance,DC=corp,DC=local");

        user.Disable();

        // Act
        user.Enable();

        // Assert
        Assert.False(user.IsDisabled);
    }
}
