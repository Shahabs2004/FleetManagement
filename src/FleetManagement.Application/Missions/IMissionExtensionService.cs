namespace FleetManagement.Application.Missions;

/// <summary>
/// قرارداد کاربردی تمدید مأموریت.
/// </summary>
public interface IMissionExtensionService
{
    Task ExtendAsync(Guid missionId, DateTimeOffset newEndAtUtc, string reason, string actor, CancellationToken cancellationToken = default);
}
