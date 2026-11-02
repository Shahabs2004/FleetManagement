using FleetManagement.Domain.Missions;

namespace FleetManagement.Application.Missions;

/// <summary>عملیات داده‌ای موردنیاز گردش انصراف و لغو مأموریت.</summary>
public interface IMissionWithdrawalUnitOfWork
{
    Task<Mission?> GetMissionAsync(Guid missionId, CancellationToken cancellationToken = default);
}
