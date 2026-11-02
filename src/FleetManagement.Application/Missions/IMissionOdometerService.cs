namespace FleetManagement.Application.Missions;

/// <summary>
/// قرارداد کاربردی ثبت و اعتبارسنجی کیلومترشمار مأموریت.
/// </summary>
public interface IMissionOdometerService
{
    /// <summary>ثبت کیلومتر شروع و پایان با کنترل پیوستگی خودرو.</summary>
    Task SetOdometerAsync(Guid missionId, long? startKm, long? endKm, string actor, CancellationToken cancellationToken = default);
}
