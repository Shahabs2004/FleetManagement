using FleetManagement.Domain.Missions;

namespace FleetManagement.Application.Missions;

/// <summary>
/// عملیات داده‌ای موردنیاز برای ایجاد درخواست مأموریت.
/// </summary>
public interface IMissionRequestUnitOfWork
{
    /// <summary>تولید شماره سالانه یکتا در محدوده تراکنش.</summary>
    Task<string> GenerateMissionNumberAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    /// <summary>افزودن درخواست مأموریت به واحد کار جاری.</summary>
    void Add(Mission mission);
}
