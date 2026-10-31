using FleetManagement.Domain.Sessions;

namespace FleetManagement.Domain.Tests.Sessions;

/// <summary>
/// آزمون قواعد عمر مطلق، مهلت بیکاری و ثبت تلاش ناموفق.
/// </summary>
public sealed class UserSessionTests
{
    [Fact]
    public void نشست_پس_از_سی_دقیقه_عدم_فعالیت_منقضی_می_شود()
    {
        // Arrange
        var session = new UserSession(
            "a.rezaei",
            UserSessionLoginMethod.DirectoryPassword,
            true,
            "10.0.0.10");

        // Act
        var expired = session.ExpireIfNeeded(
            session.LastActivityAtUtc.AddMinutes(31),
            TimeSpan.FromMinutes(30),
            TimeSpan.FromHours(8));

        // Assert
        Assert.True(expired);
        Assert.Equal(UserSessionStatus.IdleExpired, session.Status);
        Assert.NotNull(session.EndedAtUtc);
    }

    [Fact]
    public void سقف_هشت_ساعته_بر_مهلت_بیکاری_اولویت_دارد()
    {
        // Arrange
        var session = new UserSession(
            "a.rezaei",
            UserSessionLoginMethod.WindowsIntegrated,
            true,
            "10.0.0.11");

        // Act
        var expired = session.ExpireIfNeeded(
            session.LoginAtUtc.AddHours(8).AddMinutes(1),
            TimeSpan.FromMinutes(30),
            TimeSpan.FromHours(8));

        // Assert
        Assert.True(expired);
        Assert.Equal(UserSessionStatus.AbsoluteExpired, session.Status);
    }

    [Fact]
    public void فعالیت_جدید_مهلت_بیکاری_را_به_روز_می_کند()
    {
        // Arrange
        var session = new UserSession(
            "a.rezaei",
            UserSessionLoginMethod.LocalEmergencyAdministrator,
            true,
            "10.0.0.12");

        var activityTime = session.LastActivityAtUtc.AddMinutes(20);

        // Act
        session.Touch(activityTime);
        var expired = session.ExpireIfNeeded(
            activityTime.AddMinutes(20),
            TimeSpan.FromMinutes(30),
            TimeSpan.FromHours(8));

        // Assert
        Assert.Equal(activityTime, session.LastActivityAtUtc);
        Assert.False(expired);
        Assert.Equal(UserSessionStatus.Active, session.Status);
    }

    [Fact]
    public void تلاش_ورود_ناموفق_به_عنوان_نشست_فعال_ثبت_نمی_شود()
    {
        // Arrange و Act
        var session = new UserSession(
            "unknown.user",
            UserSessionLoginMethod.DirectoryPassword,
            false,
            "10.0.0.13",
            "InvalidCredentials");

        // Assert
        Assert.Equal(UserSessionStatus.Failed, session.Status);
        Assert.Equal("InvalidCredentials", session.Reason);
        Assert.NotNull(session.EndedAtUtc);
    }
}
