namespace FleetManagement.Application.Auditing;

/// <summary>
/// قرارداد ثبت ممیزی سناریوهای درخواست تغییر مأموریت.
/// </summary>
public interface IMissionChangeAuditWriter
{
    /// <summary>ثبت رویداد ممیزی مرتبط با درخواست تغییر.</summary>
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
