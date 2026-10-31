namespace FleetManagement.Domain.Missions;

/// <summary>
/// دلایل استاندارد تقسیم مأموریت.
/// </summary>
public enum MissionSplitReason
{
    /// <summary>خرابی خودرو.</summary>
    VehicleBreakdown = 1,

    /// <summary>تعویض راننده.</summary>
    DriverChange = 2,

    /// <summary>تعویض هم‌زمان راننده و خودرو.</summary>
    DriverAndVehicleChange = 3,

    /// <summary>سایر موارد همراه با توضیح اجباری.</summary>
    Other = 4
}
