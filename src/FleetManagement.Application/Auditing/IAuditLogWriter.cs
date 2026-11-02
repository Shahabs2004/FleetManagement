namespace FleetManagement.Application.Auditing;

/// <summary>
/// قرارداد ثبت رویدادهای ممیزی برای سناریوهای کاربردی.
/// </summary>
public interface IAuditLogWriter
{
    /// <summary>
    /// یک رویداد ممیزی را بدون وابستگی به شیوه ذخیره‌سازی ثبت می‌کند.
    /// </summary>
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
