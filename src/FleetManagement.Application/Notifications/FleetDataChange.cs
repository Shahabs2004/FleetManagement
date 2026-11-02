namespace FleetManagement.Application.Notifications;

/// <summary>نوع داده عملیاتی که تغییر آن باید در صفحات باز منعکس شود.</summary>
public enum FleetDataChangeKind
{
    Drivers,
    Vehicles,
    Locations
}

/// <summary>پیام سبک تغییر داده؛ فاقد اطلاعات شخصی یا محتوای رکورد.</summary>
public sealed record FleetDataChange(
    FleetDataChangeKind Kind,
    DateTimeOffset OccurredAtUtc);

/// <summary>قرارداد انتشار تغییر داده‌های عملیاتی به نشست‌های فعال.</summary>
public interface IFleetDataChangePublisher
{
    /// <summary>انتشار نوع تغییر پس از ثبت موفق در پایگاه داده.</summary>
    Task PublishAsync(
        FleetDataChangeKind kind,
        CancellationToken cancellationToken = default);
}
