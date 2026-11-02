using FleetManagement.Application.Auditing;
using FleetManagement.Application.Missions;
using FleetManagement.Domain.Missions;

namespace FleetManagement.Domain.Tests.Application;

/// <summary>آزمون سناریوی ثبت کیلومترشمار مأموریت.</summary>
public sealed class MissionOdometerServiceTests
{
    [Fact]
    public async Task SetOdometerAsync_WhenVehicleIsNotAssigned_RejectsOdometerValues()
    {
        var start = FutureWholeHour();
        var mission = new Mission(
            "1405-001",
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
            start.AddHours(2));
        var audit = new RecordingAuditWriter();
        var service = new MissionOdometerService(new FakeStore(mission), audit);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SetOdometerAsync(mission.Id, 100, 150, "operator"));

        Assert.Null(mission.OdometerStartKm);
        Assert.Null(mission.OdometerEndKm);
        Assert.Null(audit.Action);
    }


    [Fact]
    public async Task SetOdometerAsync_WhenStartIsBelowPreviousMissionEnd_RejectsUpdate()
    {
        var mission = CreateApprovedMission();
        var store = new FakeStore(mission) { PreviousEndKm = 250 };
        var audit = new RecordingAuditWriter();
        var service = new MissionOdometerService(store, audit);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SetOdometerAsync(mission.Id, 200, null, "operator"));

        Assert.Contains("آخرین مقدار ثبت‌شده", exception.Message);
        Assert.Null(mission.OdometerStartKm);
        Assert.False(store.Committed);
        Assert.Null(audit.Action);
    }

    [Fact]
    public async Task SetOdometerAsync_WhenEndExceedsNextMissionStart_RejectsUpdate()
    {
        var mission = CreateApprovedMission();
        var store = new FakeStore(mission) { NextStartKm = 400 };
        var audit = new RecordingAuditWriter();
        var service = new MissionOdometerService(store, audit);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SetOdometerAsync(mission.Id, 200, 450, "operator"));

        Assert.Contains("مأموریت بعدی", exception.Message);
        Assert.Null(mission.OdometerStartKm);
        Assert.False(store.Committed);
        Assert.Null(audit.Action);
    }

    [Fact]
    public async Task SetOdometerAsync_WhenValuesPreserveContinuity_UpdatesAndAudits()
    {
        var mission = CreateApprovedMission();
        var store = new FakeStore(mission) { PreviousEndKm = 100, NextStartKm = 500 };
        var audit = new RecordingAuditWriter();
        var service = new MissionOdometerService(store, audit);

        await service.SetOdometerAsync(mission.Id, 200, 300, "operator");

        Assert.True(store.Committed);
        Assert.Equal(200, mission.OdometerStartKm);
        Assert.Equal(300, mission.OdometerEndKm);
        Assert.Equal(100, mission.DistanceKm);
        Assert.Equal("UpdateOdometer", audit.Action);
        Assert.Contains("\"DistanceKm\":100", audit.AfterJson);
    }

    private static Mission CreateApprovedMission()
    {
        var start = FutureWholeHour();
        var mission = new Mission(
            "1405-002", "مأموریت آزمون", "requester", "درخواست‌کننده", "09120000000",
            "واحد آزمون", "گروه آزمون", MissionType.Passenger, "هدف آزمون", "مبدأ", "مقصد",
            [], start, start.AddHours(2));
        mission.Approve(Guid.NewGuid(), Guid.NewGuid(), "reviewer", DateTimeOffset.UtcNow);
        return mission;
    }

    private static DateTimeOffset FutureWholeHour()
    {
        var tehranOffset = TimeSpan.FromHours(3.5);
        var tehranDate = DateTimeOffset.UtcNow.ToOffset(tehranOffset).Date.AddDays(2);
        return new DateTimeOffset(tehranDate.AddHours(9), tehranOffset).ToUniversalTime();
    }

    private sealed class FakeStore(Mission mission) : IMissionOdometerStore
    {
        public Mission Mission { get; } = mission;
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
    }

    private sealed class FakeUnitOfWork(FakeStore owner) : IMissionOdometerUnitOfWork
    {
        public Task<Mission?> GetMissionAsync(Guid missionId, CancellationToken cancellationToken = default)
            => Task.FromResult<Mission?>(owner.Mission.Id == missionId ? owner.Mission : null);

        public Task<long?> GetPreviousOdometerEndAsync(
            Guid missionId,
            Guid vehicleId,
            DateTimeOffset missionStartAtUtc,
            CancellationToken cancellationToken = default)
            => Task.FromResult(owner.PreviousEndKm);

        public Task<long?> GetNextOdometerStartAsync(
            Guid missionId,
            Guid vehicleId,
            DateTimeOffset effectiveEndAtUtc,
            CancellationToken cancellationToken = default)
            => Task.FromResult(owner.NextStartKm);
    }

    private sealed class RecordingAuditWriter : IAuditLogWriter
    {
        public string? Action { get; private set; }
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
            Action = action;
            AfterJson = afterJson;
            return Task.FromResult(Guid.NewGuid());
        }
    }
}
