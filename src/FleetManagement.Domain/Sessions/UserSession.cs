using FleetManagement.Domain.Common;

namespace FleetManagement.Domain.Sessions;

/// <summary>
/// سابقه دائمی ورود، خروج، انقضای نشست یا تلاش ناموفق کاربر.
/// </summary>
public sealed class UserSession : Entity
{
    private UserSession()
    {
        // سازنده خصوصی برای EF Core
    }

    /// <summary>نام کاربری ثبت‌شده.</summary>
    public string Username { get; private set; } = string.Empty;

    /// <summary>روش ورود.</summary>
    public UserSessionLoginMethod LoginMethod { get; private set; }

    /// <summary>وضعیت سابقه نشست.</summary>
    public UserSessionStatus Status { get; private set; }

    /// <summary>زمان ورود یا تلاش ورود بر اساس UTC.</summary>
    public DateTimeOffset LoginAtUtc { get; private set; }

    /// <summary>آخرین زمان فعالیت نشست.</summary>
    public DateTimeOffset LastActivityAtUtc { get; private set; }

    /// <summary>زمان خروج یا انقضای نشست.</summary>
    public DateTimeOffset? EndedAtUtc { get; private set; }

    /// <summary>نشانی IP مبدأ در صورت قابل تشخیص بودن.</summary>
    public string? ClientIpAddress { get; private set; }

    /// <summary>علت ناموفق بودن ورود یا پایان نشست.</summary>
    public string? Reason { get; private set; }

    /// <summary>
    /// ثبت ورود موفق یا تلاش ناموفق.
    /// </summary>
    public UserSession(
        string username,
        UserSessionLoginMethod loginMethod,
        bool succeeded,
        string? clientIpAddress,
        string? reason = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        Username = username.Trim();
        LoginMethod = loginMethod;
        Status = succeeded ? UserSessionStatus.Active : UserSessionStatus.Failed;
        LoginAtUtc = DateTimeOffset.UtcNow;
        LastActivityAtUtc = LoginAtUtc;
        ClientIpAddress = NormalizeIp(clientIpAddress);
        Reason = succeeded ? null : NormalizeReason(reason);
        EndedAtUtc = succeeded ? null : LoginAtUtc;
    }

    /// <summary>
    /// ثبت فعالیت جدید در نشست فعال.
    /// </summary>
    public void Touch(DateTimeOffset activityAtUtc)
    {
        if (Status != UserSessionStatus.Active)
        {
            return;
        }

        if (activityAtUtc > LastActivityAtUtc)
        {
            LastActivityAtUtc = activityAtUtc;
        }
    }

    /// <summary>
    /// پایان دادن به نشست با علت مشخص.
    /// </summary>
    public void End(UserSessionStatus status, DateTimeOffset endedAtUtc)
    {
        if (Status != UserSessionStatus.Active)
        {
            return;
        }

        if (status is not (UserSessionStatus.LoggedOut
            or UserSessionStatus.IdleExpired
            or UserSessionStatus.AbsoluteExpired
            or UserSessionStatus.DirectoryRevoked))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        Status = status;
        EndedAtUtc = endedAtUtc;
    }

    /// <summary>
    /// تشخیص انقضای نشست بر اساس مهلت بیکاری و عمر مطلق.
    /// </summary>
    public bool ExpireIfNeeded(
        DateTimeOffset nowUtc,
        TimeSpan idleTimeout,
        TimeSpan absoluteTimeout)
    {
        if (Status != UserSessionStatus.Active)
        {
            return false;
        }

        if (nowUtc - LoginAtUtc >= absoluteTimeout)
        {
            End(UserSessionStatus.AbsoluteExpired, nowUtc);
            return true;
        }

        if (nowUtc - LastActivityAtUtc >= idleTimeout)
        {
            End(UserSessionStatus.IdleExpired, nowUtc);
            return true;
        }

        return false;
    }

    private static string? NormalizeIp(string? ipAddress)
    {
        var value = ipAddress?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value[..Math.Min(value.Length, 45)];
    }

    private static string? NormalizeReason(string? reason)
    {
        var value = reason?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value[..Math.Min(value.Length, 300)];
    }
}
