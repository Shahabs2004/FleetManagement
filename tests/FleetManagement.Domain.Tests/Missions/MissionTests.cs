using FleetManagement.Domain.Missions;

namespace FleetManagement.Domain.Tests.Missions;

/// <summary>
/// آزمون قواعد چرخه عمر، دقت ساعت و تقسیم مأموریت.
/// </summary>
public sealed class MissionTests
{
    private static readonly DateTimeOffset PlannedStart = new(2026, 10, 31, 10, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset PlannedEnd = new(2026, 10, 31, 14, 30, 0, TimeSpan.Zero);

    private static Mission CreateMission() => new(
        "1405-001",
        "مأموریت آزمایشی",
        "a.user",
        "کاربر آزمایشی",
        "09120000000",
        "واحد فناوری",
        "رایانه",
        MissionType.Passenger,
        "جلسه کاری",
        "ساختمان مرکزی",
        "ساختمان شماره دو",
        [],
        PlannedStart,
        PlannedEnd);

    [Fact]
    public void درخواست_جدید_در_وضعیت_انتظار_ثبت_می_شود()
    {
        var mission = CreateMission();

        Assert.Equal(MissionStatus.Pending, mission.Status);
        Assert.Null(mission.DriverId);
        Assert.Null(mission.VehicleId);
    }

    [Fact]
    public void درخواست_با_زمان_غیر_ساعتی_رد_می_شود()
    {
        var invalidStart = PlannedStart.AddMinutes(1);

        Assert.Throws<ArgumentException>(() => new Mission(
            "1405-002", "عنوان", "a.user", "کاربر", "09120000000",
            "واحد", "گروه", MissionType.Freight, "هدف", "مبدأ", "مقصد",
            [], invalidStart, PlannedEnd));
    }

    [Fact]
    public void درخواست_در_انتظار_منابع_رزرو_نمی_کند()
    {
        var mission = CreateMission();

        Assert.Null(mission.DriverId);
        Assert.Null(mission.VehicleId);
        Assert.Equal(MissionStatus.Pending, mission.Status);
    }

    [Fact]
    public void تقسیم_بخش_اول_را_می_بندد_و_مسافت_را_محاسبه_می_کند()
    {
        var mission = CreateMission();
        var driverId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        mission.Approve(driverId, vehicleId, "operator", PlannedStart.AddMinutes(-30));
        mission.StartAutomatically(PlannedStart);
        mission.SetOdometerStart(100);

        var splitAt = PlannedStart.AddHours(1);
        mission.CloseFirstSegmentForSplit(splitAt, 125, "تعویض راننده", "operator");

        Assert.Equal(MissionStatus.Completed, mission.Status);
        Assert.Equal(splitAt, mission.ActualEndAtUtc);
        Assert.Equal(splitAt, mission.PlannedEndAtUtc);
        Assert.Equal(25, mission.DistanceKm);
        Assert.Equal("operator", mission.SplitBy);
    }

    [Fact]
    public void تقسیم_به_زمان_پس_از_پایان_برنامه‌ریزی_شده_مجاز_نیست()
    {
        var mission = CreateMission();
        mission.Approve(Guid.NewGuid(), Guid.NewGuid(), "operator", PlannedStart.AddMinutes(-30));
        mission.StartAutomatically(PlannedStart);
        mission.SetOdometerStart(100);

        Assert.Throws<ArgumentException>(() =>
            mission.CloseFirstSegmentForSplit(PlannedEnd, 120, "خرابی خودرو", "operator"));
    }
}
