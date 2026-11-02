using FleetManagement.Application.Configuration;
using FleetManagement.Application.Notifications;
using FleetManagement.Domain.Drivers;
using FleetManagement.Domain.Locations;
using FleetManagement.Domain.Missions;
using FleetManagement.Application.Missions;
using FleetManagement.Domain.Vehicles;
using FleetManagement.Infrastructure.Configuration;
using FleetManagement.Infrastructure.Persistence;
using FleetManagement.Infrastructure.Persistence.Migrations;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
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

        var missionChanges = new MissionChangeManagementService(
            _factory,
            _settings,
            audit,
            notifications,
            smsQueue,
            NullLogger<MissionChangeManagementService>.Instance);
        _service = new MissionManagementService(
            _factory,
            _settings,
            audit,
            notifications,
            smsQueue,
            NullLogger<MissionManagementService>.Instance,
            missionChanges,
            new MissionOdometerService(_factory, audit),
            new MissionSplitService(_factory, _settings, audit, notifications),
            new MissionWithdrawalService(
                _factory,
                _settings,
                audit,
                notifications,
                smsQueue,
                NullLogger<MissionWithdrawalService>.Instance),
            new MissionRequestService(
                _factory,
                _settings,
                audit,
                notifications,
                smsQueue,
                NullLogger<MissionRequestService>.Instance));
    }

    public async Task DisposeAsync()
    {
        if (_database is not null)
        {
            await _database.DisposeAsync();
        }
    }

    [Fact]
    public async Task اختلاف_مدل_فعلی_با_Snapshot_به_صورت_دقیق_گزارش_می‌شود()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        await using var db = new FleetDbContext(database.Options);

        var snapshotModel = db.GetService<IModelRuntimeInitializer>()
            .Initialize(new FleetDbContextModelSnapshot().Model, designTime: true);
        var currentModel = db.GetService<IDesignTimeModel>().Model;
        var differ = db.GetService<IMigrationsModelDiffer>();
        var differences = differ.GetDifferences(
            snapshotModel.GetRelationalModel(),
            currentModel.GetRelationalModel());

        var details = differences.Select(operation =>
        {
            var properties = operation.GetType().GetProperties()
                .Where(property => property.PropertyType == typeof(string)
                    || property.PropertyType.IsPrimitive)
                .Select(property => $"{property.Name}={property.GetValue(operation)}");
            return $"{operation.GetType().Name}: {string.Join(", ", properties)}";
        });

        Assert.True(!differences.Any(), string.Join(Environment.NewLine, details));
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task زنجیره_۱۳_مهاجرت_روی_پایگاه_داده_خالی_اعمال_و_بدون_مهاجرت_معوق_است()
    {
        // این آزمون مسیر production را می‌سنجد، نه EnsureCreated را.
        await using var database = await SqlServerTestDatabase.CreateAsync(applyMigrations: true);
        await using var db = new FleetDbContext(database.Options);

        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        Assert.Equal(13, appliedMigrations.Count());
    }

    [Fact]
    public async Task RowVersion_برای_مأموریت_راننده_و_خودرو_توکن_همزمانی_است()
    {
        await using var db = await _factory.CreateDbContextAsync();

        foreach (var entityType in new[]
        {
            db.Model.FindEntityType(typeof(Mission)),
            db.Model.FindEntityType(typeof(Driver)),
            db.Model.FindEntityType(typeof(Vehicle))
        })
        {
            Assert.NotNull(entityType);
            var rowVersion = entityType!.FindProperty("RowVersion");
            Assert.NotNull(rowVersion);
            Assert.True(rowVersion!.IsConcurrencyToken);
            Assert.Equal(ValueGenerated.OnAddOrUpdate, rowVersion.ValueGenerated);
            Assert.Equal("rowversion", rowVersion.GetColumnType());
        }
    }

    [Fact]
    public async Task تغییر_همزمان_یک_مأموریت_با_RowVersion_رد_می‌شود()
    {
        var start = FutureWholeHour();
        var missionId = await CreateRequestAsync(start, start.AddHours(2));

        await using var first = await _factory.CreateDbContextAsync();
        await using var second = await _factory.CreateDbContextAsync();
        var firstMission = await first.Missions.SingleAsync(item => item.Id == missionId);
        var secondMission = await second.Missions.SingleAsync(item => item.Id == missionId);

        var now = DateTimeOffset.UtcNow;
        firstMission.StartReview(now);
        secondMission.StartReview(now);

        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task قیود_SQL_از_بازه_زمانی_و_کیلومترشمار_نامعتبر_جلوگیری_می‌کنند()
    {
        var start = FutureWholeHour();
        var missionId = await CreateRequestAsync(start, start.AddHours(2));
        await using var db = await _factory.CreateDbContextAsync();

        await Assert.ThrowsAsync<SqlException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Missions SET PlannedEndAtUtc = {start} WHERE Id = {missionId}"));

        await Assert.ThrowsAsync<SqlException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Missions SET OdometerStartKm = 500, OdometerEndKm = 400 WHERE Id = {missionId}"));
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


    [Fact]
    public async Task تقسیم_مأموریت_دو_بخش_پیوسته_با_کیلومتر_و_منابع_مستقل_ایجاد_می‌کند()
    {
        var resources = await AddResourcesAsync(driverCount: 2, vehicleCount: 1);
        var futureStart = FutureWholeHour();
        var missionId = await CreateApprovedMissionAsync(
            resources.DriverIds[0], resources.VehicleIds[0], futureStart, futureStart.AddHours(4));

        // برای آزمون تقسیم، بازه مأموریت را به بازه‌ای گذشته منتقل می‌کنیم تا زمان تقسیم معتبر باشد.
        var pastStart = futureStart.AddDays(-3);
        var pastEnd = pastStart.AddHours(4);
        await using (var db = await _factory.CreateDbContextAsync())
        {
            await db.Missions
                .Where(item => item.Id == missionId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.PlannedStartAtUtc, pastStart)
                    .SetProperty(item => item.PlannedEndAtUtc, pastEnd));

            var mission = await db.Missions.SingleAsync(item => item.Id == missionId);
            mission.StartAutomatically(DateTimeOffset.UtcNow);
            mission.SetOdometerStart(100);
            await db.SaveChangesAsync();
        }

        var splitAt = pastStart.AddHours(2);
        var secondMissionId = await _service.SplitAsync(
            missionId,
            resources.DriverIds[1],
            resources.VehicleIds[0],
            splitAt,
            firstSegmentOdometerEndKm: 125,
            secondVehicleOdometerStartKm: null,
            splitReason: MissionSplitReason.DriverChange,
            explanation: null,
            actor: "integration-test");

        await using var verify = await _factory.CreateDbContextAsync();
        var first = await verify.Missions.SingleAsync(item => item.Id == missionId);
        var second = await verify.Missions.SingleAsync(item => item.Id == secondMissionId);

        Assert.Equal(MissionStatus.Completed, first.Status);
        Assert.Equal(MissionStatus.Running, second.Status);
        Assert.Equal(splitAt, first.PlannedEndAtUtc);
        Assert.Equal(splitAt, second.PlannedStartAtUtc);
        Assert.Equal(125L, first.OdometerEndKm);
        Assert.Equal(125L, second.OdometerStartKm);
        Assert.Equal(second.Id, first.RelatedMissionId);
        Assert.Equal(first.Id, second.RelatedMissionId);
        Assert.Equal(resources.DriverIds[0], first.DriverId);
        Assert.Equal(resources.DriverIds[1], second.DriverId);
        Assert.Equal(resources.VehicleIds[0], second.VehicleId);
    }

    [Fact]
    public async Task انصراف_در_انتظار_منابع_را_رزرو_نگه_می‌دارد_و_تأیید_آن_منابع_را_آزاد_می‌کند()
    {
        var resources = await AddResourcesAsync(driverCount: 1, vehicleCount: 1);
        var start = FutureWholeHour();
        var end = start.AddHours(2);
        var missionId = await CreateApprovedMissionAsync(
            resources.DriverIds[0], resources.VehicleIds[0], start, end);
        var competingRequest = await CreateRequestAsync(start, end, "درخواست رقیب");

        await _service.RequestWithdrawalAsync(missionId, "integration.requester", "تغییر برنامه");

        await using (var db = await _factory.CreateDbContextAsync())
        {
            var pending = await db.Missions.SingleAsync(item => item.Id == missionId);
            Assert.Equal(MissionStatus.WithdrawalPending, pending.Status);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ApproveAsync(
                competingRequest, resources.DriverIds[0], resources.VehicleIds[0], "integration-test"));

        await _service.ApproveWithdrawalAsync(missionId, "fleet-reviewer");

        await using (var db = await _factory.CreateDbContextAsync())
        {
            var cancelled = await db.Missions.SingleAsync(item => item.Id == missionId);
            Assert.Equal(MissionStatus.Cancelled, cancelled.Status);
        }

        await _service.ApproveAsync(
            competingRequest, resources.DriverIds[0], resources.VehicleIds[0], "integration-test");
    }

    [Fact]
    public async Task رد_انصراف_وضعیت_قبلی_مأموریت_را_بازمی‌گرداند()
    {
        var resources = await AddResourcesAsync(driverCount: 1, vehicleCount: 1);
        var start = FutureWholeHour();
        var missionId = await CreateApprovedMissionAsync(
            resources.DriverIds[0], resources.VehicleIds[0], start, start.AddHours(2));

        await _service.RequestWithdrawalAsync(missionId, "integration.requester", "تغییر برنامه");
        await _service.RejectWithdrawalAsync(missionId, "لغو برنامه منتفی شد", "fleet-reviewer");

        await using var db = await _factory.CreateDbContextAsync();
        var mission = await db.Missions.SingleAsync(item => item.Id == missionId);
        Assert.Equal(MissionStatus.Confirmed, mission.Status);
        Assert.Equal("لغو برنامه منتفی شد", mission.DecisionReason);
        Assert.Null(mission.StatusBeforeWithdrawal);
    }

    [Fact]
    public async Task انصراف_تعیین_تکلیف_نشده_پس_از_پایان_زمان_مأموریت_خودکار_بسته_می‌شود()
    {
        var resources = await AddResourcesAsync(driverCount: 1, vehicleCount: 1);
        var start = FutureWholeHour();
        var missionId = await CreateApprovedMissionAsync(
            resources.DriverIds[0], resources.VehicleIds[0], start, start.AddHours(2));

        var pastStart = start.AddDays(-3);
        var pastEnd = pastStart.AddHours(2);
        await using (var db = await _factory.CreateDbContextAsync())
        {
            await db.Missions
                .Where(item => item.Id == missionId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.PlannedStartAtUtc, pastStart)
                    .SetProperty(item => item.PlannedEndAtUtc, pastEnd));
        }

        Assert.Equal(1, await _service.ProcessDueMissionsAsync());
        await _service.RequestWithdrawalAsync(missionId, "integration.requester", "تغییر برنامه");
        Assert.Equal(1, await _service.ProcessDueMissionsAsync());

        await using var verify = await _factory.CreateDbContextAsync();
        var mission = await verify.Missions.SingleAsync(item => item.Id == missionId);
        Assert.Equal(MissionStatus.Cancelled, mission.Status);
        Assert.NotNull(mission.ActualEndAtUtc);
        Assert.Contains("تعیین تکلیف نشد", mission.DecisionReason);
        Assert.Null(mission.StatusBeforeWithdrawal);
    }

    [Fact]
    public async Task درخواست_تغییر_تا_تأیید_به_مأموریت_اعمال_نمی‌شود_و_تأیید_نامعتبر_اتمی_برمی‌گردد()
    {
        var resources = await AddResourcesAsync(driverCount: 2, vehicleCount: 2);
        var start = FutureWholeHour();
        var missionId = await CreateApprovedMissionAsync(
            resources.DriverIds[0], resources.VehicleIds[0], start, start.AddHours(2));

        var origin = new Location("مبدأ تأییدشده", "integration-test");
        var destination = new Location("مقصد تأییدشده", "integration-test");
        origin.Approve("system-admin");
        destination.Approve("system-admin");
        await using (var db = await _factory.CreateDbContextAsync())
        {
            db.Locations.AddRange(origin, destination);
            await db.SaveChangesAsync();
        }

        var proposal = new MissionChangeProposal(
            "عنوان پس از تغییر",
            MissionType.Freight,
            "هدف جدید",
            origin.Name,
            destination.Name,
            [],
            start.AddHours(4),
            start.AddHours(6),
            resources.DriverIds[1],
            resources.VehicleIds[1],
            "درخواست‌کننده اصلاح‌شده",
            "09121111111",
            "واحد جدید",
            "گروه جدید",
            "شرح جدید",
            null,
            "حکم-تغییر",
            new DateOnly(2026, 11, 2),
            500);

        var changeId = await _service.RequestChangeAsync(
            missionId, "integration.requester", "اصلاح اطلاعات مأموریت", proposal);

        await using (var db = await _factory.CreateDbContextAsync())
        {
            var unchanged = await db.Missions.SingleAsync(item => item.Id == missionId);
            Assert.Equal("آزمون یکپارچه مأموریت", unchanged.Title);
            Assert.Equal(resources.DriverIds[0], unchanged.DriverId);
            Assert.Equal(resources.VehicleIds[0], unchanged.VehicleId);
        }

        await _service.StartChangeReviewAsync(changeId, "reviewer-one");

        // شکست پس از تغییر شیء در حافظه، باید کل تراکنش را Rollback کند.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ApproveChangeAsync(changeId, "reviewer-two"));

        await using (var db = await _factory.CreateDbContextAsync())
        {
            var unchanged = await db.Missions.SingleAsync(item => item.Id == missionId);
            var pending = await db.MissionChangeRequests.SingleAsync(item => item.Id == changeId);
            Assert.Equal("آزمون یکپارچه مأموریت", unchanged.Title);
            Assert.Equal(resources.DriverIds[0], unchanged.DriverId);
            Assert.Equal(resources.VehicleIds[0], unchanged.VehicleId);
            Assert.Equal(MissionChangeRequestStatus.UnderReview, pending.Status);
        }

        await _service.ApproveChangeAsync(changeId, "reviewer-one");

        await using var verify = await _factory.CreateDbContextAsync();
        var changed = await verify.Missions.SingleAsync(item => item.Id == missionId);
        var approved = await verify.MissionChangeRequests.SingleAsync(item => item.Id == changeId);
        Assert.Equal("عنوان پس از تغییر", changed.Title);
        Assert.Equal(MissionType.Freight, changed.Type);
        Assert.Equal(resources.DriverIds[1], changed.DriverId);
        Assert.Equal(resources.VehicleIds[1], changed.VehicleId);
        Assert.Equal("مبدأ تأییدشده", changed.Origin);
        Assert.Equal("مقصد تأییدشده", changed.Destination);
        Assert.Equal(500L, changed.OdometerStartKm);
        Assert.Equal(MissionChangeRequestStatus.Approved, approved.Status);
    }

    [Fact]
    public async Task کیلومترشمار_در_کل_ترتیب_زمانی_مأموریت‌های_یک_خودرو_پیوسته_می‌ماند()
    {
        var resources = await AddResourcesAsync(driverCount: 2, vehicleCount: 1);
        var start = FutureWholeHour();
        var firstId = await CreateApprovedMissionAsync(
            resources.DriverIds[0], resources.VehicleIds[0], start, start.AddHours(2));
        var secondId = await CreateApprovedMissionAsync(
            resources.DriverIds[1], resources.VehicleIds[0], start.AddHours(3), start.AddHours(4));

        await _service.SetOdometerAsync(firstId, 100, 150, "integration-test");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SetOdometerAsync(secondId, 140, null, "integration-test"));

        await _service.SetOdometerAsync(secondId, 160, null, "integration-test");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SetOdometerAsync(firstId, null, 170, "integration-test"));

        await using var db = await _factory.CreateDbContextAsync();
        var first = await db.Missions.SingleAsync(item => item.Id == firstId);
        var second = await db.Missions.SingleAsync(item => item.Id == secondId);
        Assert.Equal(150L, first.OdometerEndKm);
        Assert.Equal(160L, second.OdometerStartKm);
    }

    [Fact]
    public async Task درخواست_در_حال_بررسی_توسط_درخواست‌کننده_مستقیماً_لغو_می‌شود()
    {
        var start = FutureWholeHour();
        var missionId = await CreateRequestAsync(start, start.AddHours(2));
        await _service.StartReviewAsync(missionId, "fleet-reviewer");

        await _service.CancelPendingRequestAsync(
            missionId, "integration.requester", "دیگر نیازی به مأموریت نیست");

        await using var db = await _factory.CreateDbContextAsync();
        var mission = await db.Missions.SingleAsync(item => item.Id == missionId);
        Assert.Equal(MissionStatus.Cancelled, mission.Status);
        Assert.Equal("دیگر نیازی به مأموریت نیست", mission.DecisionReason);
        Assert.Null(mission.StatusBeforeWithdrawal);
    }

    [Fact]
    public async Task لغو_مدیریتی_درخواست_انصراف_در_انتظار_را_نهایی_و_وضعیت_موقت_را_پاک_می‌کند()
    {
        var resources = await AddResourcesAsync(driverCount: 1, vehicleCount: 1);
        var start = FutureWholeHour();
        var missionId = await CreateApprovedMissionAsync(
            resources.DriverIds[0], resources.VehicleIds[0], start, start.AddHours(2));

        await _service.RequestWithdrawalAsync(missionId, "integration.requester", "تغییر برنامه");
        await _service.CancelAsync(missionId, "لغو مدیریتی با فوریت", "fleet-reviewer");

        await using var db = await _factory.CreateDbContextAsync();
        var mission = await db.Missions.SingleAsync(item => item.Id == missionId);
        Assert.Equal(MissionStatus.Cancelled, mission.Status);
        Assert.Equal("لغو مدیریتی با فوریت", mission.DecisionReason);
        Assert.Null(mission.StatusBeforeWithdrawal);
    }

    [Fact]
    public async Task درخواست_انصراف_از_درخواست_تأییدنشده_رد_و_به_لغو_مستقیم_هدایت_می‌شود()
    {
        var start = FutureWholeHour();
        var missionId = await CreateRequestAsync(start, start.AddHours(2));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.RequestWithdrawalAsync(
                missionId, "integration.requester", "تغییر برنامه"));

        await _service.StartReviewAsync(missionId, "fleet-reviewer");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.RequestWithdrawalAsync(
                missionId, "integration.requester", "تغییر برنامه"));

        await using var db = await _factory.CreateDbContextAsync();
        var mission = await db.Missions.SingleAsync(item => item.Id == missionId);
        Assert.Equal(MissionStatus.UnderReview, mission.Status);
    }

    /// <summary>ایجاد درخواست و تخصیص منابع برای سناریوهای چرخه عمر.</summary>
    private async Task<Guid> CreateApprovedMissionAsync(
        Guid driverId,
        Guid vehicleId,
        DateTimeOffset start,
        DateTimeOffset end)
    {
        var missionId = await CreateRequestAsync(start, end);
        await _service.ApproveAsync(missionId, driverId, vehicleId, "integration-test");
        return missionId;
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
        // زمان ورودی باید روی ساعت کامل تهران باشد؛ ۹ صبح تهران معادل ۵:۳۰ UTC است.
        var tehranOffset = TimeSpan.FromHours(3.5);
        var tehranDate = DateTimeOffset.UtcNow.ToOffset(tehranOffset).Date.AddDays(2);
        return new DateTimeOffset(tehranDate.AddHours(9), tehranOffset).ToUniversalTime();
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

        public static async Task<SqlServerTestDatabase> CreateAsync(bool applyMigrations = false)
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
            if (applyMigrations)
            {
                await db.Database.MigrateAsync();
            }
            else
            {
                // آزمون‌های رفتاری از مدل جاری استفاده می‌کنند؛ آزمون زنجیره Migration مستقل است.
                await db.Database.EnsureCreatedAsync();
            }
            return database;
        }

        public async ValueTask DisposeAsync()
        {
            await using var db = new FleetDbContext(Options);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
