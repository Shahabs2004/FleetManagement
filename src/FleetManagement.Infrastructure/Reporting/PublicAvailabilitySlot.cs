namespace FleetManagement.Infrastructure.Reporting;

/// <summary>
/// یک بازه یک‌ساعته از ظرفیت عمومی؛ بدون افشای نام راننده یا خودرو.
/// </summary>
public sealed record PublicAvailabilitySlot(
    DateTimeOffset StartAtUtc,
    bool IsAvailable);
