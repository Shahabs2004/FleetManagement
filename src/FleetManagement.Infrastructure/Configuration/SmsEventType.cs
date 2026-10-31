namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// رویدادهایی که طبق تنظیمات سامانه می‌توانند پیامک ایجاد کنند.
/// </summary>
public enum SmsEventType
{
    /// <summary>تأیید مأموریت.</summary>
    MissionApproved = 1,

    /// <summary>رد درخواست مأموریت.</summary>
    MissionRejected = 2,

    /// <summary>تغییر زمان یا جزئیات مأموریت.</summary>
    MissionChanged = 3,

    /// <summary>تغییر راننده یا خودرو.</summary>
    DriverOrVehicleChanged = 4,

    /// <summary>لغو مأموریت.</summary>
    MissionCancelled = 5,

    /// <summary>تأیید انصراف درخواست‌کننده.</summary>
    WithdrawalApproved = 6,

    /// <summary>رد انصراف درخواست‌کننده.</summary>
    WithdrawalRejected = 7,

    /// <summary>یادآوری پیش از شروع مأموریت.</summary>
    MissionStartReminder = 8,

    /// <summary>پایان مأموریت.</summary>
    MissionCompleted = 9
}
