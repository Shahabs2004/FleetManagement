using FleetManagement.Domain.Drivers;

namespace FleetManagement.Domain.Tests.Drivers;

/// <summary>
/// آزمون قواعد اصلی موجودیت راننده.
/// </summary>
public sealed class DriverTests
{
    [Fact]
    public void راننده_فعال_و_بایگانی_نشده_قابل_تخصیص_است()
    {
        // Arrange
        var driver = new Driver("علی", "رضایی", "09120000000");

        // Act
        var canAssign = driver.CanBeAssignedToMission();

        // Assert
        Assert.True(canAssign);
    }

    [Theory]
    [InlineData(DriverEmploymentStatus.OnLeave)]
    [InlineData(DriverEmploymentStatus.EmploymentTerminated)]
    [InlineData(DriverEmploymentStatus.Inactive)]
    public void راننده_با_وضعیت_غیرفعال_قابل_تخصیص_نیست(DriverEmploymentStatus status)
    {
        // Arrange
        var driver = new Driver("علی", "رضایی", "09120000000");
        driver.ChangeEmploymentStatus(status);

        // Act
        var canAssign = driver.CanBeAssignedToMission();

        // Assert
        Assert.False(canAssign);
    }

    [Fact]
    public void راننده_بایگانی_شده_قابل_تخصیص_نیست()
    {
        // Arrange
        var driver = new Driver("علی", "رضایی", "09120000000");
        driver.Archive();

        // Act
        var canAssign = driver.CanBeAssignedToMission();

        // Assert
        Assert.False(canAssign);
    }

    [Fact]
    public void راننده_غیرفعال_قابل_بازگردانی_از_بایگانی_نیست()
    {
        // Arrange
        var driver = new Driver("علی", "رضایی", "09120000000");
        driver.ChangeEmploymentStatus(DriverEmploymentStatus.OnLeave);
        driver.Archive();

        // Act و Assert
        Assert.Throws<InvalidOperationException>(() => driver.Reactivate());
    }
}
