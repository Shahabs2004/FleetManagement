using System.Text.Json;
using FleetManagement.Application.Configuration;
using FleetManagement.Domain.Drivers;
using FleetManagement.Domain.Vehicles;
using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// مدیریت خودروی پیشنهادی پیش‌فرض رانندگان؛ تخصیص نهایی در هر مأموریت مستقل است.
/// </summary>
public sealed class DriverVehiclePreferenceService(
    ISystemSettingsStore settingsStore,
    IDbContextFactory<FleetDbContext> dbContextFactory,
    AuditLogService auditLog)
{
    private const string SettingKey = "Fleet.DriverDefaultVehicles";

    /// <summary>دریافت شناسه خودروی پیش‌فرض راننده.</summary>
    public async Task<Guid?> GetDefaultVehicleIdAsync(
        Guid driverId,
        CancellationToken cancellationToken = default)
    {
        var configuration = await GetConfigurationAsync(cancellationToken);
        return configuration.Mappings
            .FirstOrDefault(mapping => mapping.DriverId == driverId)
            ?.VehicleId;
    }

    /// <summary>دریافت تمام نگاشت‌های راننده و خودروی پیش‌فرض.</summary>
    public async Task<IReadOnlyDictionary<Guid, Guid>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var configuration = await GetConfigurationAsync(cancellationToken);
        return configuration.Mappings
            .GroupBy(mapping => mapping.DriverId)
            .ToDictionary(group => group.Key, group => group.Last().VehicleId);
    }

    /// <summary>ثبت یا حذف خودروی پیشنهادی راننده و ثبت ممیزی تغییر.</summary>
    public async Task SetDefaultVehicleAsync(
        Guid driverId,
        Guid? vehicleId,
        string actorUsername,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorUsername);

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var driver = await db.Drivers.SingleOrDefaultAsync(
            item => item.Id == driverId,
            cancellationToken)
            ?? throw new KeyNotFoundException("راننده موردنظر پیدا نشد.");

        if (vehicleId.HasValue)
        {
            if (!driver.CanBeAssignedToMission())
            {
                throw new InvalidOperationException("برای راننده غیرفعال یا بایگانی‌شده نمی‌توان خودروی پیش‌فرض تعیین کرد.");
            }

            var vehicle = await db.Vehicles.SingleOrDefaultAsync(
                item => item.Id == vehicleId.Value,
                cancellationToken)
                ?? throw new KeyNotFoundException("خودروی انتخاب‌شده پیدا نشد.");

            if (vehicle.IsArchived || vehicle.ManualStatus != ManualVehicleStatus.Ready)
            {
                throw new InvalidOperationException("خودروی پیش‌فرض باید فعال و آماده به کار باشد.");
            }
        }

        var configuration = await GetConfigurationAsync(cancellationToken);
        var existing = configuration.Mappings
            .FirstOrDefault(mapping => mapping.DriverId == driverId);
        var previousVehicleId = existing?.VehicleId;

        configuration.Mappings.RemoveAll(mapping => mapping.DriverId == driverId);
        if (vehicleId.HasValue)
        {
            configuration.Mappings.Add(new DriverVehicleDefaultMapping
            {
                DriverId = driverId,
                VehicleId = vehicleId.Value
            });
        }

        await settingsStore.SetAsync(SettingKey, configuration, cancellationToken);
        await auditLog.RecordAsync(
            actorUsername,
            "SetDriverDefaultVehicle",
            "Driver",
            driverId.ToString(),
            "Success",
            beforeJson: JsonSerializer.Serialize(new { VehicleId = previousVehicleId }),
            afterJson: JsonSerializer.Serialize(new { VehicleId = vehicleId }),
            cancellationToken: cancellationToken);
    }

    /// <summary>دریافت پیکربندی معتبر یا مقدار پیش‌فرض خالی.</summary>
    private async Task<DriverVehiclePreferenceConfiguration> GetConfigurationAsync(
        CancellationToken cancellationToken)
    {
        return await settingsStore.GetAsync<DriverVehiclePreferenceConfiguration>(
            SettingKey,
            cancellationToken) ?? new DriverVehiclePreferenceConfiguration();
    }
}
