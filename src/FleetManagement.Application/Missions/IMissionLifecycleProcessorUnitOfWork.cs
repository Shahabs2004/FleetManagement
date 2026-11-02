using FleetManagement.Domain.Missions;

namespace FleetManagement.Application.Missions;

/// <summary>پرس‌وجوی داده‌ای موردنیاز پردازش زمان‌بندی‌شده مأموریت‌ها.</summary>
public interface IMissionLifecycleProcessorUnitOfWork
{
    Task<IReadOnlyList<Mission>> GetDueMissionsAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);
}
