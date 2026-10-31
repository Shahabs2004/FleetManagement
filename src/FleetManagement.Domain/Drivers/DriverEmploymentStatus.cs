namespace FleetManagement.Domain.Drivers;

/// <summary>
/// وضعیت استخدامی راننده در سازمان.
/// </summary>
public enum DriverEmploymentStatus
{
    /// <summary>راننده فعال و قابل تخصیص است.</summary>
    Active = 1,

    /// <summary>راننده در مرخصی است و مأموریت جدید نمی‌گیرد.</summary>
    OnLeave = 2,

    /// <summary>همکاری راننده با سازمان خاتمه یافته است.</summary>
    EmploymentTerminated = 3,

    /// <summary>راننده غیرفعال شده است.</summary>
    Inactive = 4
}
