using FleetManagement.Application.Notifications;

namespace FleetManagement.Web.Components.Pages;

/// <summary>تازه‌سازی تایم‌لاین با تغییر اطلاعات پایه رانندگان و خودروها.</summary>
public partial class MissionTimelinePage
{
    /// <summary>بارگذاری دوباره محور و ردیف‌ها پس از تغییر اطلاعات پایه.</summary>
    private void OnFleetDataChanged(FleetDataChange change)
    {
        if (change.Kind is not (FleetDataChangeKind.Drivers or FleetDataChangeKind.Vehicles)) return;

        _ = InvokeAsync(() => LoadAsync(preserveSelection: true));
    }
}
