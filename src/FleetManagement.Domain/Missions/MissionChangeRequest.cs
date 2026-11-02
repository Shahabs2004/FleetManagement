using FleetManagement.Domain.Common;

namespace FleetManagement.Domain.Missions;

/// <summary>
/// درخواست مستقل تغییر اطلاعات مأموریت؛ تا زمان تأیید، مقادیر اصلی دست‌نخورده می‌مانند.
/// </summary>
public sealed class MissionChangeRequest : Entity
{
    private MissionChangeRequest()
    {
        RequesterUsername = string.Empty;
        Reason = string.Empty;
        ProposedValuesJson = string.Empty;
    }

    /// <summary>شناسه مأموریت هدف.</summary>
    public Guid MissionId { get; private set; }

    /// <summary>نام کاربری درخواست‌کننده تغییر.</summary>
    public string RequesterUsername { get; private set; }

    /// <summary>علت درخواست تغییر.</summary>
    public string Reason { get; private set; }

    /// <summary>مقادیر پیشنهادی به‌صورت JSON.</summary>
    public string ProposedValuesJson { get; private set; }

    /// <summary>وضعیت درخواست تغییر.</summary>
    public MissionChangeRequestStatus Status { get; private set; }

    /// <summary>نام نخستین بازبین؛ درخواست تا تعیین تکلیف برای او قفل می‌شود.</summary>
    public string? ReviewerUsername { get; private set; }

    /// <summary>زمان آغاز بررسی.</summary>
    public DateTimeOffset? ReviewStartedAtUtc { get; private set; }

    /// <summary>زمان تصمیم نهایی.</summary>
    public DateTimeOffset? ReviewedAtUtc { get; private set; }

    /// <summary>علت رد درخواست در صورت رد شدن.</summary>
    public string? DecisionReason { get; private set; }

    /// <summary>زمان ایجاد درخواست.</summary>
    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>ایجاد درخواست تغییر جدید.</summary>
    public MissionChangeRequest(
        Guid missionId,
        string requesterUsername,
        string reason,
        string proposedValuesJson)
    {
        if (missionId == Guid.Empty) throw new ArgumentException("شناسه مأموریت معتبر نیست.", nameof(missionId));
        MissionId = missionId;
        RequesterUsername = Required(requesterUsername, 64, nameof(requesterUsername));
        Reason = Required(reason, 1000, nameof(reason));
        ProposedValuesJson = Required(proposedValuesJson, 12000, nameof(proposedValuesJson));
        Status = MissionChangeRequestStatus.Pending;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>شروع بررسی و قفل‌کردن درخواست برای نخستین بازبین.</summary>
    public void StartReview(string reviewer, DateTimeOffset nowUtc)
    {
        if (Status != MissionChangeRequestStatus.Pending)
        {
            throw new InvalidOperationException("این درخواست تغییر قبلاً وارد مرحله بررسی شده است.");
        }

        ReviewerUsername = Required(reviewer, 64, nameof(reviewer));
        ReviewStartedAtUtc = nowUtc;
        Status = MissionChangeRequestStatus.UnderReview;
    }

    /// <summary>تأیید درخواست توسط همان بازبینی که بررسی را آغاز کرده است.</summary>
    public void Approve(string reviewer, DateTimeOffset nowUtc)
    {
        EnsureReviewer(reviewer);
        Status = MissionChangeRequestStatus.Approved;
        ReviewedAtUtc = nowUtc;
        DecisionReason = null;
    }

    /// <summary>رد درخواست توسط همان بازبینی با ثبت علت.</summary>
    public void Reject(string reviewer, string reason, DateTimeOffset nowUtc)
    {
        EnsureReviewer(reviewer);
        DecisionReason = Required(reason, 1000, nameof(reason));
        Status = MissionChangeRequestStatus.Rejected;
        ReviewedAtUtc = nowUtc;
    }

    private void EnsureReviewer(string reviewer)
    {
        if (Status != MissionChangeRequestStatus.UnderReview
            || !string.Equals(ReviewerUsername, reviewer, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("فقط بازبین آغازکننده می‌تواند این درخواست را تعیین تکلیف کند.");
        }
    }

    private static string Required(string value, int maxLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maxLength) throw new ArgumentOutOfRangeException(parameterName);
        return normalized;
    }
}
