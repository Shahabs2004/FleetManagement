namespace FleetManagement.Domain.Missions;

/// <summary>
/// وضعیت درخواست تغییر مأموریت پس از تأیید.
/// </summary>
public enum MissionChangeRequestStatus
{
    /// <summary>در انتظار بررسی.</summary>
    Pending = 1,

    /// <summary>بررسی توسط نخستین بازبین آغاز شده است.</summary>
    UnderReview = 2,

    /// <summary>تغییرات تأیید و اعمال شده‌اند.</summary>
    Approved = 3,

    /// <summary>درخواست تغییر رد شده است.</summary>
    Rejected = 4
}
