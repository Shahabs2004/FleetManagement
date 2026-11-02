using FleetManagement.Application.Notifications;
using FleetManagement.Web.Notifications;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace FleetManagement.Web;

/// <summary>همگام‌سازی داده‌های عملیاتی پس از اتصال دوباره مدار Blazor.</summary>
public sealed class FleetRealtimeCircuitHandler(NotificationEventBus eventBus) : CircuitHandler
{
    /// <summary>پس از اتصال مجدد، صفحات باز را وادار به دریافت دوباره داده‌های مجاز می‌کند.</summary>
    public override Task OnConnectionUpAsync(
        Circuit circuit,
        CancellationToken cancellationToken)
    {
        var occurredAtUtc = DateTimeOffset.UtcNow;
        foreach (var kind in Enum.GetValues<FleetDataChangeKind>())
        {
            eventBus.Publish(new FleetDataChange(kind, occurredAtUtc));
        }

        return base.OnConnectionUpAsync(circuit, cancellationToken);
    }
}
