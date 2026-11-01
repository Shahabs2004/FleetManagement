using FleetManagement.Application.Auditing;
using FleetManagement.Application.Missions;
using FleetManagement.Application.Notifications;
using FleetManagement.Domain.Missions;

namespace FleetManagement.Domain.Tests.Application;

/// <summary>
/// آزمون سناریوهای Application برای ثبت و بررسی درخواست تغییر مأموریت.
/// </summary>
public sealed class MissionChangeRequestServiceTests
{
    private static readonly DateTimeOffset PlannedStart = new(2026, 11, 2, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset PlannedEnd = new(2026, 11, 2, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ثبت_درخواست_تغییر_برای_درخواست_کننده_مجاز_انجام_می_شود()
    {
        var mission = CreateConfirmedMission();
        var store = new FakeStore { Mission = mission };
        var audit = new RecordingAuditLogWriter();
        var service = CreateService(store, audit, new RecordingNotificationSender());

        var requestId = await service.RequestChangeAsync(
            mission.Id,
            "a.user",
            "اصلاح مقصد",
            CreateProposal());

        Assert.NotEqual(Guid.Empty, requestId);
        Assert.NotNull(store.AddedRequest);
        Assert.Equal(requestId, store.AddedRequest.Id);
        Assert.Equal(MissionChangeRequestStatus.Pending, store.AddedRequest.Status);
        Assert.Equal(1, audit.CallCount);
        Assert.Equal("RequestChange", audit.LastAction);
    }

    [Fact]
    public async Task ثبت_درخواست_تغییر_برای_کاربر_غیرمالک_رد_می_شود()
    {
        var store = new FakeStore { Mission = CreateConfirmedMission() };
        var audit = new RecordingAuditLogWriter();
        var service = CreateService(store, audit, new RecordingNotificationSender());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.RequestChangeAsync(store.Mission!.Id, "other.user", "اصلاح مقصد", CreateProposal()));

        Assert.Null(store.AddedRequest);
        Assert.Equal(0, audit.CallCount);
    }

    [Fact]
    public async Task شروع_بررسی_درخواست_را_برای_بازبین_قفل_می_کند()
    {
        var request = CreateChangeRequest();
        var store = new FakeStore { ChangeRequest = request };
        var audit = new RecordingAuditLogWriter();
        var service = CreateService(store, audit, new RecordingNotificationSender());

        await service.StartChangeReviewAsync(request.Id, "reviewer");

        Assert.Equal(MissionChangeRequestStatus.UnderReview, request.Status);
        Assert.Equal("reviewer", request.ReviewerUsername);
        Assert.Equal("StartChangeReview", audit.LastAction);
    }

    [Fact]
    public async Task رد_درخواست_علت_را_ثبت_و_به_درخواست_کننده_اعلان_می_کند()
    {
        var request = CreateChangeRequest();
        request.StartReview("reviewer", DateTimeOffset.UtcNow);
        var store = new FakeStore
        {
            ChangeRequest = request,
            Mission = CreateConfirmedMission()
        };
        var audit = new RecordingAuditLogWriter();
        var notifications = new RecordingNotificationSender();
        var service = CreateService(store, audit, notifications);

        await service.RejectChangeAsync(request.Id, "reviewer", "اطلاعات کافی نیست");

        Assert.Equal(MissionChangeRequestStatus.Rejected, request.Status);
        Assert.Equal("اطلاعات کافی نیست", request.DecisionReason);
        Assert.Equal("RejectChange", audit.LastAction);
        Assert.Equal("a.user", notifications.LastRecipient);
        Assert.Equal("MissionChangeRejected", notifications.LastEventKey);
    }

    private static MissionChangeRequestService CreateService(
        FakeStore store,
        RecordingAuditLogWriter audit,
        RecordingNotificationSender notifications) =>
        new(store, audit, notifications);

    private static Mission CreateConfirmedMission()
    {
        var mission = new Mission(
            "1405-001",
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
            PlannedStart,
            PlannedEnd);

        mission.Approve(Guid.NewGuid(), Guid.NewGuid(), "operator", PlannedStart.AddHours(-1));
        return mission;
    }

    private static MissionChangeRequest CreateChangeRequest()
    {
        var request = new MissionChangeRequest(
            Guid.NewGuid(),
            "a.user",
            "اصلاح مقصد",
            "{}");
        return request;
    }

    private static MissionChangeProposal CreateProposal() => new(
        "عنوان جدید",
        MissionType.Passenger,
        "هدف جدید",
        "مبدأ",
        "مقصد",
        [],
        PlannedStart,
        PlannedEnd,
        Guid.NewGuid(),
        Guid.NewGuid(),
        "کاربر آزمایشی",
        "09120000000",
        "واحد فناوری",
        "رایانه",
        null,
        null,
        null,
        null,
        null);

    private sealed class FakeStore : IMissionChangeRequestStore
    {
        private readonly FakeUnitOfWork _unitOfWork = new();

        public Mission? Mission
        {
            get => _unitOfWork.Mission;
            init => _unitOfWork.Mission = value;
        }

        public MissionChangeRequest? ChangeRequest
        {
            get => _unitOfWork.ChangeRequest;
            init => _unitOfWork.ChangeRequest = value;
        }

        public MissionChangeRequest? AddedRequest => _unitOfWork.AddedRequest;

        public Task<TResult> ExecuteSerializableAsync<TResult>(
            Func<IMissionChangeRequestUnitOfWork, CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken = default) =>
            operation(_unitOfWork, cancellationToken);

        public Task<IReadOnlyList<MissionChangeRequest>> GetChangeRequestsAsync(
            string? requesterUsername = null,
            MissionChangeRequestStatus? status = null,
            int take = 200,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MissionChangeRequest>>([]);
    }

    private sealed class FakeUnitOfWork : IMissionChangeRequestUnitOfWork
    {
        public Mission? Mission { get; set; }
        public MissionChangeRequest? ChangeRequest { get; set; }
        public MissionChangeRequest? AddedRequest { get; private set; }

        public Task<Mission?> GetMissionAsync(Guid missionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Mission?.Id == missionId ? Mission : null);

        public Task<MissionChangeRequest?> GetChangeRequestAsync(
            Guid changeRequestId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ChangeRequest?.Id == changeRequestId ? ChangeRequest : null);

        public Task<bool> HasOpenRequestAsync(Guid missionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(AddedRequest is not null
                || (ChangeRequest is not null
                    && ChangeRequest.MissionId == missionId
                    && ChangeRequest.Status is MissionChangeRequestStatus.Pending or MissionChangeRequestStatus.UnderReview));

        public void Add(MissionChangeRequest request) => AddedRequest = request;
    }

    private sealed class RecordingAuditLogWriter : IAuditLogWriter
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

    private sealed class RecordingNotificationSender : IMissionChangeNotificationSender
    {
        public string? LastRecipient { get; private set; }
        public string? LastEventKey { get; private set; }

        public Task CreateAsync(
            string recipientUsername,
            string title,
            string message,
            string eventKey,
            string? referenceType = null,
            string? referenceId = null,
            CancellationToken cancellationToken = default)
        {
            LastRecipient = recipientUsername;
            LastEventKey = eventKey;
            return Task.CompletedTask;
        }
    }
}
