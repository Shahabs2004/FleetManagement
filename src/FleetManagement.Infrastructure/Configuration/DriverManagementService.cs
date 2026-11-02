using System.Text.Json;
using FleetManagement.Application.Notifications;
using FleetManagement.Domain.Drivers;
using FleetManagement.Domain.Missions;
using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// عملیات پایه مدیریت رانندگان همراه با ثبت رویداد ممیزی.
/// </summary>
public sealed class DriverManagementService(
    IDbContextFactory<FleetDbContext> dbContextFactory,
    AuditLogService auditLog,
    IFleetDataChangePublisher dataChangePublisher)
{
    /// <summary>دریافت رانندگان با امکان جست‌وجو بر اساس نام و نام خانوادگی.</summary>
    public async Task<IReadOnlyList<Driver>> SearchAsync(
        string? search = null,
        bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Drivers.AsNoTracking();

        if (!includeArchived)
        {
            query = query.Where(driver => !driver.IsArchived);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim();
            query = query.Where(driver =>
                driver.FirstName.Contains(value) || driver.LastName.Contains(value));
        }

        return await query
            .OrderBy(driver => driver.LastName)
            .ThenBy(driver => driver.FirstName)
            .ToListAsync(cancellationToken);
    }

    /// <summary>ایجاد راننده جدید و ثبت رویداد ممیزی.</summary>
    public async Task<Guid> CreateAsync(
        string firstName,
        string lastName,
        string mobilePhone,
        string actor,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var driver = new Driver(firstName, lastName, mobilePhone);
        db.Drivers.Add(driver);
        await db.SaveChangesAsync(cancellationToken);

        await auditLog.RecordAsync(
            actor, "Create", "Driver", driver.Id.ToString(), "Success",
            afterJson: JsonSerializer.Serialize(new { driver.FirstName, driver.LastName, driver.MobilePhone, driver.EmploymentStatus }),
            clientIpAddress: ipAddress,
            cancellationToken: cancellationToken);
        await dataChangePublisher.PublishAsync(FleetDataChangeKind.Drivers, cancellationToken);

        return driver.Id;
    }

    /// <summary>اصلاح مشخصات راننده و ثبت تصویر قبل و بعد.</summary>
    public async Task UpdateAsync(
        Guid driverId,
        string firstName,
        string lastName,
        string mobilePhone,
        string actor,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var driver = await db.Drivers.SingleOrDefaultAsync(item => item.Id == driverId, cancellationToken)
            ?? throw new KeyNotFoundException("راننده موردنظر پیدا نشد.");

        var before = JsonSerializer.Serialize(new { driver.FirstName, driver.LastName, driver.MobilePhone, driver.EmploymentStatus, driver.IsArchived });
        driver.UpdateProfile(firstName, lastName, mobilePhone);
        var after = JsonSerializer.Serialize(new { driver.FirstName, driver.LastName, driver.MobilePhone, driver.EmploymentStatus, driver.IsArchived });
        await db.SaveChangesAsync(cancellationToken);

        await auditLog.RecordAsync(
            actor, "Update", "Driver", driver.Id.ToString(), "Success",
            beforeJson: before, afterJson: after, clientIpAddress: ipAddress,
            cancellationToken: cancellationToken);
        await dataChangePublisher.PublishAsync(FleetDataChangeKind.Drivers, cancellationToken);
    }

    /// <summary>
    /// اصلاح یکپارچه مشخصات و وضعیت استخدامی راننده با کنترل مأموریت فعال.
    /// </summary>
    public async Task UpdateAsync(
        Guid driverId,
        string firstName,
        string lastName,
        string mobilePhone,
        DriverEmploymentStatus employmentStatus,
        string actor,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var driver = await db.Drivers.SingleOrDefaultAsync(item => item.Id == driverId, cancellationToken)
            ?? throw new KeyNotFoundException("راننده موردنظر پیدا نشد.");

        if (employmentStatus != DriverEmploymentStatus.Active)
        {
            var hasActiveMission = await db.Missions.AnyAsync(mission =>
                mission.DriverId == driverId
                && (mission.Status == MissionStatus.Confirmed || mission.Status == MissionStatus.Running || mission.Status == MissionStatus.WithdrawalPending),
                cancellationToken);

            if (hasActiveMission)
            {
                throw new InvalidOperationException("وضعیت استخدامی راننده دارای مأموریت فعال قابل تغییر نیست.");
            }
        }

        var before = JsonSerializer.Serialize(new
        {
            driver.FirstName,
            driver.LastName,
            driver.MobilePhone,
            driver.EmploymentStatus,
            driver.IsArchived
        });

        driver.UpdateProfile(firstName, lastName, mobilePhone);
        driver.ChangeEmploymentStatus(employmentStatus);

        var after = JsonSerializer.Serialize(new
        {
            driver.FirstName,
            driver.LastName,
            driver.MobilePhone,
            driver.EmploymentStatus,
            driver.IsArchived
        });

        await db.SaveChangesAsync(cancellationToken);
        await auditLog.RecordAsync(
            actor,
            "Update",
            "Driver",
            driver.Id.ToString(),
            "Success",
            beforeJson: before,
            afterJson: after,
            clientIpAddress: ipAddress,
            cancellationToken: cancellationToken);
        await dataChangePublisher.PublishAsync(FleetDataChangeKind.Drivers, cancellationToken);
    }

    /// <summary>تغییر وضعیت استخدامی و ثبت ممیزی.</summary>
    public async Task ChangeEmploymentStatusAsync(
        Guid driverId,
        DriverEmploymentStatus status,
        string actor,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var driver = await db.Drivers.SingleOrDefaultAsync(item => item.Id == driverId, cancellationToken)
            ?? throw new KeyNotFoundException("راننده موردنظر پیدا نشد.");

        if (status != DriverEmploymentStatus.Active)
        {
            var hasActiveMission = await db.Missions.AnyAsync(mission =>
                mission.DriverId == driverId
                && (mission.Status == MissionStatus.Confirmed || mission.Status == MissionStatus.Running || mission.Status == MissionStatus.WithdrawalPending),
                cancellationToken);

            if (hasActiveMission)
            {
                throw new InvalidOperationException("وضعیت استخدامی راننده دارای مأموریت فعال قابل تغییر نیست.");
            }
        }

        var before = driver.EmploymentStatus.ToString();
        driver.ChangeEmploymentStatus(status);
        await db.SaveChangesAsync(cancellationToken);

        await auditLog.RecordAsync(
            actor, "ChangeEmploymentStatus", "Driver", driver.Id.ToString(), "Success",
            beforeJson: JsonSerializer.Serialize(new { Status = before }),
            afterJson: JsonSerializer.Serialize(new { Status = status.ToString() }),
            clientIpAddress: ipAddress,
            cancellationToken: cancellationToken);
        await dataChangePublisher.PublishAsync(FleetDataChangeKind.Drivers, cancellationToken);
    }

    /// <summary>بایگانی راننده بدون حذف اطلاعات تاریخی.</summary>
    public async Task ArchiveAsync(
        Guid driverId,
        string actor,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var driver = await db.Drivers.SingleOrDefaultAsync(item => item.Id == driverId, cancellationToken)
            ?? throw new KeyNotFoundException("راننده موردنظر پیدا نشد.");

        var hasActiveMission = await db.Missions.AnyAsync(mission =>
            mission.DriverId == driverId
            && (mission.Status == MissionStatus.Confirmed || mission.Status == MissionStatus.Running || mission.Status == MissionStatus.WithdrawalPending),
            cancellationToken);

        if (hasActiveMission)
        {
            throw new InvalidOperationException("راننده دارای مأموریت فعال قابل بایگانی نیست.");
        }

        driver.Archive();
        await db.SaveChangesAsync(cancellationToken);

        await auditLog.RecordAsync(
            actor, "Archive", "Driver", driver.Id.ToString(), "Success",
            afterJson: JsonSerializer.Serialize(new { driver.IsArchived }),
            clientIpAddress: ipAddress,
            cancellationToken: cancellationToken);
        await dataChangePublisher.PublishAsync(FleetDataChangeKind.Drivers, cancellationToken);
    }

    /// <summary>بازگرداندن راننده بایگانی‌شده در صورت فعال بودن وضعیت استخدامی.</summary>
    public async Task ReactivateAsync(
        Guid driverId,
        string actor,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var driver = await db.Drivers.SingleOrDefaultAsync(item => item.Id == driverId, cancellationToken)
            ?? throw new KeyNotFoundException("راننده موردنظر پیدا نشد.");

        driver.Reactivate();
        await db.SaveChangesAsync(cancellationToken);

        await auditLog.RecordAsync(
            actor, "Reactivate", "Driver", driver.Id.ToString(), "Success",
            afterJson: JsonSerializer.Serialize(new { driver.IsArchived }),
            clientIpAddress: ipAddress,
            cancellationToken: cancellationToken);
        await dataChangePublisher.PublishAsync(FleetDataChangeKind.Drivers, cancellationToken);
    }
}
