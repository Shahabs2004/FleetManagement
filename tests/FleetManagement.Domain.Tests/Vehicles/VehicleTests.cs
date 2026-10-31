using FleetManagement.Domain.Vehicles;

namespace FleetManagement.Domain.Tests.Vehicles;

/// <summary>
/// آزمون قواعد وضعیت و تخصیص خودرو.
/// </summary>
public sealed class VehicleTests
{
    [Fact]
    public void مأموریت_فعال_وضعیت_نمایشی_را_در_مأموریت_می_کند()
    {
        // Arrange
        var vehicle = new Vehicle("ایران‌خودرو", "سمند", "۱۲ الف ۳۴۵ ایران ۶۷");

        // Act
        var status = vehicle.GetEffectiveStatus(hasActiveMission: true);

        // Assert
        Assert.Equal(VehicleEffectiveStatus.InMission, status);
    }

    [Fact]
    public void پس_از_پایان_مأموریت_وضعیت_دستی_قبلی_حفظ_می_شود()
    {
        // Arrange
        var vehicle = new Vehicle("ایران‌خودرو", "سمند", "۱۲ الف ۳۴۵ ایران ۶۷");
        vehicle.ChangeManualStatus(ManualVehicleStatus.RepairShop, hasActiveMission: false);

        // Act
        var status = vehicle.GetEffectiveStatus(hasActiveMission: false);

        // Assert
        Assert.Equal(VehicleEffectiveStatus.RepairShop, status);
    }

    [Fact]
    public void خودرو_دارای_مأموریت_فعال_قابل_انتقال_به_تعمیرگاه_نیست()
    {
        // Arrange
        var vehicle = new Vehicle("ایران‌خودرو", "سمند", "۱۲ الف ۳۴۵ ایران ۶۷");

        // Act و Assert
        Assert.Throws<InvalidOperationException>(
            () => vehicle.ChangeManualStatus(ManualVehicleStatus.RepairShop, hasActiveMission: true));
    }

    [Fact]
    public void خودرو_تعمیرگاهی_قابل_تخصیص_نیست()
    {
        // Arrange
        var vehicle = new Vehicle("ایران‌خودرو", "سمند", "۱۲ الف ۳۴۵ ایران ۶۷");
        vehicle.ChangeManualStatus(ManualVehicleStatus.RepairShop, hasActiveMission: false);

        // Act
        var canAssign = vehicle.CanBeAssignedToMission(hasActiveMission: false);

        // Assert
        Assert.False(canAssign);
    }
}
