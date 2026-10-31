using FleetManagement.Domain.Common;

namespace FleetManagement.Domain.Notifications;

/// <summary>
/// اعلان داخلی کاربر با سابقه دائمی و وضعیت خوانده‌شدن.
/// </summary>
public sealed class UserNotification : Entity
{
    private UserNotification()
    {
        RecipientUsername = string.Empty;
        Title = string.Empty;
        Message = string.Empty;
        EventKey = string.Empty;
    }

    /// <summary>نام کاربری گیرنده اعلان.</summary>
    public string RecipientUsername { get; private set; }

    /// <summary>عنوان کوتاه اعلان.</summary>
    public string Title { get; private set; }

    /// <summary>متن اعلان.</summary>
    public string Message { get; private set; }

    /// <summary>کلید ثابت رویداد.</summary>
    public string EventKey { get; private set; }

    /// <summary>نوع رکورد مرتبط.</summary>
    public string? ReferenceType { get; private set; }

    /// <summary>شناسه رکورد مرتبط.</summary>
    public string? ReferenceId { get; private set; }

    /// <summary>زمان ایجاد اعلان.</summary>
    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>زمان خوانده‌شدن اعلان.</summary>
    public DateTimeOffset? ReadAtUtc { get; private set; }

    /// <summary>آیا اعلان خوانده شده است؟</summary>
    public bool IsRead => ReadAtUtc.HasValue;

    /// <summary>ایجاد اعلان جدید.</summary>
    public UserNotification(
        string recipientUsername,
        string title,
        string message,
        string eventKey,
        string? referenceType = null,
        string? referenceId = null)
    {
        RecipientUsername = Required(recipientUsername, 64, nameof(recipientUsername));
        Title = Required(title, 200, nameof(title));
        Message = Required(message, 1000, nameof(message));
        EventKey = Required(eventKey, 80, nameof(eventKey));
        ReferenceType = Optional(referenceType, 100);
        ReferenceId = Optional(referenceId, 100);
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>علامت‌گذاری اعلان به‌عنوان خوانده‌شده.</summary>
    public void MarkAsRead(DateTimeOffset nowUtc)
    {
        ReadAtUtc ??= nowUtc;
    }

    private static string Required(string value, int maxLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }

        return normalized;
    }

    private static string? Optional(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) return null;
        if (normalized.Length > maxLength) throw new ArgumentOutOfRangeException(nameof(value));
        return normalized;
    }
}
