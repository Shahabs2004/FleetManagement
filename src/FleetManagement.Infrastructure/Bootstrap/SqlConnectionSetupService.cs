using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using FleetManagement.Infrastructure.Persistence;

namespace FleetManagement.Infrastructure.Bootstrap;

/// <summary>
/// اعتبارسنجی اتصال SQL Server و ثبت اطلاعات حداقلی آن برای راه‌اندازی و بازیابی.
/// </summary>
public sealed class SqlConnectionSetupService(
    IBootstrapStateStore stateStore,
    IDbContextFactory<FleetDbContext> dbContextFactory)
{
    /// <summary>
    /// اتصال SQL را آزمایش می‌کند و در صورت موفقیت رشته اتصال را در فایل bootstrap محافظت‌شده ذخیره می‌کند.
    /// </summary>
    public async Task TestAndSaveAsync(
        string server,
        string database,
        string username,
        string password,
        bool trustServerCertificate,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(server);
        ArgumentException.ThrowIfNullOrWhiteSpace(database);
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = server.Trim(),
            InitialCatalog = database.Trim(),
            UserID = username.Trim(),
            Password = password,
            IntegratedSecurity = false,
            Encrypt = true,
            TrustServerCertificate = trustServerCertificate,
            ConnectTimeout = 15,
            ApplicationName = "FleetManagement"
        };

        // آزمون اتصال قبل از ذخیره، برای جلوگیری از ثبت اطلاعات اشتباه.
        await using (var connection = new SqlConnection(builder.ConnectionString))
        {
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            _ = await command.ExecuteScalarAsync(cancellationToken);
        }

        var state = await stateStore.ReadAsync(cancellationToken);
        state.SqlConnectionString = builder.ConnectionString;
        await stateStore.WriteAsync(state, cancellationToken);

        // این مرحله فقط اتصال را ذخیره می‌کند؛ ایجاد ساختار جداول در مرحله Migration اجرا می‌شود.
    }
}
