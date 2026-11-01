namespace FleetManagement.Application.Auditing;

/// <summary>
/// قرارداد ثبت ممیزی سناریوهای چرخه عمر مأموریت.
/// </summary>
public interface IMissionAuditWriter
{
    /// <summary>ثبت رویداد ممیزی مأموریت.</summary>
    Task<Guid> RecordAsync(
        string actorUsername,
        string action,
        string entityType,
        string? entityId,
        string outcome,
        string? reason = null,
        string? beforeJson = null,
        string? afterJson = null,
        string? clientIpAddress = null,
        CancellationToken cancellationToken = default);
}
