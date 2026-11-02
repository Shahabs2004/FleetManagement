using FleetManagement.Application.Auditing;
using FleetManagement.Application.Configuration;
using FleetManagement.Application.Missions;
using FleetManagement.Application.Notifications;
using FleetManagement.Domain.Missions;
using FleetManagement.Domain.Notifications;

namespace FleetManagement.Domain.Tests.Application;

/// <summary>
/// آزمون سناریوهای Application برای ثبت و بررسی درخواست اولیه مأموریت.
/// </summary>
public sealed class MissionRequestReviewServiceTests
{
    [Fact]
    public async Task ثبت_درخواست_شماره_و_ممیزی_و_اعلان_عملیاتی_را_ایجاد_می_کند()
    {
        var store = new FakeRequestStore();
        var audit = new RecordingAuditWriter();
        var notifications = new RecordingNotificationSender();
        var service = new MissionRequestService(
            new FixedRulesService(CreateRules(1, 90)),
            store,
            audit,
            notifications);
        var (start, end) = GetFutureWindow();

        var id = await service.CreateRequestAsync(
            "مأموریت آزمایشی",
            "a.user",
            "کاربر آزمایشی",
            "09120000000",
            "واحد فناوری",
            "رایانه",
            MissionType.Passenger,
            "جلسه کاری",
            "ساختمان مرکزی",
            "ساختمان شماره دو",
            [],
            start,
            end,
            null,
            2,
            null,
            null,
            "a.user");

        Assert.NotEqual(Guid.Empty, id);
        Assert.NotNull(store.AddedMission);
        Assert.Equal(id, store.AddedMission.Id);
        Assert.Equal("1405-123", store.AddedMission.MissionNumber);
        Assert.Equal(MissionStatus.Pending, store.AddedMission.Status);
        Assert.Equal("CreateRequest", audit.LastAction);
        Assert.Equal(1, notifications.CallCount);
        Assert.Equal("MissionRequestCreated", notifications.LastEventKey);
        Assert.Equal(new[] { "FleetManager", "Operator" }, notifications.LastRoleKeys);
    }

    [Fact]
    public async Task ثبت_درخواست_بیرون_از_افق_مجاز_پیش_از_ذخیره_رد_می_شود()
    {
        var store = new FakeRequestStore();
        var audit = new RecordingAuditWriter();
        var notifications = new RecordingNotificationSender();
        var service = new MissionRequestService(
            new FixedRulesService(CreateRules(1, 30)),
            store,
            audit,
            notifications);
        var start = DateTimeOffset.UtcNow.AddDays(31);
        var end = start.AddHours(2);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateRequestAsync(
                "مأموریت آزمایشی",
                "a.user",
                "کاربر آزمایشی",
                "09120000000",
                "واحد فناوری",
                "رایانه",
                MissionType.Passenger,
                "جلسه کاری",
                "ساختمان مرکزی",
                "ساختمان شماره دو",
                [],
                start,
                end,
                null,
                null,
                null,
                null,
                "a.user"));

        Assert.Null(store.AddedMission);
        Assert.Equal(0, audit.CallCount);
        Assert.Equal(0, notifications.CallCount);
    }

    [Fact]
    public async Task شروع_بررسی_درخواست_اولیه_و_ممیزی_را_ثبت_می_کند()
    {
        var mission = CreatePendingMission();
        var store = new FakeReviewStore { Mission = mission };
        var audit = new RecordingAuditWriter();
        var notifications = new RecordingNotificationSender();
        var sms = new RecordingSmsScheduler();
        var service = CreateReviewService(store, audit, notifications, sms);

        await service.StartReviewAsync(mission.Id, "reviewer");

        Assert.Equal(MissionStatus.UnderReview, mission.Status);
        Assert.NotNull(mission.ReviewStartedAtUtc);
        Assert.Equal("StartReview", audit.LastAction);
        Assert.Equal(0, notifications.CallCount);
        Assert.Equal(0, sms.CallCount);
    }

    [Fact]
    public async Task رد_درخواست_اولیه_علت_و_اعلان_و_پیامک_را_ثبت_می_کند()
    {
        var mission = CreatePendingMission();
        var store = new FakeReviewStore { Mission = mission };
        var audit = new RecordingAuditWriter();
        var notifications = new RecordingNotificationSender();
        var sms = new RecordingSmsScheduler();
        var service = CreateReviewService(store, audit, notifications, sms);

        await service.RejectAsync(mission.Id, "مدارک کافی نیست", "reviewer");

        Assert.Equal(MissionStatus.Rejected, mission.Status);
        Assert.Equal("مدارک کافی نیست", mission.DecisionReason);
        Assert.Equal("Reject", audit.LastAction);
        Assert.Equal("a.user", notifications.LastRecipient);
        Assert.Equal("MissionRejected", notifications.LastEventKey);
        Assert.Equal(2, notifications.CallCount);
        Assert.Equal(1, sms.CallCount);
        Assert.Equal(SmsEventType.MissionRejected, sms.LastEventType);
    }

    [Fact]
    public async Task تأیید_بدون_راننده_موجود_هیچ_تغییر_یا_اعلانی_ایجاد_نمی_کند()
    {
        var mission = CreatePendingMission();
        var store = new FakeReviewStore { Mission = mission };
        var audit = new RecordingAuditWriter();
        var notifications = new RecordingNotificationSender();
        var sms = new RecordingSmsScheduler();
        var service = CreateReviewService(store, audit, notifications, sms);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.ApproveAsync(mission.Id, Guid.NewGuid(), Guid.NewGuid(), "reviewer"));

        Assert.Equal(MissionStatus.Pending, mission.Status);
        Assert.Equal(0, audit.CallCount);
        Assert.Equal(0, notifications.CallCount);
        Assert.Equal(0, sms.CallCount);
    }

    private static MissionReviewService CreateReviewService(
        FakeReviewStore store,
        RecordingAuditWriter audit,
        RecordingNotificationSender notifications,
        RecordingSmsScheduler sms) =>
        new(
            new FixedRulesService(CreateRules(1, 90)),
            store,
            audit,
            notifications,
            sms);

    private static (DateTimeOffset Start, DateTimeOffset End) GetFutureWindow()
    {
        var todayUtc = DateTimeOffset.UtcNow;
        var start = new DateTimeOffset(
            todayUtc.Year,
            todayUtc.Month,
            todayUtc.Day,
            0,
            30,
            0,
            TimeSpan.Zero).AddDays(2);

        return (start, start.AddHours(4));
    }

    private static Mission CreatePendingMission()
    {
        var (start, end) = GetFutureWindow();
        return new Mission(
            "1405-123",
            "مأموریت آزمایشی",
            "a.user",
            "کاربر آزمایشی",
            "09120000000",
            "واحد فناوری",
            "رایانه",
            MissionType.Passenger,
            "جلسه کاری",
            "ساختمان مرکزی",
            "ساختمان شماره دو",
            [],
            start,
            end);
    }

    private static MissionRulesConfiguration CreateRules(int minimumGapHours, int bookingHorizonDays) => new()
    {
        MinimumGapHours = minimumGapHours,
        BookingHorizonDays = bookingHorizonDays
    };

    private sealed class FixedRulesService(MissionRulesConfiguration rules) : IMissionRulesService
    {
        public Task<MissionRulesConfiguration> GetRulesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(rules);

        public Task SaveRulesAsync(
            MissionRulesConfiguration value,
            string actor,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeRequestStore : IMissionRequestStore
    {
        public Mission? AddedMission { get; private set; }

        public Task<TResult> ExecuteSerializableAsync<TResult>(
            Func<IMissionRequestUnitOfWork, CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken = default) =>
            operation(new FakeRequestUnitOfWork(this), cancellationToken);

        private sealed class FakeRequestUnitOfWork(FakeRequestStore store) : IMissionRequestUnitOfWork
        {
            public Task<string> GenerateMissionNumberAsync(
                DateTimeOffset nowUtc,
                CancellationToken cancellationToken = default) =>
                Task.FromResult("1405-123");

            public void Add(Mission mission) => store.AddedMission = mission;
        }
    }

    private sealed class FakeReviewStore : IMissionReviewStore
    {
        public Mission? Mission { get; init; }

        public Task<TResult> ExecuteSerializableAsync<TResult>(
            Func<IMissionReviewUnitOfWork, CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken = default) =>
            operation(new FakeReviewUnitOfWork(Mission), cancellationToken);

        private sealed class FakeReviewUnitOfWork(Mission? mission) : IMissionReviewUnitOfWork
        {
            public Task<Mission?> GetMissionAsync(Guid missionId, CancellationToken cancellationToken = default) =>
                Task.FromResult(mission?.Id == missionId ? mission : null);

            public Task<FleetManagement.Domain.Drivers.Driver?> GetDriverAsync(
                Guid driverId,
                CancellationToken cancellationToken = default) =>
                Task.FromResult<FleetManagement.Domain.Drivers.Driver?>(null);

            public Task<FleetManagement.Domain.Vehicles.Vehicle?> GetVehicleAsync(
                Guid vehicleId,
                CancellationToken cancellationToken = default) =>
                Task.FromResult<FleetManagement.Domain.Vehicles.Vehicle?>(null);

            public Task<bool> HasResourceConflictAsync(
                Mission current,
                Guid driverId,
                Guid vehicleId,
                TimeSpan minimumGap,
                CancellationToken cancellationToken = default) =>
                Task.FromResult(false);
        }
    }

    private sealed class RecordingAuditWriter : IAuditLogWriter
    {
        public int CallCount { get; private set; }
        public string? LastAction { get; private set; }

        public Task<Guid> RecordAsync(
            string actorUsername,
            string action,
            string entityType,
            string? entityId,
            string outcome,
            string? reason = null,
            string? beforeJson = null,
            string? afterJson = null,
            string? clientIpAddress = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastAction = action;
            return Task.FromResult(Guid.NewGuid());
        }
    }

    private sealed class RecordingNotificationSender : IMissionNotificationSender
    {
        public int CallCount { get; private set; }
        public string? LastRecipient { get; private set; }
        public string? LastEventKey { get; private set; }
        public string[] LastRoleKeys { get; private set; } = [];

        public Task CreateAsync(
            string recipientUsername,
            string title,
            string message,
            string eventKey,
            string? referenceType = null,
            string? referenceId = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastRecipient = recipientUsername;
            LastEventKey = eventKey;
            return Task.CompletedTask;
        }

        public Task<int> CreateForRolesAsync(
            IEnumerable<string> roleKeys,
            string title,
            string message,
            string eventKey,
            string? referenceType = null,
            string? referenceId = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastEventKey = eventKey;
            LastRoleKeys = roleKeys.ToArray();
            return Task.FromResult(1);
        }
    }

    private sealed class RecordingSmsScheduler : IMissionSmsScheduler
    {
        public int CallCount { get; private set; }
        public SmsEventType? LastEventType { get; private set; }

        public Task QueueSafelyAsync(
            Mission mission,
            SmsEventType eventType,
            string? reason,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastEventType = eventType;
            return Task.CompletedTask;
        }
    }
}
