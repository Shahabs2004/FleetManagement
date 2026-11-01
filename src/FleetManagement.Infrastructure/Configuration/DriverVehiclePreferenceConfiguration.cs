namespace FleetManagement.Infrastructure.Configuration;

/// <summary>نگاشت خودروی پیشنهادی پیش‌فرض هر راننده.</summary>
public sealed class DriverVehiclePreferenceConfiguration
{
    /// <summary>فهرست نگاشت راننده به خودروی پیش‌فرض.</summary>
    public List<DriverVehicleDefaultMapping> Mappings { get; set; } = [];
}

/// <summary>یک نگاشت مستقل از اطلاعات پایه راننده.</summary>
public sealed class DriverVehicleDefaultMapping
{
    /// <summary>شناسه راننده.</summary>
    public Guid DriverId { get; set; }

    /// <summary>شناسه خودروی پیشنهادی.</summary>
    public Guid VehicleId { get; set; }
}
