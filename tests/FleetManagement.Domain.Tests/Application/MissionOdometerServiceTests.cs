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

    private static DateTimeOffset FutureWholeHour()
    {
        var tehranOffset = TimeSpan.FromHours(3.5);
        var tehranDate = DateTimeOffset.UtcNow.ToOffset(tehranOffset).Date.AddDays(2);
        return new DateTimeOffset(tehranDate.AddHours(9), tehranOffset).ToUniversalTime();
    }

    private sealed class FakeStore(Mission mission) : IMissionOdometerStore
    {
        public Task<TResult> ExecuteSerializableAsync<TResult>(
            Func<IMissionOdometerUnitOfWork, CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken = default)
            => operation(new FakeUnitOfWork(mission), cancellationToken);
    }

    private sealed class FakeUnitOfWork(Mission mission) : IMissionOdometerUnitOfWork
    {
        public Task<Mission?> GetMissionAsync(Guid missionId, CancellationToken cancellationToken = default)
            => Task.FromResult<Mission?>(mission.Id == missionId ? mission : null);

        public Task<long?> GetPreviousOdometerEndAsync(
            Guid missionId,
            Guid vehicleId,
            DateTimeOffset missionStartAtUtc,
            CancellationToken cancellationToken = default)
            => Task.FromResult<long?>(null);

        public Task<long?> GetNextOdometerStartAsync(
            Guid missionId,
            Guid vehicleId,
            DateTimeOffset effectiveEndAtUtc,
            CancellationToken cancellationToken = default)
            => Task.FromResult<long?>(null);
    }

    private sealed class RecordingAuditWriter : IAuditLogWriter
    {
        public string? Action { get; private set; }

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
            return Task.FromResult(Guid.NewGuid());
        }
    }
}
