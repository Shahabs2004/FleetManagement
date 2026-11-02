using FleetManagement.Application.Configuration;
using FleetManagement.Application.Notifications;
using FleetManagement.Domain.Drivers;
using FleetManagement.Domain.Missions;
using FleetManagement.Domain.Vehicles;
using FleetManagement.Infrastructure.Configuration;
using FleetManagement.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FleetManagement.Infrastructure.IntegrationTests;

/// <summary>
/// آزمون‌های یکپارچه گردش تخصیص مأموریت روی SQL Server واقعی.
/// </summary>
public sealed class MissionManagementSqlServerTests : IAsyncLifetime
{
    private SqlServerTestDatabase _database = null!;
    private IDbContextFactory<FleetDbContext> _factory = null!;
    private MissionManagementService _service = null!;
    private ISystemSettingsStore _settings = null!;

    public async Task InitializeAsync()
    {
        _database = await SqlServerTestDatabase.CreateAsync();
        _factory = new TestDbContextFactory(_database.Options);
        _settings = new SqlSystemSettingsStore(_factory);

        var audit = new AuditLogService(_factory);
        var authorization = new AuthorizationSettingsService(_settings, audit);
        var recipients = new UserNotificationRecipientResolver(_factory, authorization);
        var notifications = new NotificationService(
            _factory,
            new NoOpNotificationPublisher(),
            NullLogger<NotificationService>.Instance,
            recipients);
        var smsSettings = new SmsSettingsService(_settings, audit);
        var smsQueue = new SmsQueueService(_factory, audit, smsSettings);

        _service = new MissionManagementService(
            _factory,
            _settings,
            audit,
            notifications,
            smsQueue,
            NullLogger<MissionManagementService>.Instance);
    }

    public async Task DisposeAsync()
    {
        if (_database is not null)
        {
            await _database.DisposeAsync();
        }
    }

    [Fact]
    public async Task دو_تأیید_همزمان_نمی‌توانند_یک_راننده_و_خودرو_را_رزرو_کنند()
    {
        var resources = await AddResourcesAsync(driverCount: 1, vehicleCount: 1);
        var start = FutureWholeHour();
        var first = await CreateRequestAsync(start, start.AddHours(2));
        var second = await CreateRequestAsync(start, start.AddHours(2));

        var outcomes = await Task.WhenAll(
            TryApproveAsync(first, resources.DriverIds[0], resources.VehicleIds[0]),
            TryApproveAsync(second, resources.DriverIds[0], resources.VehicleIds[0]));

        Assert.Equal(1, outcomes.Count(succeeded => succeeded));

        await using var db = await _factory.CreateDbContextAsync();
        var confirmedCount = await db.Missions.CountAsync(
            mission => mission.Status == MissionStatus.Confirmed);
        Assert.Equal(1, confirmedCount);
    }

    [Fact]
    public async Task راننده_در_دو_مأموریت_همپوشان_قابل_تخصیص_نیست()
    {
        var resources = await AddResourcesAsync(driverCount: 1, vehicleCount: 2);
        var start = FutureWholeHour();
        var first = await CreateRequestAsync(start, start.AddHours(2));
        var second = await CreateRequestAsync(start.AddHours(1), start.AddHours(3));

        await _service.ApproveAsync(first, resources.DriverIds[0], resources.VehicleIds[0], "integration-test");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.ApproveAsync(second, resources.DriverIds[0], resources.VehicleIds[1], "integration-test"));
    }

    [Fact]
    public async Task خودرو_در_دو_مأموریت_همپوشان_قابل_تخصیص_نیست()
    {
        var resources = await AddResourcesAsync(driverCount: 2, vehicleCount: 1);
        var start = FutureWholeHour();
        var first = await CreateRequestAsync(start, start.AddHours(2));
        var second = await CreateRequestAsync(start.AddHours(1), start.AddHours(3));

        await _service.ApproveAsync(first, resources.DriverIds[0], resources.VehicleIds[0], "integration-test");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.ApproveAsync(second, resources.DriverIds[1], resources.VehicleIds[0], "integration-test"));
    }

    [Fact]
    public async Task فاصله_اجباری_بین_دو_مأموریت_برای_راننده_و_خودرو_اعمال_می‌شود()
    {
        await _settings.SetAsync(
            "Missions.Rules",
            new MissionRulesConfiguration { MinimumGapHours = 1, BookingHorizonDays = 90 });

        var resources = await AddResourcesAsync(driverCount: 1, vehicleCount: 1);
        var start = FutureWholeHour();
        var first = await CreateRequestAsync(start, start.AddHours(2));
        var second = await CreateRequestAsync(start.AddHours(2), start.AddHours(4));

        await _service.ApproveAsync(first, resources.DriverIds[0], resources.VehicleIds[0], "integration-test");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.ApproveAsync(second, resources.DriverIds[0], resources.VehicleIds[0], "integration-test"));
    }

    [Fact]
    public async Task شماره_مأموریت_در_ثبت_همزمان_تکراری_نمی‌شود()
    {
        const int requestCount = 8;
        var start = FutureWholeHour();

        var ids = await Task.WhenAll(
            Enumerable.Range(0, requestCount)
                .Select(index => CreateRequestAsync(start, start.AddHours(1), $"درخواست همزمان {index}")));

        await using var db = await _factory.CreateDbContextAsync();
        var numbers = await db.Missions
            .Where(mission => ids.Contains(mission.Id))
            .Select(mission => mission.MissionNumber)
            .ToListAsync();

        Assert.Equal(requestCount, numbers.Count);
        Assert.Equal(requestCount, numbers.Distinct(StringComparer.Ordinal).Count());
        Assert.All(numbers, number => Assert.Matches(@"^\d{4}-\d{3,}$", number));
    }

    private async Task<bool> TryApproveAsync(Guid missionId, Guid driverId, Guid vehicleId)
    {
        try
        {
            await _service.ApproveAsync(missionId, driverId, vehicleId, "integration-test");
            return true;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
            or DbUpdateException
            or SqlException)
        {
            return false;
        }
    }

    private async Task<(Guid[] DriverIds, Guid[] VehicleIds)> AddResourcesAsync(
        int driverCount,
        int vehicleCount)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var drivers = Enumerable.Range(0, driverCount)
            .Select(index => new Driver($"راننده{index}", "آزمایشی", $"0912000000{index}"))
            .ToArray();
        var vehicles = Enumerable.Range(0, vehicleCount)
            .Select(index => new Vehicle(
                "ایران‌خودرو",
                "سمند",
                index == 0 ? "۱۲ الف ۳۴۵ ایران ۶۷" : "۱۳ الف ۳۴۵ ایران ۶۷"))
            .ToArray();

        db.Drivers.AddRange(drivers);
        db.Vehicles.AddRange(vehicles);
        await db.SaveChangesAsync();

        return (drivers.Select(driver => driver.Id).ToArray(),
            vehicles.Select(vehicle => vehicle.Id).ToArray());
    }

    private Task<Guid> CreateRequestAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        string? title = null)
    {
        return _service.CreateRequestAsync(
            title ?? "آزمون یکپارچه مأموریت",
            "integration.requester",
            "درخواست‌کننده آزمون",
            "09120000000",
            "واحد آزمون",
            "گروه آزمون",
            MissionType.Passenger,
            "بررسی رفتار SQL Server",
            "مبدأ آزمون",
            "مقصد آزمون",
            [],
            start,
            end,
            null,
            1,
            null,
            null,
            "integration-test");
    }

    private static DateTimeOffset FutureWholeHour()
    {
        var date = DateTimeOffset.UtcNow.UtcDateTime.Date.AddDays(2);
        return new DateTimeOffset(date.AddHours(9), TimeSpan.Zero);
    }

    private sealed class TestDbContextFactory(DbContextOptions<FleetDbContext> options)
        : IDbContextFactory<FleetDbContext>
    {
        public FleetDbContext CreateDbContext() => new(options);

        public Task<FleetDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new FleetDbContext(options));
        }
    }

    private sealed class NoOpNotificationPublisher : INotificationPublisher
    {
        public Task PublishAsync(
            string username,
            NotificationPush notification,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class SqlServerTestDatabase : IAsyncDisposable
    {
        private SqlServerTestDatabase(DbContextOptions<FleetDbContext> options)
        {
            Options = options;
        }

        public DbContextOptions<FleetDbContext> Options { get; }

        public static async Task<SqlServerTestDatabase> CreateAsync()
        {
            var baseConnectionString = Environment.GetEnvironmentVariable(
                "FLEETMANAGEMENT_TEST_SQL_CONNECTION");

            if (string.IsNullOrWhiteSpace(baseConnectionString))
            {
                throw new InvalidOperationException(
                    "متغیر FLEETMANAGEMENT_TEST_SQL_CONNECTION برای اجرای آزمون SQL Server الزامی است.");
            }

            var builder = new SqlConnectionStringBuilder(baseConnectionString)
            {
                InitialCatalog = $"FleetManagementIntegration_{Guid.NewGuid():N}",
                ConnectTimeout = 30
            };

            var optionsBuilder = new DbContextOptionsBuilder<FleetDbContext>();
            optionsBuilder.UseSqlServer(builder.ConnectionString);

            var database = new SqlServerTestDatabase(optionsBuilder.Options);
            await using var db = new FleetDbContext(database.Options);
            await db.Database.MigrateAsync();
            return database;
        }

        public async ValueTask DisposeAsync()
        {
            await using var db = new FleetDbContext(Options);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
