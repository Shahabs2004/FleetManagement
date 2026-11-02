using FleetManagement.Application.Auditing;
using FleetManagement.Application.Missions;
using FleetManagement.Application.Notifications;
using FleetManagement.Domain.Missions;
using FleetManagement.Domain.Notifications;

namespace FleetManagement.Domain.Tests.Application;

/// <summary>آزمون انتقال‌های خودکار چرخه عمر مأموریت در Application.</summary>
public sealed class MissionLifecycleProcessorServiceTests
{
    [Fact]
    public async Task ProcessDueMissionsAsync_WhenConfirmedMissionIsDue_StartsAndAuditsIt()
    {
        var mission = CreateConfirmedMission();
        var store = new FakeStore([mission]);
        var audit = new RecordingAuditWriter();
        var notifications = new RecordingNotificationSender();
        var sms = new RecordingSmsScheduler();
        var service = CreateService(store, audit, notifications, sms);

        var count = await service.ProcessDueMissionsAsync();

        Assert.Equal(1, count);
        Assert.True(store.Committed);
        Assert.Equal(MissionStatus.Running, mission.Status);
        Assert.Contains("AutoStart", audit.Actions);
        Assert.Empty(notifications.EventKeys);
        Assert.Empty(sms.QueuedEvents);
    }

    [Fact]
    public async Task ProcessDueMissionsAsync_WhenRunningMissionIsDue_CompletesAndNotifies()
    {
        var mission = CreateRunningMission();
        var store = new FakeStore([mission]);
        var audit = new RecordingAuditWriter();
        var notifications = new RecordingNotificationSender();
        var sms = new RecordingSmsScheduler();
        var service = CreateService(store, audit, notifications, sms);

        var count = await service.ProcessDueMissionsAsync();

        Assert.Equal(1, count);
        Assert.Equal(MissionStatus.Completed, mission.Status);
        Assert.Contains("AutoComplete", audit.Actions);
        Assert.Contains("MissionCompleted", notifications.EventKeys);
        Assert.Contains(SmsEventType.MissionCompleted, sms.QueuedEvents);
    }

    [Fact]
    public async Task ProcessDueMissionsAsync_WhenWithdrawalExpires_ClosesAndCancelsReminder()
    {
        var mission = CreateWithdrawalPendingMission();
        var store = new FakeStore([mission]);
        var audit = new RecordingAuditWriter();
        var notifications = new RecordingNotificationSender();
        var sms = new RecordingSmsScheduler();
        var service = CreateService(store, audit, notifications, sms);

        var count = await service.ProcessDueMissionsAsync();

        Assert.Equal(1, count);
        Assert.Equal(MissionStatus.Cancelled, mission.Status);
        Assert.Null(mission.StatusBeforeWithdrawal);
        Assert.Contains("AutoCloseWithdrawal", audit.Actions);
        Assert.Contains(SmsEventType.MissionCancelled, sms.QueuedEvents);
        Assert.Equal(1, sms.CancelReminderCount);
        Assert.Empty(notifications.EventKeys);
    }

    [Fact]
    public async Task ProcessDueMissionsAsync_WhenThereAreNoDueMissions_ReturnsZeroWithoutSideEffects()
    {
        var store = new FakeStore([]);
        var audit = new RecordingAuditWriter();
        var notifications = new RecordingNotificationSender();
        var sms = new RecordingSmsScheduler();
        var service = CreateService(store, audit, notifications, sms);

        var count = await service.ProcessDueMissionsAsync();

        Assert.Equal(0, count);
        Assert.True(store.Committed);
        Assert.Empty(audit.Actions);
        Assert.Empty(notifications.EventKeys);
        Assert.Empty(sms.QueuedEvents);
    }

    private static MissionLifecycleProcessorService CreateService(
        FakeStore store,
        RecordingAuditWriter audit,
        RecordingNotificationSender notifications,
        RecordingSmsScheduler sms) =>
        new(store, audit, notifications, sms);

    private static Mission CreateConfirmedMission()
    {
        var start = PastWholeHour();
        var mission = CreateMission("1405-201", start, start.AddHours(2));
        mission.Approve(Guid.NewGuid(), Guid.NewGuid(), "reviewer", start.AddHours(-1));
        return mission;
    }

    private static Mission CreateRunningMission()
    {
        var start = PastWholeHour();
        var mission = CreateMission("1405-202", start, start.AddHours(2));
        mission.Approve(Guid.NewGuid(), Guid.NewGuid(), "reviewer", start.AddHours(-1));
        mission.StartAutomatically(start);
        return mission;
    }

    private static Mission CreateWithdrawalPendingMission()
    {
        var mission = CreateRunningMission();
        mission.RequestWithdrawal("درخواست انصراف");
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

    private sealed class FakeStore(IReadOnlyList<Mission> missions) : IMissionLifecycleProcessorStore
    {
        public bool Committed { get; private set; }

        public async Task<TResult> ExecuteSerializableAsync<TResult>(
            Func<IMissionLifecycleProcessorUnitOfWork, CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken = default)
        {
            var result = await operation(new FakeUnitOfWork(missions), cancellationToken);
            Committed = true;
            return result;
        }

        private sealed class FakeUnitOfWork(IReadOnlyList<Mission> missions)
            : IMissionLifecycleProcessorUnitOfWork
        {
            public Task<IReadOnlyList<Mission>> GetDueMissionsAsync(
                DateTimeOffset nowUtc,
                CancellationToken cancellationToken = default) =>
                Task.FromResult(missions);
        }
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
        public int CancelReminderCount { get; private set; }

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
            CancellationToken cancellationToken = default)
        {
            CancelReminderCount++;
            return Task.CompletedTask;
        }
    }
}
