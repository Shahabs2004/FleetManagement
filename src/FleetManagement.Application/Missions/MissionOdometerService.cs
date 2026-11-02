using System.Text.Json;
using FleetManagement.Application.Auditing;
using FleetManagement.Domain.Missions;

namespace FleetManagement.Application.Missions;

/// <summary>
/// اجرای سناریوی ثبت کیلومترشمار و کنترل پیوستگی آن در برنامه زمانی خودرو.
/// </summary>
public sealed class MissionOdometerService(
    IMissionOdometerStore store,
    IAuditLogWriter auditLog) : IMissionOdometerService
{
    /// <inheritdoc />
    public async Task SetOdometerAsync(
        Guid missionId,
        long? startKm,
        long? endKm,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var audit = await store.ExecuteSerializableAsync(async (unitOfWork, token) =>
        {
            var mission = await unitOfWork.GetMissionAsync(missionId, token)
                ?? throw new KeyNotFoundException("مأموریت موردنظر پیدا نشد.");
            var before = new { mission.OdometerStartKm, mission.OdometerEndKm };

            if ((startKm.HasValue || endKm.HasValue) && !mission.VehicleId.HasValue)
            {
                throw new InvalidOperationException("ابتدا باید خودرو برای مأموریت تعیین شده باشد.");
            }

            var effectiveEndAtUtc = mission.ActualEndAtUtc ?? mission.PlannedEndAtUtc;
            var resultingStartKm = startKm ?? mission.OdometerStartKm;
            var resultingEndKm = endKm ?? mission.OdometerEndKm;

            if (mission.VehicleId.HasValue && resultingStartKm.HasValue)
            {
                var previousEnd = await unitOfWork.GetPreviousEndOdometerAsync(
                    mission.Id,
                    mission.VehicleId.Value,
                    mission.PlannedStartAtUtc,
                    token);

                if (previousEnd.HasValue && resultingStartKm.Value < previousEnd.Value)
                {
                    throw new InvalidOperationException("کیلومترشمار شروع از آخرین مقدار ثبت‌شده برای این خودرو کمتر است.");
                }
            }

            var lastCurrentMissionOdometer = resultingEndKm ?? resultingStartKm;
            if (mission.VehicleId.HasValue && lastCurrentMissionOdometer.HasValue)
            {
                var nextStart = await unitOfWork.GetNextStartOdometerAsync(
                    mission.Id,
                    mission.VehicleId.Value,
                    effectiveEndAtUtc,
                    token);

                if (nextStart.HasValue && lastCurrentMissionOdometer.Value > nextStart.Value)
                {
                    throw new InvalidOperationException("مقدار کیلومترشمار این مأموریت از شروع مأموریت بعدی بیشتر است.");
                }
            }

            if (startKm.HasValue)
            {
                mission.SetOdometerStart(startKm.Value);
            }

            if (endKm.HasValue)
            {
                mission.SetOdometerEnd(endKm.Value);
            }

            return new OdometerAudit(
                mission.Id,
                before,
                new { mission.OdometerStartKm, mission.OdometerEndKm, mission.DistanceKm });
        }, cancellationToken);

        await auditLog.RecordAsync(
            actor,
            "UpdateOdometer",
            "Mission",
            audit.MissionId.ToString(),
            "Success",
            beforeJson: JsonSerializer.Serialize(audit.Before),
            afterJson: JsonSerializer.Serialize(audit.After),
            cancellationToken: cancellationToken);
    }

    private sealed record OdometerAudit(
        Guid MissionId,
        object Before,
        object After);
}
