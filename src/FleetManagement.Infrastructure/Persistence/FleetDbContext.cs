using Microsoft.EntityFrameworkCore;

namespace FleetManagement.Infrastructure.Persistence;

/// <summary>
/// نقطه مرکزی نگاشت مدل‌های دامنه به پایگاه داده.
/// </summary>
public sealed class FleetDbContext(DbContextOptions<FleetDbContext> options)
    : DbContext(options)
{
    /// <summary>
    /// پیکربندی نگاشت‌ها و محدودیت‌های عمومی مدل داده.
    /// نگاشت اختصاصی هر ماژول باید در کلاس جداگانه انجام شود.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // در گام‌های بعدی، پیکربندی موجودیت‌های هر ماژول از کلاس‌های مستقل بارگذاری می‌شود.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FleetDbContext).Assembly);
    }
}
