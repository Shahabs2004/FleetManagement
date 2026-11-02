using FleetManagement.Application.Notifications;
using FleetManagement.Web.Notifications;
using Microsoft.AspNetCore.Components;

namespace FleetManagement.Web.Components.Pages;

/// <summary>مدیریت اشتراک به‌روزرسانی زنده فهرست مکان‌ها.</summary>
public partial class LocationsPage : IDisposable
{
    [Inject]
    private NotificationEventBus FleetDataEvents { get; set; } = default!;

    /// <summary>اتصال صفحه به رویدادهای تغییر داده.</summary>
    protected override void OnInitialized()
    {
        base.OnInitialized();
        FleetDataEvents.DataChanged += OnFleetDataChanged;
    }

    /// <summary>بارگذاری مجدد مکان‌ها پس از ثبت یا بررسی پیشنهاد در نشست دیگر.</summary>
    private void OnFleetDataChanged(FleetDataChange change)
    {
        if (change.Kind != FleetDataChangeKind.Locations) return;

        _ = InvokeAsync(async () =>
        {
            await LoadAsync();
            StateHasChanged();
        });
    }

    /// <summary>قطع اشتراک هنگام خروج از صفحه.</summary>
    public void Dispose()
        => FleetDataEvents.DataChanged -= OnFleetDataChanged;
}
