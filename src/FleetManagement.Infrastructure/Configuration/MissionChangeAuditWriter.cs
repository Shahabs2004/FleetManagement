using FleetManagement.Application.Auditing;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// آداپتور ممیزی سناریوهای تغییر که ثبت را به سرویس ممیزی مشترک واگذار می‌کند.
/// </summary>
public sealed class MissionChangeAuditWriter(AuditLogService auditLog) : IMissionChangeAuditWriter
{
    /// <inheritdoc />
    public Task<Guid> RecordAsync(
        string actorUsername,
        string action,
        string entityType,
        string? entityId,
        string outcome,
        string? reason = null,
        string? beforeJson = null,
        string? afterJson = null,
        string? clientIpAddress = null,
        CancellationToken cancellationToken = default) =>
        auditLog.RecordAsync(
            actorUsername,
            action,
            entityType,
            entityId,
            outcome,
            reason,
            beforeJson,
            afterJson,
            clientIpAddress,
            cancellationToken);
}
