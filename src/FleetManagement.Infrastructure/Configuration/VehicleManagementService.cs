using System.Text.Json;
using FleetManagement.Domain.Vehicles;
using FleetManagement.Domain.Missions;
using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// عملیات پایه ثبت، اصلاح و بایگانی خودرو همراه با ممیزی.
/// </summary>
public sealed class VehicleManagementService(
    IDbContextFactory<FleetDbContext> dbContextFactory,
    AuditLogService auditLog)
{
    /// <summary>جست‌وجوی خودروها بر اساس برند، مدل یا پلاک.</summary>
    public async Task<IReadOnlyList<Vehicle>> SearchAsync(
        string? search = null,
        bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Vehicles.AsNoTracking();

        if (!includeArchived)
        {
            query = query.Where(vehicle => !vehicle.IsArchived);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim();
            query = query.Where(vehicle =>
                vehicle.Brand.Contains(value)
                || vehicle.Model.Contains(value)
                || vehicle.LicensePlate.Contains(value));
        }

        return await query.OrderBy(vehicle => vehicle.Brand).ThenBy(vehicle => vehicle.Model).ToListAsync(cancellationToken);
    }

    /// <summary>ثبت خودرو و ثبت رویداد ممیزی.</summary>
    public async Task<Guid> CreateAsync(
        string brand,
        string model,
        string licensePlate,
        string actor,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var vehicle = new Vehicle(brand, model, licensePlate);
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync(cancellationToken);

        await auditLog.RecordAsync(
            actor, "Create", "Vehicle", vehicle.Id.ToString(), "Success",
            afterJson: JsonSerializer.Serialize(new { vehicle.Brand, vehicle.Model, vehicle.LicensePlate, vehicle.ManualStatus }),
            clientIpAddress: ipAddress,
            cancellationToken: cancellationToken);

        return vehicle.Id;
    }

    /// <summary>اصلاح مشخصات خودرو و ثبت تصویر قبل و بعد.</summary>
    public async Task UpdateAsync(
        Guid vehicleId,
        string brand,
        string model,
        string licensePlate,
        string actor,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var vehicle = await db.Vehicles.SingleOrDefaultAsync(item => item.Id == vehicleId, cancellationToken)
            ?? throw new KeyNotFoundException("خودروی موردنظر پیدا نشد.");

        var before = JsonSerializer.Serialize(new { vehicle.Brand, vehicle.Model, vehicle.LicensePlate, vehicle.ManualStatus });
        vehicle.UpdateDetails(brand, model, licensePlate);
        var after = JsonSerializer.Serialize(new { vehicle.Brand, vehicle.Model, vehicle.LicensePlate, vehicle.ManualStatus });
        await db.SaveChangesAsync(cancellationToken);

        await auditLog.RecordAsync(
            actor, "Update", "Vehicle", vehicle.Id.ToString(), "Success",
            beforeJson: before, afterJson: after, clientIpAddress: ipAddress,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// اصلاح یکپارچه مشخصات و وضعیت دستی خودرو با کنترل مأموریت فعال.
    /// </summary>
    public async Task UpdateAsync(
        Guid vehicleId,
        string brand,
        string model,
        string licensePlate,
        ManualVehicleStatus manualStatus,
        string actor,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var vehicle = await db.Vehicles.SingleOrDefaultAsync(item => item.Id == vehicleId, cancellationToken)
            ?? throw new KeyNotFoundException("خودروی موردنظر پیدا نشد.");

        var hasActiveMission = await db.Missions.AnyAsync(mission =>
            mission.VehicleId == vehicleId
            && mission.Status is MissionStatus.Confirmed or MissionStatus.Running,
            cancellationToken);

        var before = JsonSerializer.Serialize(new
        {
            vehicle.Brand,
            vehicle.Model,
            vehicle.LicensePlate,
            vehicle.ManualStatus
        });

        vehicle.UpdateDetails(brand, model, licensePlate);
        vehicle.ChangeManualStatus(manualStatus, hasActiveMission);

        var after = JsonSerializer.Serialize(new
        {
            vehicle.Brand,
            vehicle.Model,
            vehicle.LicensePlate,
            vehicle.ManualStatus
        });

        await db.SaveChangesAsync(cancellationToken);
        await auditLog.RecordAsync(
            actor,
            "Update",
            "Vehicle",
            vehicle.Id.ToString(),
            "Success",
            beforeJson: before,
            afterJson: after,
            clientIpAddress: ipAddress,
            cancellationToken: cancellationToken);
    }

    /// <summary>بایگانی خودرو بدون حذف سابقه آن.</summary>
    public async Task ArchiveAsync(
        Guid vehicleId,
        string actor,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var vehicle = await db.Vehicles.SingleOrDefaultAsync(item => item.Id == vehicleId, cancellationToken)
            ?? throw new KeyNotFoundException("خودروی موردنظر پیدا نشد.");

        var hasActiveMission = await db.Missions.AnyAsync(mission =>
            mission.VehicleId == vehicleId
            && mission.Status is MissionStatus.Confirmed or MissionStatus.Running,
            cancellationToken);

        if (hasActiveMission)
        {
            throw new InvalidOperationException("خودروی دارای مأموریت فعال قابل بایگانی نیست.");
        }

        vehicle.Archive();
        await db.SaveChangesAsync(cancellationToken);

        await auditLog.RecordAsync(
            actor, "Archive", "Vehicle", vehicle.Id.ToString(), "Success",
            afterJson: JsonSerializer.Serialize(new { vehicle.IsArchived }),
            clientIpAddress: ipAddress,
            cancellationToken: cancellationToken);
    }

    /// <summary>بازگرداندن خودرو از بایگانی در صورت آماده‌به‌کار بودن.</summary>
    public async Task ReactivateAsync(
        Guid vehicleId,
        string actor,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var vehicle = await db.Vehicles.SingleOrDefaultAsync(item => item.Id == vehicleId, cancellationToken)
            ?? throw new KeyNotFoundException("خودروی موردنظر پیدا نشد.");

        vehicle.Reactivate();
        await db.SaveChangesAsync(cancellationToken);

        await auditLog.RecordAsync(
            actor, "Reactivate", "Vehicle", vehicle.Id.ToString(), "Success",
            afterJson: JsonSerializer.Serialize(new { vehicle.IsArchived }),
            clientIpAddress: ipAddress,
            cancellationToken: cancellationToken);
    }
}
