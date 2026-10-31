using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FleetManagement.Infrastructure.Bootstrap;

/// <summary>
/// ساخت DbContext بر اساس رشته اتصال محافظت‌شده محلی.
/// این کارخانه پس از تکمیل ویزارد نیز اتصال را از فایل bootstrap می‌خواند.
/// </summary>
public sealed class BootstrapDbContextFactory(
    IBootstrapStateStore stateStore) : IDbContextFactory<FleetDbContext>
{
    /// <inheritdoc />
    public FleetDbContext CreateDbContext()
    {
        // این متد برای سازگاری با قرارداد EF Core است؛ مسیر اصلی در برنامه async است.
        var state = stateStore.ReadAsync().GetAwaiter().GetResult();
        return CreateContext(state.SqlConnectionString);
    }

    /// <inheritdoc />
    public async Task<FleetDbContext> CreateDbContextAsync(
        CancellationToken cancellationToken = default)
    {
        var state = await stateStore.ReadAsync(cancellationToken);
        return CreateContext(state.SqlConnectionString);
    }

    /// <summary>
    /// ساخت DbContext با تنظیمات اتصال SQL.
    /// </summary>
    private static FleetDbContext CreateContext(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "اتصال SQL هنوز در ویزارد راه‌اندازی تنظیم نشده است.");
        }

        var options = new DbContextOptionsBuilder<FleetDbContext>()
            .UseSqlServer(
                connectionString,
                sqlOptions => sqlOptions.EnableRetryOnFailure())
            .Options;

        return new FleetDbContext(options);
    }
}
