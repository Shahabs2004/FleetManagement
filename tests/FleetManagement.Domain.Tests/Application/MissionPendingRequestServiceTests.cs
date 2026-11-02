using System.Text.Json;
using FleetManagement.Application.Auditing;
using FleetManagement.Application.Configuration;
using FleetManagement.Application.Missions;
using FleetManagement.Domain.Missions;

namespace FleetManagement.Domain.Tests.Application;

/// <summary>
/// آزمون سناریوی ویرایش درخواست مأموریت پیش از آغاز بررسی.
/// </summary>
public sealed class MissionPendingRequestServiceTests
{
    [Fact]
    public async Task UpdatePendingRequestAsync_WhenRequesterOwnsPendingMission_UpdatesAndAudits()
    {
        var mission = CreatePendingMission();
        var store = new FakeStore(mission, ["مبدأ جدید", "مقصد جدید", "توقف جدید"]);
        var audit = new RecordingAuditWriter();
        var service = new MissionPendingRequestService(
            new FixedRulesService(),
            store,
            audit);

        var start = FutureWholeHour();
        await service.UpdatePendingRequestAsync(
            mission.Id,
            "requester",
            "عنوان جدید",
            "09121111111",
            "گروه جدید",
            MissionType.Passenger,
            "هدف جدید",
            "مبدأ جدید",
            "مقصد جدید",
            ["توقف جدید"],
            start.AddHours(1),
            start.AddHours(3),
            "شرح جدید",
            2,
            "ORDER-22",
            new DateOnly(2026, 11, 2));

        Assert.Equal("عنوان جدید", mission.Title);
        Assert.Equal("مبدأ جدید", mission.Origin);
        Assert.Equal("مقصد جدید", mission.Destination);
        Assert.Contains("توقف جدید", JsonSerializer.Deserialize<string[]>(mission.RouteJson)!);
        Assert.Equal("ORDER-22", mission.MissionOrderNumber);
        Assert.Equal(MissionStatus.Pending, mission.Status);
        Assert.Equal("EditPendingRequest", audit.Action);
        Assert.Equal("Success", audit.Outcome);
        Assert.NotNull(audit.BeforeJson);
        Assert.NotNull(audit.AfterJson);
    }

    [Fact]
    public async Task UpdatePendingRequestAsync_WhenRequesterDoesNotOwnMission_RejectsWithoutChanges()
    {
        var mission = CreatePendingMission();
        var store = new FakeStore(mission, ["مبدأ جدید", "مقصد جدید"]);
        var audit = new RecordingAuditWriter();
        var service = new MissionPendingRequestService(
            new FixedRulesService(),
            store,
            audit);

        var start = FutureWholeHour();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.UpdatePendingRequestAsync(
                mission.Id,
                "another-user",
                "عنوان غیرمجاز",
                "09121111111",
                "گروه جدید",
                MissionType.Passenger,
                "هدف جدید",
                "مبدأ جدید",
                "مقصد جدید",
                [],
                start,
                start.AddHours(2),
                null,
                1,
                null,
                null));

        Assert.Equal("عنوان اولیه", mission.Title);
        Assert.Null(audit.Action);
    }

    [Fact]
    public async Task UpdatePendingRequestAsync_WhenLocationIsNotApproved_RejectsWithoutChanges()
    {
        var mission = CreatePendingMission();
        var store = new FakeStore(mission, ["مبدأ جدید"]);
        var audit = new RecordingAuditWriter();
        var service = new MissionPendingRequestService(
            new FixedRulesService(),
            store,
            audit);

        var start = FutureWholeHour();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdatePendingRequestAsync(
                mission.Id,
                "requester",
                "عنوان جدید",
                "09121111111",
                "گروه جدید",
                MissionType.Passenger,
                "هدف جدید",
                "مبدأ جدید",
                "مقصد جدید",
                [],
                start,
                start.AddHours(2),
                null,
                1,
                null,
                null));

        Assert.Equal("عنوان اولیه", mission.Title);
        Assert.Null(audit.Action);
    }

    private static Mission CreatePendingMission()
    {
        var start = FutureWholeHour();
        return new Mission(
            "1405-001",
            "عنوان اولیه",
            "requester",
            "درخواست‌کننده",
            "09120000000",
            "واحد آزمون",
            "گروه اولیه",
            MissionType.Passenger,
            "هدف اولیه",
            "مبدأ اولیه",
            "مقصد اولیه",
            [],
            start,
            start.AddHours(2));
    }

    private static DateTimeOffset FutureWholeHour()
    {
        var tehranOffset = TimeSpan.FromHours(3.5);
        var tehranDate = DateTimeOffset.UtcNow.ToOffset(tehranOffset).Date.AddDays(2);
        return new DateTimeOffset(tehranDate.AddHours(9), tehranOffset).ToUniversalTime();
    }

    private sealed class FixedRulesService : IMissionRulesService
    {
        public Task<MissionRulesConfiguration> GetRulesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new MissionRulesConfiguration
            {
                MinimumGapHours = 0,
                BookingHorizonDays = 90
            });

        public Task SaveRulesAsync(
            MissionRulesConfiguration rules,
            string actor,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeStore(
        Mission mission,
        string[] approvedLocations) : IMissionPendingRequestStore
    {
        public Task<TResult> ExecuteSerializableAsync<TResult>(
            Func<IMissionPendingRequestUnitOfWork, CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken = default)
            => operation(new FakeUnitOfWork(mission, approvedLocations), cancellationToken);
    }

    private sealed class FakeUnitOfWork(
        Mission mission,
        string[] approvedLocations) : IMissionPendingRequestUnitOfWork
    {
        public Task<Mission?> GetMissionAsync(
            Guid missionId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<Mission?>(mission.Id == missionId ? mission : null);
        }

        public Task<string[]> GetApprovedLocationNamesAsync(
            string[] requestedLocations,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var found = approvedLocations
                .Where(name => requestedLocations.Contains(name, StringComparer.OrdinalIgnoreCase))
                .ToArray();
            return Task.FromResult(found);
        }
    }

    private sealed class RecordingAuditWriter : IAuditLogWriter
    {
        public string? Action { get; private set; }
        public string? Outcome { get; private set; }
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
            cancellationToken.ThrowIfCancellationRequested();
            Action = action;
            Outcome = outcome;
            BeforeJson = beforeJson;
            AfterJson = afterJson;
            return Task.FromResult(Guid.NewGuid());
        }
    }
}
