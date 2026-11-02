using System.Collections.Concurrent;

namespace FleetManagement.Web.Security;

/// <summary>
/// جلوگیری از نوشتن در SQL در هر درخواست؛ آخرین فعالیت حداکثر هر یک دقیقه ثبت می‌شود.
/// </summary>
public sealed class SessionActivityTracker
{
    private static readonly TimeSpan MinimumTouchInterval = TimeSpan.FromMinutes(1);
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _lastTouch = new();

    /// <summary>
    /// تعیین می‌کند که زمان ثبت فعالیت نشست فرا رسیده است یا خیر.
    /// </summary>
    public bool ShouldTouch(Guid sessionId)
    {
        var now = DateTimeOffset.UtcNow;

        while (true)
        {
            if (!_lastTouch.TryGetValue(sessionId, out var previous))
            {
                if (_lastTouch.TryAdd(sessionId, now))
                {
                    return true;
                }

                continue;
            }

            if (now - previous < MinimumTouchInterval)
            {
                return false;
            }

            if (_lastTouch.TryUpdate(sessionId, now, previous))
            {
                return true;
            }
        }
    }

    /// <summary>پاک‌سازی شناسه نشست پس از خروج یا انقضا.</summary>
    public void Forget(Guid sessionId)
    {
        _lastTouch.TryRemove(sessionId, out _);
    }
}
