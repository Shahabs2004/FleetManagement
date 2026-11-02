using FleetManagement.Application.Auditing;
using FleetManagement.Application.Missions;
using FleetManagement.Application.Notifications;
using FleetManagement.Domain.Missions;
using FleetManagement.Domain.Notifications;

namespace FleetManagement.Domain.Tests.Application;

/// <summary>آزمون قواعد کاربردی لغو، انصراف و خاتمه زودهنگام مأموریت.</summary>
public sealed class MissionWithdrawalServiceTests
{
    [Fact]
    public async Task CancelPendingRequestAsync_WhenRequesterOwnsPendingMission_CancelsAndAudits()
    {
        var mission = CreatePendingMission();
        var store = new FakeStore(mission);
        var audit = new RecordingAuditWriter();
        var notifications = new RecordingNotificationSender();
        var sms = new RecordingSmsScheduler();
        var service = CreateService(store, audit, notifications, sms);

        await service.CancelPendingRequestAsync(mission.Id, mission.RequesterUsername, "لغو درخواست");

        Assert.True(store.Committed);
        Assert.Equal(MissionStatus.Cancelled, mission.Status);
        Assert.Equal("CancelPendingRequest", audit.LastAction);
        Assert.Empty(notifications.EventKeys);
        Assert.Empty(sms.QueuedEvents);
    }

    [Fact]
    public async Task CancelPendingRequestAsync_WhenRequesterIsNotOwner_RejectsRequest()
    {
        var mission = CreatePendingMission();
        var store = new FakeStore(mission);
        var service = CreateService(store, new RecordingAuditWriter(), new RecordingNotificationSender(), new RecordingSmsScheduler());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CancelPendingRequestAsync(mission.Id, "another.user", "لغو درخواست"));

        Assert.False(store.Committed);
    }

    [Fact]
    public async Task RequestWithdrawalAsync_WhenMissionIsPending_RequiresDirectCancellation()
    {
        var mission = CreatePendingMission();
        var store = new FakeStore(mission);
        var service = CreateService(store, new RecordingAuditWriter(), new RecordingNotificationSender(), new RecordingSmsScheduler());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RequestWithdrawalAsync(mission.Id, mission.RequesterUsername, "انصراف"));

        Assert.False(store.Committed);
    }

    [Fact]
    public async Task RequestWithdrawalAsync_WhenConfirmedMissionIsOwned_RequestsWithdrawalAndNotifiesRoles()
    {
        var mission = CreateConfirmedMission();
        var store = new FakeStore(mission);
        var audit = new RecordingAuditWriter();
        var notifications = new RecordingNotificationSender();
        var service = CreateService(store, audit, notifications, new RecordingSmsScheduler());

        await service.RequestWithdrawalAsync(mission.Id, mission.RequesterUsername, "تغییر برنامه");

        Assert.True(store.Committed);
        Assert.Equal(MissionStatus.WithdrawalPending, mission.Status);
        Assert.Equal("RequestWithdrawal", audit.LastAction);
        Assert.Contains("MissionWithdrawalRequested", notifications.EventKeys);
    }

    [Fact]
    public async Task ApproveWithdrawalAsync_WhenWithdrawalIsPending_CancelsMissionAndReminder()
    {
        var mission = CreateWithdrawalPendingMission();
        var store = new FakeStore(mission);
        var audit = new RecordingAuditWriter();
        var notifications = new RecordingNotificationSender();
        var sms = new RecordingSmsScheduler();
        var service = CreateService(store, audit, notifications, sms);

        await service.ApproveWithdrawalAsync(mission.Id, "reviewer");

        Assert.True(store.Committed);
        Assert.Equal(MissionStatus.Cancelled, mission.Status);
        Assert.Null(mission.StatusBeforeWithdrawal);
        Assert.Equal("ApproveWithdrawal", audit.LastAction);
        Assert.Contains("WithdrawalApproved", notifications.EventKeys);
        Assert.Contains(SmsEventType.WithdrawalApproved, sms.QueuedEvents);
        Assert.Equal(1, sms.CancelReminderCount);
    }

    [Fact]
    public async Task RejectWithdrawalAsync_RestoresPreviousStatusAndQueuesNotification()
    {
        var mission = CreateWithdrawalPendingMission();
        var store = new FakeStore(mission);
        var audit = new RecordingAuditWriter();
        var notifications = new RecordingNotificationSender();
        var sms = new RecordingSmsScheduler();
        var service = CreateService(store, audit, notifications, sms);

        await service.RejectWithdrawalAsync(mission.Id, "علت رد", "reviewer");

        Assert.True(store.Committed);
        Assert.Equal(MissionStatus.Confirmed, mission.Status);
        Assert.Null(mission.StatusBeforeWithdrawal);
        Assert.Equal("علت رد", mission.DecisionReason);
        Assert.Equal("RejectWithdrawal", audit.LastAction);
        Assert.Equal("Rejected", audit.LastOutcome);
        Assert.Contains("WithdrawalRejected", notifications.EventKeys);
        Assert.Contains(SmsEventType.WithdrawalRejected, sms.QueuedEvents);
    }

    [Fact]
    public async Task CancelAsync_RecordsNotificationsAndCancelsUnsentReminders()
    {
        var mission = CreateConfirmedMission();
        var store = new FakeStore(mission);
        var audit = new RecordingAuditWriter();
        var notifications = new RecordingNotificationSender();
        var sms = new RecordingSmsScheduler();
        var service = CreateService(store, audit, notifications, sms);

        await service.CancelAsync(mission.Id, "لغو مدیریتی", "manager");

        Assert.True(store.Committed);
        Assert.Equal(MissionStatus.Cancelled, mission.Status);
        Assert.Equal("Cancel", audit.LastAction);
        Assert.Contains("MissionCancelled", notifications.EventKeys);
        Assert.Contains(SmsEventType.MissionCancelled, sms.QueuedEvents);
        Assert.Equal(1, sms.CancelReminderCount);
    }

    [Fact]
    public async Task EndEarlyAsync_StoresCompletionAndBeforeAfterAudit()
    {
        var mission = CreateRunningMission();
        var store = new FakeStore(mission);
        var audit = new RecordingAuditWriter();
        var notifications = new RecordingNotificationSender();
        var sms = new RecordingSmsScheduler();
        var service = CreateService(store, audit, notifications, sms);
        var endedAt = mission.PlannedStartAtUtc.AddHours(2);

        await service.EndEarlyAsync(mission.Id, endedAt, "پایان زودهنگام", "manager");

        Assert.True(store.Committed);
        Assert.Equal(MissionStatus.Completed, mission.Status);
        Assert.Equal(endedAt, mission.ActualEndAtUtc);
        Assert.Equal("EndEarly", audit.LastAction);
        Assert.NotNull(audit.BeforeJson);
        Assert.Contains("ActualEndAtUtc", audit.AfterJson);
        Assert.Contains("MissionEndedEarly", notifications.EventKeys);
        Assert.Contains(SmsEventType.MissionCompleted, sms.QueuedEvents);
    }

    private static MissionWithdrawalService CreateService(
        FakeStore store,
        RecordingAuditWriter audit,
        RecordingNotificationSender notifications,
        RecordingSmsScheduler sms) =>
        new(store, audit, notifications, sms);

    private static Mission CreatePendingMission()
    {
        var start = FutureWholeHour();
        return CreateMission("1405-101", start, start.AddHours(2));
    }

    private static Mission CreateConfirmedMission()
    {
        var start = FutureWholeHour();
        var mission = CreateMission("1405-102", start, start.AddHours(2));
        mission.Approve(Guid.NewGuid(), Guid.NewGuid(), "reviewer", start.AddHours(-1));
        return mission;
    }

    private static Mission CreateRunningMission()
    {
        var start = PastWholeHour();
        var mission = CreateMission("1405-103", start, start.AddHours(4));
        mission.Approve(Guid.NewGuid(), Guid.NewGuid(), "reviewer", start.AddHours(-1));
        mission.StartAutomatically(start);
        return mission;
    }

    private static Mission CreateWithdrawalPendingMission()
    {
        var mission = CreateConfirmedMission();
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

    private static DateTimeOffset FutureWholeHour() => TehranWholeHour(2);
    private static DateTimeOffset PastWholeHour() => TehranWholeHour(-2);

    private static DateTimeOffset TehranWholeHour(int dayOffset)
    {
        var offset = TimeSpan.FromMinutes(210);
        var date = DateTimeOffset.UtcNow.ToOffset(offset).Date.AddDays(dayOffset);
        return new DateTimeOffset(date.AddHours(9), offset).ToUniversalTime();
    }

    private sealed class FakeStore(Mission mission) : IMissionWithdrawalStore
    {
        public bool Committed { get; private set; }

        public async Task<TResult> ExecuteSerializableAsync<TResult>(
            Func<IMissionWithdrawalUnitOfWork, CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken = default)
        {
            var result = await operation(new FakeUnitOfWork(mission), cancellationToken);
            Committed = true;
            return result;
        }

        private sealed class FakeUnitOfWork(Mission mission) : IMissionWithdrawalUnitOfWork
        {
            public Task<Mission?> GetMissionAsync(
                Guid missionId,
                CancellationToken cancellationToken = default) =>
                Task.FromResult<Mission?>(mission.Id == missionId ? mission : null);
        }
    }

    private sealed class RecordingAuditWriter : IAuditLogWriter
    {
        public string? LastAction { get; private set; }
        public string? LastOutcome { get; private set; }
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
            LastOutcome = outcome;
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
