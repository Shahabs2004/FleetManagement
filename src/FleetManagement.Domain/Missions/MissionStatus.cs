namespace FleetManagement.Domain.Missions;

/// <summary>
/// وضعیت‌های چرخه عمر درخواست و مأموریت.
/// </summary>
public enum MissionStatus
{
    /// <summary>در انتظار بررسی اولیه</summary>
    Pending = 1,

    /// <summary>در حال بررسی توسط مسئول ناوگان</summary>
    UnderReview = 2,

    /// <summary>تأیید شده و در انتظار شروع</summary>
    Confirmed = 3,

    /// <summary>مأموریت در حال اجرا</summary>
    Running = 4,

    /// <summary>مأموریت با موفقیت پایان یافته است</summary>
    Completed = 5,

    /// <summary>درخواست یا مأموریت لغو شده است</summary>
    Cancelled = 6,

    /// <summary>درخواست رد شده است</summary>
    Rejected = 7,

    /// <summary>درخواست انصراف در انتظار تصمیم است</summary>
    WithdrawalPending = 8
}
