using FleetManagement.Application.Auditing;
using FleetManagement.Application.Configuration;
using FleetManagement.Application.Missions;
using FleetManagement.Application.Notifications;
using FleetManagement.Domain.Drivers;
using FleetManagement.Domain.Missions;
using FleetManagement.Domain.Vehicles;

namespace FleetManagement.Domain.Tests.Application;

/// <summary>آزمون قواعد کاربردی تقسیم مأموریت.</summary>
public sealed class MissionSplitServiceTests
{
    [Fact]
    public async Task SplitAsync_WhenResourcesChangeAndRulesPass_CreatesTwoLinkedRunningSegments()
    {
        var firstDriver = CreateDriver();
        var nextDriver = CreateDriver();
        var vehicle = CreateVehicle();
        var first = CreateRunningMission(firstDriver.Id, vehicle.Id);
        var store = new FakeStore(first, nextDriver, vehicle);
        var audit = new RecordingAuditWriter();
        var notifications = new RecordingNotificationSender();
        var service = CreateService(store, audit, notifications);

        var secondId = await service.SplitAsync(
            first.Id,
            nextDriver.Id,
            vehicle.Id,
            first.PlannedStartAtUtc.AddHours(2),
            125,
            null,
            MissionSplitReason.DriverChange,
            null,
            "operator");

        var second = store.AddedMission!;
        Assert.Equal(second.Id, secondId);
        Assert.True(store.Committed);
        Assert.Equal(MissionStatus.Completed, first.Status);
        Assert.Equal(MissionStatus.Running, second.Status);
        Assert.Equal(first.PlannedEndAtUtc, second.PlannedEndAtUtc);
        Assert.Equal(first.SplitAtUtc, second.PlannedStartAtUtc);
        Assert.Equal(125, first.OdometerEndKm);
        Assert.Equal(125, second.OdometerStartKm);
        Assert.Equal(second.Id, first.RelatedMissionId);
        Assert.Equal(first.Id, second.RelatedMissionId);
        Assert.Equal(new[] { "SplitFirstSegment", "SplitSecondSegment" }, audit.Actions);
        Assert.Equal("MissionSplit", notifications.EventKey);
    }

    [Fact]
    public async Task SplitAsync_WhenNeitherResourceChanges_RejectsRequest()
    {
        var driver = CreateDriver();
        var vehicle = CreateVehicle();
        var first = CreateRunningMission(driver.Id, vehicle.Id);
        var store = new FakeStore(first, driver, vehicle);
        var service = CreateService(store, new RecordingAuditWriter(), new RecordingNotificationSender());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SplitAsync(
            first.Id, driver.Id, vehicle.Id, first.PlannedStartAtUtc.AddHours(2), 125, null,
            MissionSplitReason.DriverChange, null, "operator"));

        Assert.False(store.Committed);
        Assert.Null(store.AddedMission);
    }

    [Fact]
    public async Task SplitAsync_WhenReasonDoesNotMatchResourceChange_RejectsRequest()
    {
        var firstDriver = CreateDriver();
        var nextDriver = CreateDriver();
        var firstVehicle = CreateVehicle();
        var nextVehicle = CreateVehicle();
        var first = CreateRunningMission(firstDriver.Id, firstVehicle.Id);
        var store = new FakeStore(first, nextDriver, nextVehicle);
        var service = CreateService(store, new RecordingAuditWriter(), new RecordingNotificationSender());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SplitAsync(
            first.Id, nextDriver.Id, nextVehicle.Id, first.PlannedStartAtUtc.AddHours(2), 125, 200,
            MissionSplitReason.DriverChange, null, "operator"));

        Assert.False(store.Committed);
    }

    [Fact]
    public async Task SplitAsync_WhenOtherReasonHasNoExplanation_RejectsRequest()
    {
        var firstDriver = CreateDriver();
        var nextDriver = CreateDriver();
        var vehicle = CreateVehicle();
        var first = CreateRunningMission(firstDriver.Id, vehicle.Id);
        var store = new FakeStore(first, nextDriver, vehicle);
        var service = CreateService(store, new RecordingAuditWriter(), new RecordingNotificationSender());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SplitAsync(
            first.Id, nextDriver.Id, vehicle.Id, first.PlannedStartAtUtc.AddHours(2), 125, null,
            MissionSplitReason.Other, " ", "operator"));

        Assert.False(store.Committed);
    }

    [Fact]
    public async Task SplitAsync_WhenNewDriverIsNotActive_RejectsRequest()
    {
        var firstDriver = CreateDriver();
        var nextDriver = CreateDriver();
        nextDriver.ChangeEmploymentStatus(DriverEmploymentStatus.OnLeave);
        var vehicle = CreateVehicle();
        var first = CreateRunningMission(firstDriver.Id, vehicle.Id);
        var store = new FakeStore(first, nextDriver, vehicle);
        var service = CreateService(store, new RecordingAuditWriter(), new RecordingNotificationSender());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SplitAsync(
            first.Id, nextDriver.Id, vehicle.Id, first.PlannedStartAtUtc.AddHours(2), 125, null,
            MissionSplitReason.DriverChange, null, "operator"));

        Assert.False(store.Committed);
    }

    [Fact]
    public async Task SplitAsync_WhenNewVehicleIsUnavailable_RejectsRequest()
    {
        var firstDriver = CreateDriver();
        var nextDriver = CreateDriver();
        var firstVehicle = CreateVehicle();
        var nextVehicle = CreateVehicle();
        nextVehicle.ChangeManualStatus(ManualVehicleStatus.RepairShop, hasActiveMission: false);
        var first = CreateRunningMission(firstDriver.Id, firstVehicle.Id);
        var store = new FakeStore(first, nextDriver, nextVehicle);
        var service = CreateService(store, new RecordingAuditWriter(), new RecordingNotificationSender());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SplitAsync(
            first.Id, nextDriver.Id, nextVehicle.Id, first.PlannedStartAtUtc.AddHours(2), 125, 200,
            MissionSplitReason.DriverAndVehicleChange, null, "operator"));

        Assert.False(store.Committed);
    }

    [Fact]
    public async Task SplitAsync_WhenVehicleChangesWithoutStartOdometer_RejectsRequest()
    {
        var firstDriver = CreateDriver();
        var nextDriver = CreateDriver();
        var firstVehicle = CreateVehicle();
        var nextVehicle = CreateVehicle();
        var first = CreateRunningMission(firstDriver.Id, firstVehicle.Id);
        var store = new FakeStore(first, nextDriver, nextVehicle);
        var service = CreateService(store, new RecordingAuditWriter(), new RecordingNotificationSender());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SplitAsync(
            first.Id, nextDriver.Id, nextVehicle.Id, first.PlannedStartAtUtc.AddHours(2), 125, null,
            MissionSplitReason.DriverAndVehicleChange, null, "operator"));

        Assert.False(store.Committed);
    }

    [Fact]
    public async Task SplitAsync_WhenNewVehicleOdometerIsBelowLastValue_RejectsRequest()
    {
        var firstDriver = CreateDriver();
        var nextDriver = CreateDriver();
        var firstVehicle = CreateVehicle();
        var nextVehicle = CreateVehicle();
        var first = CreateRunningMission(firstDriver.Id, firstVehicle.Id);
        var store = new FakeStore(first, nextDriver, nextVehicle) { LastOdometerEndKm = 250 };
        var service = CreateService(store, new RecordingAuditWriter(), new RecordingNotificationSender());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SplitAsync(
            first.Id, nextDriver.Id, nextVehicle.Id, first.PlannedStartAtUtc.AddHours(2), 125, 200,
            MissionSplitReason.DriverAndVehicleChange, null, "operator"));

        Assert.False(store.Committed);
    }

    [Fact]
    public async Task SplitAsync_WhenNewResourcesConflictWithAnotherMission_RejectsRequest()
    {
        var firstDriver = CreateDriver();
        var nextDriver = CreateDriver();
        var firstVehicle = CreateVehicle();
        var nextVehicle = CreateVehicle();
        var first = CreateRunningMission(firstDriver.Id, firstVehicle.Id);
        var conflict = CreateConfirmedMission(
            nextDriver.Id,
            nextVehicle.Id,
            first.PlannedStartAtUtc.AddHours(1),
            first.PlannedStartAtUtc.AddHours(3));
        var store = new FakeStore(first, nextDriver, nextVehicle) { Candidates = [conflict] };
        var service = CreateService(store, new RecordingAuditWriter(), new RecordingNotificationSender());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SplitAsync(
            first.Id, nextDriver.Id, nextVehicle.Id, first.PlannedStartAtUtc.AddHours(2), 125, 200,
            MissionSplitReason.DriverAndVehicleChange, null, "operator"));

        Assert.False(store.Committed);
    }

    private static MissionSplitService CreateService(
        FakeStore store,
        RecordingAuditWriter audit,
        RecordingNotificationSender notifications) =>
        new(store, new FakeRulesService(), audit, notifications);

    private static Mission CreateRunningMission(Guid driverId, Guid vehicleId)
    {
        var tehranOffset = TimeSpan.FromHours(3.5);
        var tehranDate = DateTimeOffset.UtcNow.ToOffset(tehranOffset).Date.AddDays(-2);
        var start = new DateTimeOffset(tehranDate.AddHours(9), tehranOffset).ToUniversalTime();
        var mission = CreateMission("1405-001", start, start.AddHours(4));
        mission.Approve(driverId, vehicleId, "reviewer", start.AddHours(-1));
        mission.StartAutomatically(start);
        mission.SetOdometerStart(100);
        return mission;
    }

    private static Mission CreateConfirmedMission(
        Guid driverId,
        Guid vehicleId,
        DateTimeOffset start,
        DateTimeOffset end)
    {
        var mission = CreateMission("1405-003", start, end);
        mission.Approve(driverId, vehicleId, "reviewer", start.AddHours(-1));
        return mission;
    }

    private static Mission CreateMission(string number, DateTimeOffset start, DateTimeOffset end) =>
        new(
            number,
            "مأموریت آزمون",
            "requester",
            "درخواست‌کننده",
            "09120000000",
            "واحد آزمون",
            "گروه آزمون",
            MissionType.Passenger,
            "هدف آزمون",
            "مبدأ",
            "مقصد",
            [],
            start,
            end);

    private static Driver CreateDriver() => new("راننده", "آزمون", "09120000000");
    private static Vehicle CreateVehicle() => new("ایران‌خودرو", "سمند", "۱۲ الف ۳۴۵ ایران ۶۷");

    private sealed class FakeStore : IMissionSplitStore
    {
        private readonly Mission _mission;
        private readonly Driver _driver;
        private readonly Vehicle _vehicle;

        public FakeStore(Mission mission, Driver driver, Vehicle vehicle)
        {
            _mission = mission;
            _driver = driver;
            _vehicle = vehicle;
        }

        public bool Committed { get; private set; }
        public Mission? AddedMission { get; private set; }
        public long? LastOdometerEndKm { get; init; }
        public IReadOnlyList<Mission> Candidates { get; init; } = [];

        public async Task<TResult> ExecuteSerializableAsync<TResult>(
            Func<IMissionSplitUnitOfWork, CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken = default)
        {
            var result = await operation(new FakeUnitOfWork(this), cancellationToken);
            Committed = true;
            return result;
        }

        private sealed class FakeUnitOfWork(FakeStore owner) : IMissionSplitUnitOfWork
        {
            public Task<Mission?> GetMissionAsync(Guid missionId, CancellationToken cancellationToken = default) =>
                Task.FromResult<Mission?>(owner._mission.Id == missionId ? owner._mission : null);

            public Task<Driver?> GetDriverAsync(Guid driverId, CancellationToken cancellationToken = default) =>
                Task.FromResult<Driver?>(owner._driver.Id == driverId ? owner._driver : null);

            public Task<Vehicle?> GetVehicleAsync(Guid vehicleId, CancellationToken cancellationToken = default) =>
                Task.FromResult<Vehicle?>(owner._vehicle.Id == vehicleId ? owner._vehicle : null);

            public Task<long?> GetLastOdometerEndAsync(Guid vehicleId, CancellationToken cancellationToken = default) =>
                Task.FromResult(owner.LastOdometerEndKm);

            public Task<string> GenerateMissionNumberAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken = default) =>
                Task.FromResult("1405-002");

            public Task<IReadOnlyList<Mission>> GetResourceConflictCandidatesAsync(
                Guid excludedMissionId,
                Guid driverId,
                Guid vehicleId,
                CancellationToken cancellationToken = default) =>
                Task.FromResult(owner.Candidates);

            public void Add(Mission mission) => owner.AddedMission = mission;
            public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        }
    }

    private sealed class FakeRulesService : IMissionRulesService
    {
        public Task<MissionRulesConfiguration> GetRulesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new MissionRulesConfiguration { MinimumGapHours = 0 });

        public Task SaveRulesAsync(
            MissionRulesConfiguration rules,
            string actor,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RecordingAuditWriter : IAuditLogWriter
    {
        public List<string> Actions { get; } = [];

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
            Actions.Add(action);
            return Task.FromResult(Guid.NewGuid());
        }
    }

    private sealed class RecordingNotificationSender : IMissionNotificationSender
    {
        public string? EventKey { get; private set; }

        public Task<Guid> CreateAsync(
            string recipientUsername,
            string title,
            string message,
            string eventKey,
            string? referenceType = null,
            string? referenceId = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Guid.NewGuid());

        public Task<int> CreateForRolesAsync(
            IEnumerable<string> roleKeys,
            string title,
            string message,
            string eventKey,
            string? referenceType = null,
            string? referenceId = null,
            CancellationToken cancellationToken = default)
        {
            EventKey = eventKey;
            return Task.FromResult(0);
        }
    }
}
