using FleetManagement.Application.Notifications;
using Microsoft.AspNetCore.SignalR;

namespace FleetManagement.Web.Notifications;

/// <summary>انتشار تغییرات سبک داده‌های ناوگان به مدارهای Blazor و اتصال‌های SignalR.</summary>
public sealed class SignalRFleetDataChangePublisher(
    IHubContext<NotificationHub> hubContext,
    NotificationEventBus eventBus,
    ILogger<SignalRFleetDataChangePublisher> logger) : IFleetDataChangePublisher
{
    /// <summary>انتشار پیام بدون داده رکورد؛ دریافت‌کننده مجدداً از سرویس مجاز خود بارگذاری می‌کند.</summary>
    public async Task PublishAsync(
        FleetDataChangeKind kind,
        CancellationToken cancellationToken = default)
    {
        var change = new FleetDataChange(kind, DateTimeOffset.UtcNow);
        try
        {
            eventBus.Publish(change);
        }
        catch (Exception exception)
        {
            // خطای شنونده محلی نباید نتیجه عملیات ذخیره‌شده را تغییر دهد.
            logger.LogWarning(exception, "تازه‌سازی محلی برای تغییر داده {ChangeKind} ناموفق بود.", kind);
        }

        try
        {
            await hubContext.Clients.All.SendAsync(
                "ReceiveFleetDataChanged",
                change,
                cancellationToken);
        }
        catch (Exception exception)
        {
            // تغییر در پایگاه داده ثبت شده است؛ قطع کانال بلادرنگ نباید عملیات اصلی را ناموفق کند.
            logger.LogWarning(
                exception,
                "انتشار تغییر داده {ChangeKind} از طریق SignalR انجام نشد.",
                kind);
        }
    }
}
