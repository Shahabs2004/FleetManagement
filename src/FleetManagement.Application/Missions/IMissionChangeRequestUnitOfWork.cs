using FleetManagement.Domain.Missions;

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

    /// <summary>افزودن درخواست جدید به واحد کار جاری.</summary>
    void Add(MissionChangeRequest request);
}
