using Microsoft.Data.SqlClient;

namespace FleetManagement.Infrastructure.Bootstrap;

/// <summary>
/// اعتبارسنجی دسترسی SQL Server، ایجاد پایگاه داده و ثبت اتصال اضطراری.
/// </summary>
public sealed class SqlConnectionSetupService(IBootstrapStateStore stateStore)
{
    /// <summary>
    /// اتصال به سرور را آزمایش می‌کند، پایگاه داده را در صورت نبود ایجاد می‌کند
    /// و رشته اتصال نهایی را در فایل bootstrap محافظت‌شده ذخیره می‌کند.
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
            InitialCatalog = "master",
            UserID = username.Trim(),
            Password = password,
            IntegratedSecurity = false,
            Encrypt = true,
            TrustServerCertificate = trustServerCertificate,
            ConnectTimeout = 15,
            ApplicationName = "FleetManagement"
        };

        // اتصال به master برای امکان ایجاد پایگاه داده‌ای که هنوز وجود ندارد.
        await using (var connection = new SqlConnection(builder.ConnectionString))
        {
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = """
                IF DB_ID(@databaseName) IS NULL
                BEGIN
                    DECLARE @createDatabaseSql nvarchar(max) =
                        N'CREATE DATABASE ' + QUOTENAME(@databaseName);
                    EXEC(@createDatabaseSql);
                END;
                """;
            command.Parameters.AddWithValue("@databaseName", database.Trim());
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        // پس از ایجاد، اتصال به پایگاه داده هدف نیز آزمایش می‌شود.
        builder.InitialCatalog = database.Trim();

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
    }
}
