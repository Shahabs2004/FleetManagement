using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FleetManagement.Infrastructure.Bootstrap;

/// <summary>
/// ایجاد پایگاه داده و اعمال Migrationهای نسخه جاری.
/// </summary>
public sealed class SqlDatabaseInitializer(
    IDbContextFactory<FleetDbContext> dbContextFactory)
{
    /// <summary>
    /// اجرای Migrationها برای ایجاد یا ارتقای ساختار پایگاه داده.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext =
            await dbContextFactory.CreateDbContextAsync(cancellationToken);

        // حساب SQL باید مجوز ایجاد پایگاه داده و تغییر ساختار جداول را داشته باشد.
        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
