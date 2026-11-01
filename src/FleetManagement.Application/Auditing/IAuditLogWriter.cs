namespace FleetManagement.Application.Auditing;

/// <summary>
/// قرارداد ثبت رویدادهای ممیزی؛ جزئیات ذخیره‌سازی در لایه زیرساخت باقی می‌ماند.
/// </summary>
public interface IAuditLogWriter
{
    /// <summary>ثبت یک رویداد ممیزی غیرقابل‌ویرایش.</summary>
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
