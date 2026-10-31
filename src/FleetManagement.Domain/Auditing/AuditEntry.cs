using FleetManagement.Domain.Common;

namespace FleetManagement.Domain.Auditing;

/// <summary>
/// رویداد تغییرناپذیر ممیزی برای ردیابی عملیات مهم سامانه.
/// </summary>
public sealed class AuditEntry : Entity
{
    private AuditEntry()
    {
        ActorUsername = string.Empty;
        Action = string.Empty;
        EntityType = string.Empty;
        Outcome = string.Empty;
    }

    /// <summary>نام کاربری عامل انجام عملیات.</summary>
    public string ActorUsername { get; private set; }

    /// <summary>کلید ثابت عملیات مانند Create یا Approve.</summary>
    public string Action { get; private set; }

    /// <summary>نوع موجودیت تحت تأثیر.</summary>
    public string EntityType { get; private set; }

    /// <summary>شناسه رکورد تحت تأثیر در صورت وجود.</summary>
    public string? EntityId { get; private set; }

    /// <summary>زمان ثبت رویداد بر اساس UTC.</summary>
    public DateTimeOffset OccurredAtUtc { get; private set; }

    /// <summary>نتیجه عملیات مانند Success یا Rejected.</summary>
    public string Outcome { get; private set; }

    /// <summary>علت یا توضیح ثبت‌شده توسط عامل.</summary>
    public string? Reason { get; private set; }

    /// <summary>تصویر قبل از تغییر به‌صورت JSON پاک‌سازی‌شده.</summary>
    public string? BeforeJson { get; private set; }

    /// <summary>تصویر بعد از تغییر به‌صورت JSON پاک‌سازی‌شده.</summary>
    public string? AfterJson { get; private set; }

    /// <summary>نشانی IP مبدأ در صورت وجود.</summary>
    public string? ClientIpAddress { get; private set; }

    /// <summary>
    /// ایجاد رویداد ممیزی پس از اعتبارسنجی اندازه و محتوای ورودی.
    /// </summary>
    public AuditEntry(
        string actorUsername,
        string action,
        string entityType,
        string? entityId,
        string outcome,
        string? reason,
        string? beforeJson,
        string? afterJson,
        string? clientIpAddress)
    {
        ActorUsername = Required(actorUsername, 64, nameof(actorUsername));
        Action = Required(action, 80, nameof(action));
        EntityType = Required(entityType, 100, nameof(entityType));
        EntityId = Optional(entityId, 100);
        Outcome = Required(outcome, 40, nameof(outcome));
        Reason = Optional(reason, 1000);
        BeforeJson = Optional(beforeJson, 12000);
        AfterJson = Optional(afterJson, 12000);
        ClientIpAddress = Optional(clientIpAddress, 45);
        OccurredAtUtc = DateTimeOffset.UtcNow;
    }

    private static string Required(string value, int maxLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentOutOfRangeException(parameterName, $"حداکثر طول مجاز {maxLength} نویسه است.");
        }

        return normalized;
    }

    private static string? Optional(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (normalized.Length > maxLength)
        {
            throw new ArgumentOutOfRangeException(nameof(value), $"حداکثر طول مجاز {maxLength} نویسه است.");
        }

        return normalized;
    }
}
