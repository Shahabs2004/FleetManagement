using System.Text.Json;
using FleetManagement.Domain.Users;
using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// مدیریت وضعیت داخلی کاربران و واحد سازمانی دستی؛ هویت دامنه از AD حذف نمی‌شود.
/// </summary>
public sealed class UserManagementService(
    IDbContextFactory<FleetDbContext> dbContextFactory,
    AuditLogService auditLog)
{
    /// <summary>جست‌وجوی کاربران فقط بر اساس نام، نام خانوادگی یا نام کاربری.</summary>
    public async Task<IReadOnlyList<ApplicationUser>> SearchAsync(
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.ApplicationUsers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim();
            query = query.Where(user =>
                user.Username.Contains(value)
                || user.FirstName.Contains(value)
                || user.LastName.Contains(value));
        }

        return await query
            .OrderBy(user => user.LastName)
            .ThenBy(user => user.FirstName)
            .Take(500)
            .ToListAsync(cancellationToken);
    }

    /// <summary>غیرفعال‌سازی داخلی کاربر با ثبت تصویر قبل و بعد.</summary>
    public async Task DisableAsync(
        Guid userId,
        string actor,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var user = await GetUserAsync(db, userId, cancellationToken);
        var before = new { user.IsDisabled };
        user.Disable();
        await db.SaveChangesAsync(cancellationToken);

        await auditLog.RecordAsync(
            actor, "DisableUser", "ApplicationUser", user.Id.ToString(), "Success",
            beforeJson: JsonSerializer.Serialize(before),
            afterJson: JsonSerializer.Serialize(new { user.IsDisabled }),
            cancellationToken: cancellationToken);
    }

    /// <summary>فعال‌سازی مجدد صریح کاربر توسط مدیر سامانه.</summary>
    public async Task EnableAsync(
        Guid userId,
        string actor,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var user = await GetUserAsync(db, userId, cancellationToken);
        var before = new { user.IsDisabled };
        user.Enable();
        await db.SaveChangesAsync(cancellationToken);

        await auditLog.RecordAsync(
            actor, "EnableUser", "ApplicationUser", user.Id.ToString(), "Success",
            beforeJson: JsonSerializer.Serialize(before),
            afterJson: JsonSerializer.Serialize(new { user.IsDisabled }),
            cancellationToken: cancellationToken);
    }

    /// <summary>تعیین دستی واحد سازمانی با حفظ مقدار AD در ستون مستقل.</summary>
    public async Task OverrideOrganizationalUnitAsync(
        Guid userId,
        string organizationalUnit,
        string actor,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var user = await GetUserAsync(db, userId, cancellationToken);
        var before = new { user.OrganizationalUnit, user.IsOrganizationalUnitManuallyOverridden };
        user.OverrideOrganizationalUnit(organizationalUnit);
        await db.SaveChangesAsync(cancellationToken);

        await auditLog.RecordAsync(
            actor, "OverrideOrganizationalUnit", "ApplicationUser", user.Id.ToString(), "Success",
            beforeJson: JsonSerializer.Serialize(before),
            afterJson: JsonSerializer.Serialize(new { user.OrganizationalUnit, user.IsOrganizationalUnitManuallyOverridden }),
            cancellationToken: cancellationToken);
    }

    /// <summary>حذف override و بازگشت به آخرین واحد سازمانی همگام‌شده از AD.</summary>
    public async Task ClearOrganizationalUnitOverrideAsync(
        Guid userId,
        string actor,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var user = await GetUserAsync(db, userId, cancellationToken);
        var before = new { user.OrganizationalUnit, user.IsOrganizationalUnitManuallyOverridden };
        user.ClearOrganizationalUnitOverride(user.DirectoryOrganizationalUnit);
        await db.SaveChangesAsync(cancellationToken);

        await auditLog.RecordAsync(
            actor, "ClearOrganizationalUnitOverride", "ApplicationUser", user.Id.ToString(), "Success",
            beforeJson: JsonSerializer.Serialize(before),
            afterJson: JsonSerializer.Serialize(new { user.OrganizationalUnit, user.IsOrganizationalUnitManuallyOverridden }),
            cancellationToken: cancellationToken);
    }

    private static async Task<ApplicationUser> GetUserAsync(
        FleetDbContext db,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await db.ApplicationUsers.SingleOrDefaultAsync(user => user.Id == userId, cancellationToken)
            ?? throw new KeyNotFoundException("کاربر موردنظر پیدا نشد.");
    }
}
