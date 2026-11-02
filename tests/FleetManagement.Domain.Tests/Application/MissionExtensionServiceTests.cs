using FleetManagement.Application.Auditing;
using FleetManagement.Application.Configuration;
using FleetManagement.Application.Missions;
using FleetManagement.Application.Notifications;
using FleetManagement.Domain.Missions;
using FleetManagement.Domain.Notifications;

namespace FleetManagement.Domain.Tests.Application;

/// <summary>آزمون قواعد کاربردی تمدید مأموریت و کنترل تعارض منابع.</summary>
public sealed class MissionExtensionServiceTests
{
    [Fact]
    public async Task ExtendAsync_WhenMissionHasNoAssignedResources_RejectsExtension()
    {
        var start = PastWholeHour();
        var mission = CreateMission("1405-301", start, start.AddHours(2));
        var store = new FakeStore(mission);
        var service = CreateService(store, new MissionRulesConfiguration(), new RecordingAuditWriter(),
            new RecordingNotificationSender(), new RecordingSmsScheduler());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ExtendAsync(mission.Id, mission.PlannedEndAtUtc.AddHours(2), "تمدید", "manager"));

        Assert.False(store.Committed);
    }

    [Fact]
    public async Task ExtendAsync_WhenNewEndConflictsWithAnotherActiveMission_RejectsExtension()
    {
        var mission = CreateRunningMission();
        var candidateStart = mission.PlannedEndAtUtc.AddHours(1);
        var candidate = CreateConfirmedMission(
            "1405-302",
            mission.DriverId!.Value,
            mission.VehicleId!.Value,
            candidateStart,
            candidateStart.AddHours(2));
        var store = new FakeStore(mission) { Candidates = [candidate] };
        var service = CreateService(store, new MissionRulesConfiguration(), new RecordingAuditWriter(),
            new RecordingNotificationSender(), new RecordingSmsScheduler());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ExtendAsync(mission.Id, mission.PlannedEndAtUtc.AddHours(2), "تمدید", "manager"));

        Assert.False(store.Committed);
    }

    [Fact]
    public async Task ExtendAsync_WhenMinimumGapWouldBeViolated_RejectsExtension()
    {
        var mission = CreateRunningMission();
        var candidateStart = mission.PlannedEndAtUtc.AddHours(3);
        var candidate = CreateConfirmedMission(
            "1405-303",
            mission.DriverId!.Value,
            mission.VehicleId!.Value,
            candidateStart,
            candidateStart.AddHours(2));
        var store = new FakeStore(mission) { Candidates = [candidate] };
        var service = CreateService(store, new MissionRulesConfiguration { MinimumGapHours = 2 },
            new RecordingAuditWriter(), new RecordingNotificationSender(), new RecordingSmsScheduler());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ExtendAsync(mission.Id, mission.PlannedEndAtUtc.AddHours(2), "تمدید", "manager"));

        Assert.False(store.Committed);
    }

    [Fact]
    public async Task ExtendAsync_WhenOnlyCompletedMissionOverlaps_ExtendsAndRecordsSideEffects()
    {
        var mission = CreateRunningMission();
        var candidateStart = mission.PlannedEndAtUtc.AddHours(1);
        var completedCandidate = CreateRunningMission(
            "1405-304",
            mission.DriverId!.Value,
            mission.VehicleId!.Value,
            candidateStart,
            candidateStart.AddHours(2));
        completedCandidate.CompleteAutomatically();

        var store = new FakeStore(mission) { Candidates = [completedCandidate] };
        var audit = new RecordingAuditWriter();
        var notifications = new RecordingNotificationSender();
        var sms = new RecordingSmsScheduler();
        var service = CreateService(store, new MissionRulesConfiguration(), audit, notifications, sms);
        var newEnd = mission.PlannedEndAtUtc.AddHours(2);

        await service.ExtendAsync(mission.Id, newEnd, "نیاز به زمان بیشتر", "manager");

        Assert.True(store.Committed);
        Assert.Equal(newEnd, mission.PlannedEndAtUtc);
        Assert.Equal("Extend", audit.LastAction);
        Assert.Contains("PlannedEndAtUtc", audit.BeforeJson);
        Assert.Contains("PlannedEndAtUtc", audit.AfterJson);
        Assert.Equal(2, notifications.EventKeys.Count(key => key == "MissionExtended"));
        Assert.Contains(SmsEventType.MissionChanged, sms.QueuedEvents);
    }

    [Fact]
    public async Task ExtendAsync_WhenNewEndDoesNotIncreaseDuration_RejectsExtension()
    {
        var mission = CreateRunningMission();
        var store = new FakeStore(mission);
        var service = CreateService(store, new MissionRulesConfiguration(), new RecordingAuditWriter(),
            new RecordingNotificationSender(), new RecordingSmsScheduler());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ExtendAsync(mission.Id, mission.PlannedEndAtUtc, "تمدید", "manager"));

        Assert.False(store.Committed);
    }

    private static MissionExtensionService CreateService(
        FakeStore store,
        MissionRulesConfiguration rules,
        RecordingAuditWriter audit,
        RecordingNotificationSender notifications,
        RecordingSmsScheduler sms) =>
        new(store, new FakeRulesService(rules), audit, notifications, sms);

    private static Mission CreateRunningMission()
    {
        var start = PastWholeHour();
        var driverId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        return CreateRunningMission("1405-300", driverId, vehicleId, start, start.AddHours(2));
    }

    private static Mission CreateRunningMission(
        string number,
        Guid driverId,
        Guid vehicleId,
        DateTimeOffset start,
        DateTimeOffset end)
    {
        var mission = CreateMission(number, start, end);
        mission.Approve(driverId, vehicleId, "reviewer", start.AddHours(-1));
        mission.StartAutomatically(start);
        return mission;
    }

    private static Mission CreateConfirmedMission(
        string number,
        Guid driverId,
        Guid vehicleId,
        DateTimeOffset start,
        DateTimeOffset end)
    {
        var mission = CreateMission(number, start, end);
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

    private static DateTimeOffset PastWholeHour()
    {
        var offset = TimeSpan.FromMinutes(210);
        var date = DateTimeOffset.UtcNow.ToOffset(offset).Date.AddDays(-2);
        return new DateTimeOffset(date.AddHours(9), offset).ToUniversalTime();
    }

    private sealed class FakeStore(Mission mission) : IMissionExtensionStore
    {
        public IReadOnlyList<Mission> Candidates { get; init; } = [];
        public bool Committed { get; private set; }

        public async Task<TResult> ExecuteSerializableAsync<TResult>(
            Func<IMissionExtensionUnitOfWork, CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken = default)
        {
            var result = await operation(new FakeUnitOfWork(mission, Candidates), cancellationToken);
            Committed = true;
            return result;
        }

        private sealed class FakeUnitOfWork(Mission mission, IReadOnlyList<Mission> candidates)
            : IMissionExtensionUnitOfWork
        {
            public Task<Mission?> GetMissionAsync(Guid missionId, CancellationToken cancellationToken = default) =>
                Task.FromResult<Mission?>(mission.Id == missionId ? mission : null);

            public Task<IReadOnlyList<Mission>> GetResourceConflictCandidatesAsync(
                Guid excludedMissionId,
                Guid driverId,
                Guid vehicleId,
                CancellationToken cancellationToken = default) =>
                Task.FromResult(candidates);
        }
    }

    private sealed class FakeRulesService(MissionRulesConfiguration rules) : IMissionRulesService
    {
        public Task<MissionRulesConfiguration> GetRulesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(rules);

        public Task SaveRulesAsync(
            MissionRulesConfiguration value,
            string actor,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RecordingAuditWriter : IAuditLogWriter
    {
        public string? LastAction { get; private set; }
        public string? BeforeJson { get; private set; }
        public string? AfterJson { get; private set; }

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
            LastAction = action;
            BeforeJson = beforeJson;
            AfterJson = afterJson;
            return Task.FromResult(Guid.NewGuid());
        }
    }

    private sealed class RecordingNotificationSender : IMissionNotificationSender
    {
        public List<string> EventKeys { get; } = [];

        public Task<Guid> CreateAsync(
            string recipientUsername,
            string title,
            string message,
            string eventKey,
            string? referenceType = null,
            string? referenceId = null,
            CancellationToken cancellationToken = default)
        {
            EventKeys.Add(eventKey);
            return Task.FromResult(Guid.NewGuid());
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
            EventKeys.Add(eventKey);
            return Task.FromResult(0);
        }
    }

    private sealed class RecordingSmsScheduler : IMissionSmsScheduler
    {
        public List<SmsEventType> QueuedEvents { get; } = [];

        public Task QueueSafelyAsync(
            Mission mission,
            SmsEventType eventType,
            string? reason,
            CancellationToken cancellationToken = default)
        {
            QueuedEvents.Add(eventType);
            return Task.CompletedTask;
        }

        public Task CancelUnsentRemindersSafelyAsync(
            Guid missionId,
            string reason,
            string actor,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
