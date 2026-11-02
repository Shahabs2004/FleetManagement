using System.Data;
using System.Text.Json;
using FleetManagement.Application.Missions;
using FleetManagement.Domain.Missions;
using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// اجرای ثبت کیلومترشمار و کنترل پیوستگی مقادیر خودرو در ترتیب مأموریت‌ها.
/// </summary>
public sealed class MissionOdometerService(
    IDbContextFactory<FleetDbContext> dbContextFactory,
    AuditLogService auditLog) : IMissionOdometerService
{
    public async Task SetOdometerAsync(
        Guid missionId,
        long? startKm,
        long? endKm,
        string actor,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var mission = await GetMissionAsync(db, missionId, cancellationToken);
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
            var previousEnd = await db.Missions
                .Where(item => item.Id != mission.Id
                    && item.VehicleId == mission.VehicleId
                    && item.OdometerEndKm.HasValue
                    && (item.ActualEndAtUtc ?? item.PlannedEndAtUtc) <= mission.PlannedStartAtUtc)
                .OrderByDescending(item => item.ActualEndAtUtc ?? item.PlannedEndAtUtc)
                .Select(item => item.OdometerEndKm)
                .FirstOrDefaultAsync(cancellationToken);

            if (previousEnd.HasValue && resultingStartKm.Value < previousEnd.Value)
            {
                throw new InvalidOperationException("کیلومترشمار شروع از آخرین مقدار ثبت‌شده برای این خودرو کمتر است.");
            }
        }

        var lastCurrentMissionOdometer = resultingEndKm ?? resultingStartKm;
        if (mission.VehicleId.HasValue && lastCurrentMissionOdometer.HasValue)
        {
            var nextStart = await db.Missions
                .Where(item => item.Id != mission.Id
                    && item.VehicleId == mission.VehicleId
                    && item.OdometerStartKm.HasValue
                    && item.PlannedStartAtUtc >= effectiveEndAtUtc)
                .OrderBy(item => item.PlannedStartAtUtc)
                .Select(item => item.OdometerStartKm)
                .FirstOrDefaultAsync(cancellationToken);

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

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await auditLog.RecordAsync(
            actor, "UpdateOdometer", "Mission", mission.Id.ToString(), "Success",
            beforeJson: JsonSerializer.Serialize(before),
            afterJson: JsonSerializer.Serialize(new { mission.OdometerStartKm, mission.OdometerEndKm, mission.DistanceKm }),
            cancellationToken: cancellationToken);
    }

    private static async Task<Mission> GetMissionAsync(
        FleetDbContext db,
        Guid missionId,
        CancellationToken cancellationToken)
    {
        return await db.Missions.SingleOrDefaultAsync(
            mission => mission.Id == missionId,
            cancellationToken)
            ?? throw new KeyNotFoundException("مأموریت موردنظر پیدا نشد.");
    }
}
