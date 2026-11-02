using FleetManagement.Domain.Missions;

namespace FleetManagement.Application.Missions;

/// <summary>
/// قرارداد خواندن و جست‌وجوی مأموریت‌ها.
/// </summary>
public interface IMissionQueryService
{
    Task<IReadOnlyList<Mission>> GetTimelineAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        int take = 1000,
        CancellationToken cancellationToken = default);

    Task<Mission> GetByIdAsync(Guid missionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Mission>> SearchAsync(
        string? requesterUsername = null,
        MissionStatus? status = null,
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        int take = 200,
        CancellationToken cancellationToken = default);
}
