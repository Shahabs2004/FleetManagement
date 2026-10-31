using FleetManagement.Domain.Configuration;
using FleetManagement.Domain.Drivers;
using FleetManagement.Domain.Vehicles;
using FleetManagement.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace FleetManagement.Infrastructure.Persistence;

/// <summary>
/// نقطه مرکزی نگاشت مدل‌های دامنه به پایگاه داده.
/// </summary>
public sealed class FleetDbContext(DbContextOptions<FleetDbContext> options)
    : DbContext(options)
{
    /// <summary>مجموعه رانندگان سامانه.</summary>
    public DbSet<Driver> Drivers => Set<Driver>();

    /// <summary>مجموعه خودروهای سامانه.</summary>
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    /// <summary>پروفایل کاربران سازمانی همگام‌شده با Active Directory.</summary>
    public DbSet<ApplicationUser> ApplicationUsers => Set<ApplicationUser>();

    /// <summary>مجموعه تنظیمات عمومی و ماژولی سامانه.</summary>
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    /// <summary>
    /// پیکربندی نگاشت‌ها و محدودیت‌های عمومی مدل داده.
    /// نگاشت اختصاصی هر ماژول در کلاس جداگانه انجام می‌شود.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // پیکربندی همه موجودیت‌ها از کلاس‌های مستقل هر ماژول بارگذاری می‌شود.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FleetDbContext).Assembly);
    }
}
