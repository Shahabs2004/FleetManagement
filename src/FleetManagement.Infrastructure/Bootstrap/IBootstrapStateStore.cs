namespace FleetManagement.Infrastructure.Bootstrap;

/// <summary>
/// قرارداد خواندن و نوشتن اطلاعات حداقلی راه‌اندازی.
/// </summary>
public interface IBootstrapStateStore
{
    /// <summary>خواندن وضعیت راه‌اندازی از فایل محافظت‌شده.</summary>
    Task<BootstrapState> ReadAsync(CancellationToken cancellationToken = default);

    /// <summary>ذخیره امن وضعیت راه‌اندازی در فایل محلی.</summary>
    Task WriteAsync(BootstrapState state, CancellationToken cancellationToken = default);
}
