using System.Text.Json;
using FleetManagement.Application.Notifications;
using FleetManagement.Domain.Locations;
using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// مدیریت پیشنهاد، تأیید و نگهداری فهرست مکان‌های قابل استفاده در مأموریت.
/// </summary>
public sealed class LocationManagementService(
    IDbContextFactory<FleetDbContext> dbContextFactory,
    AuditLogService auditLog,
    NotificationService notificationService,
    IFleetDataChangePublisher dataChangePublisher)
{
    /// <summary>دریافت مکان‌های تأییدشده برای انتخاب در فرم مأموریت.</summary>
    public async Task<IReadOnlyList<Location>> GetApprovedAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Locations.AsNoTracking()
            .Where(location => location.Status == LocationStatus.Approved)
            .OrderBy(location => location.Name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>دریافت پیشنهادهای در انتظار تصمیم مدیر.</summary>
    public async Task<IReadOnlyList<Location>> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Locations.AsNoTracking()
            .Where(location => location.Status == LocationStatus.Pending)
            .OrderBy(location => location.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// یافتن نام‌های مشابه برای هشدار به کاربر؛ وجود مشابه مانع ثبت پیشنهاد نمی‌شود.
    /// </summary>
    public async Task<IReadOnlyList<string>> FindSimilarNamesAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var value = name.Trim();
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Locations.AsNoTracking()
            .Where(location => location.Name.Contains(value) || value.Contains(location.Name))
            .OrderBy(location => location.Name)
            .Select(location => location.Name)
            .Take(10)
            .ToListAsync(cancellationToken);
    }

    /// <summary>ثبت پیشنهاد مکان توسط هر کاربر واردشده.</summary>
    public async Task<Guid> ProposeAsync(
        string name,
        string proposer,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var location = new Location(name, proposer);
        db.Locations.Add(location);
        await db.SaveChangesAsync(cancellationToken);

        await auditLog.RecordAsync(
            proposer, "Propose", "Location", location.Id.ToString(), "Success",
            afterJson: JsonSerializer.Serialize(new { location.Name, location.Status }),
            cancellationToken: cancellationToken);
        await dataChangePublisher.PublishAsync(FleetDataChangeKind.Locations, cancellationToken);
        await notificationService.CreateForRolesAsync(
            ["SystemAdministrator"],
            "پیشنهاد مکان جدید",
            $"مکان «{location.Name}» برای بررسی مدیر سامانه پیشنهاد شد.",
            "LocationProposed",
            "Location",
            location.Id.ToString(),
            cancellationToken);

        return location.Id;
    }

    /// <summary>تأیید پیشنهاد و افزودن آن به فهرست قابل انتخاب.</summary>
    public async Task ApproveAsync(
        Guid locationId,
        string reviewer,
        string? correctedName = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var location = await GetLocationAsync(db, locationId, cancellationToken);
        var before = location.Name;
        location.Approve(reviewer, correctedName);
        await db.SaveChangesAsync(cancellationToken);

        await auditLog.RecordAsync(
            reviewer, "Approve", "Location", location.Id.ToString(), "Success",
            beforeJson: JsonSerializer.Serialize(new { Name = before }),
            afterJson: JsonSerializer.Serialize(new { location.Name, location.Status }),
            cancellationToken: cancellationToken);
        await dataChangePublisher.PublishAsync(FleetDataChangeKind.Locations, cancellationToken);
    }

    /// <summary>رد پیشنهاد مکان با علت.</summary>
    public async Task RejectAsync(
        Guid locationId,
        string reason,
        string reviewer,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var location = await GetLocationAsync(db, locationId, cancellationToken);
        location.Reject(reviewer, reason);
        await db.SaveChangesAsync(cancellationToken);

        await auditLog.RecordAsync(
            reviewer, "Reject", "Location", location.Id.ToString(), "Rejected", reason,
            afterJson: JsonSerializer.Serialize(new { location.Name, location.Status }),
            cancellationToken: cancellationToken);
        await dataChangePublisher.PublishAsync(FleetDataChangeKind.Locations, cancellationToken);
    }

    /// <summary>اصلاح نام مکان تأییدشده.</summary>
    public async Task RenameAsync(
        Guid locationId,
        string newName,
        string reviewer,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var location = await GetLocationAsync(db, locationId, cancellationToken);
        var before = location.Name;
        location.Rename(newName, reviewer);
        await db.SaveChangesAsync(cancellationToken);

        await auditLog.RecordAsync(
            reviewer, "Rename", "Location", location.Id.ToString(), "Success",
            beforeJson: JsonSerializer.Serialize(new { Name = before }),
            afterJson: JsonSerializer.Serialize(new { location.Name }),
            cancellationToken: cancellationToken);
        await dataChangePublisher.PublishAsync(FleetDataChangeKind.Locations, cancellationToken);
    }

    /// <summary>غیرفعال‌سازی مکان بدون حذف سوابق مأموریت.</summary>
    public async Task DeactivateAsync(
        Guid locationId,
        string reviewer,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var location = await GetLocationAsync(db, locationId, cancellationToken);
        location.Deactivate(reviewer);
        await db.SaveChangesAsync(cancellationToken);

        await auditLog.RecordAsync(
            reviewer, "Deactivate", "Location", location.Id.ToString(), "Success",
            afterJson: JsonSerializer.Serialize(new { location.Name, location.Status }),
            cancellationToken: cancellationToken);
        await dataChangePublisher.PublishAsync(FleetDataChangeKind.Locations, cancellationToken);
    }

    private static async Task<Location> GetLocationAsync(
        FleetDbContext db,
        Guid locationId,
        CancellationToken cancellationToken)
    {
        return await db.Locations.SingleOrDefaultAsync(item => item.Id == locationId, cancellationToken)
            ?? throw new KeyNotFoundException("مکان موردنظر پیدا نشد.");
    }
}
