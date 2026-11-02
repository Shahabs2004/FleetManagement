using FleetManagement.Application.Auditing;
using FleetManagement.Application.Missions;
using FleetManagement.Domain.Missions;

namespace FleetManagement.Domain.Tests.Application;

/// <summary>
/// آزمون قواعد کاربردی ثبت کیلومترشمار و پیوستگی کیلومتر خودرو.
/// </summary>
public sealed class MissionOdometerServiceTests
{
    [Fact]
    public async Task SetOdometerAsync_WhenStartIsBelowPreviousMissionEnd_RejectsUpdate()
    {
        var mission = CreateApprovedMission();
        var store = new FakeMissionOdometerStore(mission) { PreviousEndKm = 250 };
        var audit = new RecordingAuditLogWriter();
        var service = new MissionOdometerService(store, audit);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SetOdometerAsync(mission.Id, 200, null, "operator"));

        Assert.Contains("آخرین مقدار ثبت‌شده", exception.Message);
        Assert.Null(mission.OdometerStartKm);
        Assert.False(store.Committed);
        Assert.False(audit.WasCalled);
    }

    [Fact]
    public async Task SetOdometerAsync_WhenEndExceedsNextMissionStart_RejectsUpdate()
    {
        var mission = CreateApprovedMission();
        var store = new FakeMissionOdometerStore(mission) { NextStartKm = 400 };
        var audit = new RecordingAuditLogWriter();
        var service = new MissionOdometerService(store, audit);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SetOdometerAsync(mission.Id, 200, 450, "operator"));

        Assert.Contains("مأموریت بعدی", exception.Message);
        Assert.Null(mission.OdometerStartKm);
        Assert.False(store.Committed);
        Assert.False(audit.WasCalled);
    }

    [Fact]
    public async Task SetOdometerAsync_WhenValuesPreserveContinuity_UpdatesAndAudits()
    {
        var mission = CreateApprovedMission();
        var store = new FakeMissionOdometerStore(mission)
        {
            PreviousEndKm = 100,
            NextStartKm = 500
        };
        var audit = new RecordingAuditLogWriter();
        var service = new MissionOdometerService(store, audit);

        await service.SetOdometerAsync(mission.Id, 200, 300, "operator");

        Assert.True(store.Committed);
        Assert.Equal(200, mission.OdometerStartKm);
        Assert.Equal(300, mission.OdometerEndKm);
        Assert.Equal(100, mission.DistanceKm);
        Assert.True(audit.WasCalled);
        Assert.Equal("operator", audit.ActorUsername);
        Assert.Equal("UpdateOdometer", audit.Action);
        Assert.Equal("Mission", audit.EntityType);
        Assert.Equal("Success", audit.Outcome);
        Assert.Contains("\"DistanceKm\":100", audit.AfterJson);
    }

    [Fact]
    public async Task SetOdometerAsync_WhenMissionHasNoVehicle_RejectsOdometerValues()
    {
        var mission = CreatePendingMission();
        var store = new FakeMissionOdometerStore(mission);
        var audit = new RecordingAuditLogWriter();
        var service = new MissionOdometerService(store, audit);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SetOdometerAsync(mission.Id, 100, null, "operator"));

        Assert.False(store.Committed);
        Assert.False(audit.WasCalled);
    }

    private static Mission CreatePendingMission()
    {
        var now = DateTimeOffset.UtcNow;
        var start = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, TimeSpan.Zero).AddHours(2);
        return new Mission(
            "۱۴۰۵-۹۹۹",
            "آزمون کیلومتر",
            "requester",
            "درخواست‌کننده",
            "09120000000",
            "واحد",
            "گروه",
            MissionType.Passenger,
            "آزمون",
            "مبدأ",
            "مقصد",
            null,
            start,
            start.AddHours(2));
    }

    private static Mission CreateApprovedMission()
    {
        var mission = CreatePendingMission();
        mission.Approve(Guid.NewGuid(), Guid.NewGuid(), "reviewer", DateTimeOffset.UtcNow);
        return mission;
    }

    private sealed class FakeMissionOdometerStore(Mission mission) : IMissionOdometerStore
    {
        private readonly Mission _mission = mission;
        public long? PreviousEndKm { get; init; }
        public long? NextStartKm { get; init; }
        public bool Committed { get; private set; }

        public async Task<TResult> ExecuteSerializableAsync<TResult>(
            Func<IMissionOdometerUnitOfWork, CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken = default)
        {
            var result = await operation(new FakeUnitOfWork(this), cancellationToken);
            Committed = true;
            return result;
        }

        private sealed class FakeUnitOfWork(FakeMissionOdometerStore owner) : IMissionOdometerUnitOfWork
        {
            public Task<Mission?> GetMissionAsync(Guid missionId, CancellationToken cancellationToken = default) =>
                Task.FromResult<Mission?>(owner._mission.Id == missionId ? owner._mission : null);

            public Task<long?> GetPreviousEndOdometerAsync(
                Guid missionId,
                Guid vehicleId,
                DateTimeOffset beforeOrAtUtc,
                CancellationToken cancellationToken = default) =>
                Task.FromResult(owner.PreviousEndKm);

            public Task<long?> GetNextStartOdometerAsync(
                Guid missionId,
                Guid vehicleId,
                DateTimeOffset afterOrAtUtc,
                CancellationToken cancellationToken = default) =>
                Task.FromResult(owner.NextStartKm);
        }
    }

    private sealed class RecordingAuditLogWriter : IAuditLogWriter
    {
        public bool WasCalled { get; private set; }
        public string? ActorUsername { get; private set; }
        public string? Action { get; private set; }
        public string? EntityType { get; private set; }
        public string? Outcome { get; private set; }
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
            cancellationToken.ThrowIfCancellationRequested();
            WasCalled = true;
            ActorUsername = actorUsername;
            Action = action;
            EntityType = entityType;
            Outcome = outcome;
            AfterJson = afterJson;
            return Task.FromResult(Guid.NewGuid());
        }
    }
}
