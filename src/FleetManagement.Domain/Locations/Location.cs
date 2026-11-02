using FleetManagement.Domain.Common;

namespace FleetManagement.Domain.Locations;

/// <summary>
/// مکان مشترک قابل انتخاب به‌عنوان مبدأ، مقصد یا توقف میانی مأموریت.
/// </summary>
public sealed class Location : Entity
{
    private Location()
    {
        Name = string.Empty;
        ProposedBy = string.Empty;
    }

    /// <summary>نام مکان؛ مطابق نیاز پروژه فیلد دیگری نگهداری نمی‌شود.</summary>
    public string Name { get; private set; }

    /// <summary>وضعیت تأیید مکان.</summary>
    public LocationStatus Status { get; private set; }

    /// <summary>نام کاربری پیشنهاددهنده.</summary>
    public string ProposedBy { get; private set; }

    /// <summary>نام کاربری مدیر بررسی‌کننده.</summary>
    public string? ReviewedBy { get; private set; }

    /// <summary>علت رد پیشنهاد.</summary>
    public string? ReviewReason { get; private set; }

    /// <summary>زمان ثبت پیشنهاد.</summary>
    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>زمان آخرین تصمیم مدیر.</summary>
    public DateTimeOffset? ReviewedAtUtc { get; private set; }

    /// <summary>ایجاد پیشنهاد جدید مکان.</summary>
    public Location(string name, string proposer)
    {
        Name = NormalizeName(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(proposer);
        ProposedBy = proposer.Trim();
        Status = LocationStatus.Pending;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>تأیید پیشنهاد و قابل انتخاب کردن مکان.</summary>
    public void Approve(string reviewer, string? correctedName = null)
    {
        EnsurePending();
        if (!string.IsNullOrWhiteSpace(correctedName))
        {
            Name = NormalizeName(correctedName);
        }

        ReviewedBy = RequiredUser(reviewer);
        ReviewedAtUtc = DateTimeOffset.UtcNow;
        ReviewReason = null;
        Status = LocationStatus.Approved;
    }

    /// <summary>رد پیشنهاد مکان با ثبت علت.</summary>
    public void Reject(string reviewer, string reason)
    {
        EnsurePending();
        ReviewedBy = RequiredUser(reviewer);
        ReviewReason = RequiredText(reason, 500, nameof(reason));
        ReviewedAtUtc = DateTimeOffset.UtcNow;
        Status = LocationStatus.Rejected;
    }

    /// <summary>اصلاح نام مکان تأییدشده توسط مدیر.</summary>
    public void Rename(string name, string reviewer)
    {
        if (Status != LocationStatus.Approved)
        {
            throw new InvalidOperationException("فقط مکان تأییدشده قابل ویرایش است.");
        }

        Name = NormalizeName(name);
        ReviewedBy = RequiredUser(reviewer);
        ReviewedAtUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>غیرفعال‌سازی مکان؛ سوابق تاریخی حفظ می‌شوند.</summary>
    public void Deactivate(string reviewer)
    {
        if (Status != LocationStatus.Approved)
        {
            throw new InvalidOperationException("فقط مکان تأییدشده قابل غیرفعال‌سازی است.");
        }

        Status = LocationStatus.Inactive;
        ReviewedBy = RequiredUser(reviewer);
        ReviewedAtUtc = DateTimeOffset.UtcNow;
    }

    private void EnsurePending()
    {
        if (Status != LocationStatus.Pending)
        {
            throw new InvalidOperationException("فقط پیشنهاد در انتظار بررسی قابل تصمیم‌گیری است.");
        }
    }

    private static string NormalizeName(string name) => RequiredText(name, 200, nameof(name));

    private static string RequiredUser(string username) => RequiredText(username, 64, nameof(username));

    private static string RequiredText(string value, int maxLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }

        return normalized;
    }
}
