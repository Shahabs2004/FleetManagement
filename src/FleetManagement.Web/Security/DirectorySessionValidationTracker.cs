using System.Collections.Concurrent;

namespace FleetManagement.Web.Security;

/// <summary>
/// جلوگیری از اجرای هم‌زمان و پرتکرار بازبینی حساب AD برای یک نشست.
/// </summary>
public sealed class DirectorySessionValidationTracker
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _nextCheck =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>آغاز بازبینی در صورت رسیدن زمان کنترل بعدی.</summary>
    public bool TryBegin(string username, DateTimeOffset nowUtc)
    {
        while (true)
        {
            if (!_nextCheck.TryGetValue(username, out var nextCheck))
            {
                if (_nextCheck.TryAdd(username, nowUtc.AddMinutes(5)))
                {
                    return true;
                }

                continue;
            }

            if (nextCheck > nowUtc)
            {
                return false;
            }

            if (_nextCheck.TryUpdate(username, nowUtc.AddMinutes(5), nextCheck))
            {
                return true;
            }
        }
    }

    /// <summary>در صورت قطعی AD، بازبینی مجدد پس از یک دقیقه انجام شود.</summary>
    public void RetrySoon(string username, DateTimeOffset nowUtc)
    {
        _nextCheck[username] = nowUtc.AddMinutes(1);
    }

    /// <summary>حذف وضعیت کنترل پس از لغو نشست.</summary>
    public void Forget(string username)
    {
        _nextCheck.TryRemove(username, out _);
    }
}
