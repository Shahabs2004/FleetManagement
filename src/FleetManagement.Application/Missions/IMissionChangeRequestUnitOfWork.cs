using FleetManagement.Domain.Missions;
using FleetManagement.Domain.Drivers;
using FleetManagement.Domain.Vehicles;

namespace FleetManagement.Application.Missions;

/// <summary>
/// عملیات داده‌ای لازم در یک تراکنش تغییر مأموریت.
/// </summary>
public interface IMissionChangeRequestUnitOfWork
{
    /// <summary>دریافت مأموریت برای بررسی قواعد ثبت یا رد درخواست.</summary>
    Task<Mission?> GetMissionAsync(Guid missionId, CancellationToken cancellationToken = default);

    /// <summary>دریافت درخواست تغییر برای اعمال انتقال وضعیت.</summary>
    Task<MissionChangeRequest?> GetChangeRequestAsync(Guid changeRequestId, CancellationToken cancellationToken = default);

    /// <summary>بررسی وجود درخواست تغییر باز برای مأموریت.</summary>
    Task<bool> HasOpenRequestAsync(Guid missionId, CancellationToken cancellationToken = default);

    /// <summary>دریافت نام مکان‌های تأییدشده از مجموعه پیشنهادی.</summary>
    Task<IReadOnlyList<string>> GetApprovedLocationNamesAsync(
        IReadOnlyCollection<string> locationNames,
        CancellationToken cancellationToken = default);

    /// <summary>دریافت راننده برای اعتبارسنجی تخصیص.</summary>
    Task<Driver?> GetDriverAsync(Guid driverId, CancellationToken cancellationToken = default);

    /// <summary>دریافت خودرو برای اعتبارسنجی تخصیص.</summary>
    Task<Vehicle?> GetVehicleAsync(Guid vehicleId, CancellationToken cancellationToken = default);

    /// <summary>بررسی تعارض منابع در بازه پیشنهادی.</summary>
    Task<bool> HasResourceConflictAsync(
        Mission current,
        Guid driverId,
        Guid vehicleId,
        TimeSpan gap,
        DateTimeOffset proposedEnd,
        DateTimeOffset proposedStart,
        CancellationToken cancellationToken = default);

    /// <summary>آخرین کیلومترشمار پایان ثبت‌شده برای خودرو.</summary>
    Task<long?> GetLastOdometerAsync(Guid vehicleId, CancellationToken cancellationToken = default);

    /// <summary>افزودن درخواست جدید به واحد کار جاری.</summary>
    void Add(MissionChangeRequest request);
}
