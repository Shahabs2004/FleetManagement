using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FleetManagement.Infrastructure.Bootstrap;

/// <summary>
/// بررسی دسترس‌پذیری SQL بدون متوقف کردن میزبان وب.
/// </summary>
public sealed class SqlAvailabilityService(
    IDbContextFactory<FleetDbContext> dbContextFactory)
{
    /// <summary>
    /// بررسی امکان اتصال برنامه به پایگاه داده عملیاتی.
    /// خطای اتصال به‌صورت وضعیت عدم دسترس‌پذیری گزارش می‌شود.
    /// </summary>
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var dbContext =
                await dbContextFactory.CreateDbContextAsync(cancellationToken);

            return await dbContext.Database.CanConnectAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
            // اتصال هنوز پیکربندی نشده است.
            return false;
        }
        catch (Exception)
        {
            // قطع SQL نباید میزبان IIS را متوقف کند.
            return false;
        }
    }
}
