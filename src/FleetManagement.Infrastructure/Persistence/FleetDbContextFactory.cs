using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FleetManagement.Infrastructure.Persistence;

/// <summary>
/// ایجاد DbContext هنگام اجرای ابزارهای طراحی EF Core.
/// </summary>
public sealed class FleetDbContextFactory : IDesignTimeDbContextFactory<FleetDbContext>
{
    public FleetDbContext CreateDbContext(string[] args)
    {
        // رشته اتصال فقط از محیط امن اجرای ابزار خوانده می‌شود و در سورس ثبت نمی‌شود.
        var connectionString =
            Environment.GetEnvironmentVariable("FLEETMANAGEMENT_SQL_CONNECTION");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "متغیر محیطی FLEETMANAGEMENT_SQL_CONNECTION برای اجرای Migration تنظیم نشده است.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<FleetDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new FleetDbContext(optionsBuilder.Options);
    }
}
