using FleetManagement.Application.Configuration;
using FleetManagement.Application.Missions;
using FleetManagement.Domain.Missions;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// گردش اصلی درخواست، تأیید، اجرای مأموریت و کنترل تعارض منابع.
/// </summary>
public sealed class MissionManagementService(
    IMissionChangeManagementService changeManagementService,
    IMissionOdometerService odometerService,
    IMissionSplitService splitService,
    IMissionWithdrawalService withdrawalService,
    IMissionRequestService requestService,
    IMissionLifecycleProcessorService lifecycleProcessorService,
    IMissionQueryService queryService,
    IMissionExtensionService extensionService,
    IMissionRulesService rulesService)
{
    /// <summary>خواندن قواعد برنامه‌ریزی مأموریت.</summary>
    public Task<MissionRulesConfiguration> GetRulesAsync(CancellationToken cancellationToken = default)
        => rulesService.GetRulesAsync(cancellationToken);

    /// <summary>ذخیره قواعد فاصله بین مأموریت‌ها و افق رزرو.</summary>
    public Task SaveRulesAsync(MissionRulesConfiguration rules, string actor, CancellationToken cancellationToken = default)
        => rulesService.SaveRulesAsync(rules, actor, cancellationToken);

    /// <summary>ثبت درخواست تغییر مأموریت؛ اجرای سناریو در سرویس تخصصی تغییر انجام می‌شود.</summary>
    public Task<Guid> RequestChangeAsync(
        Guid missionId,
        string requesterUsername,
        string reason,
        MissionChangeProposal proposal,
        CancellationToken cancellationToken = default)
        => changeManagementService.RequestChangeAsync(missionId, requesterUsername, reason, proposal, cancellationToken);

    /// <summary>دریافت درخواست‌های تغییر از سرویس تخصصی تغییر.</summary>
    public Task<IReadOnlyList<MissionChangeRequest>> GetChangeRequestsAsync(
        string? requesterUsername = null,
        MissionChangeRequestStatus? status = null,
        int take = 200,
        CancellationToken cancellationToken = default)
        => changeManagementService.GetChangeRequestsAsync(requesterUsername, status, take, cancellationToken);

    /// <summary>شروع بررسی درخواست تغییر.</summary>
    public Task StartChangeReviewAsync(
        Guid changeRequestId,
        string reviewer,
        CancellationToken cancellationToken = default)
        => changeManagementService.StartChangeReviewAsync(changeRequestId, reviewer, cancellationToken);

    /// <summary>تأیید و اعمال اتمیک پیشنهاد تغییر.</summary>
    public Task ApproveChangeAsync(
        Guid changeRequestId,
        string reviewer,
        CancellationToken cancellationToken = default)
        => changeManagementService.ApproveChangeAsync(changeRequestId, reviewer, cancellationToken);

    /// <summary>رد درخواست تغییر با ثبت علت.</summary>
    public Task RejectChangeAsync(
        Guid changeRequestId,
        string reviewer,
        string reason,
        CancellationToken cancellationToken = default)
        => changeManagementService.RejectChangeAsync(changeRequestId, reviewer, reason, cancellationToken);

    /// <summary>
    /// ثبت درخواست مأموریت؛ درخواست‌های در انتظار منابع را رزرو نمی‌کنند.
    /// شماره سالانه در تراکنش Serializable تولید می‌شود تا تکراری نباشد.
    /// </summary>
    /// <summary>ثبت درخواست مأموریت.</summary>
    public Task<Guid> CreateRequestAsync(
        string title, string requesterUsername, string requesterName, string requesterPhoneSnapshot,
        string requesterOrganizationalUnit, string requestingGroup, MissionType type, string purpose,
        string origin, string destination, IEnumerable<string>? intermediateStops,
        DateTimeOffset plannedStartAtUtc, DateTimeOffset plannedEndAtUtc, string? description,
        int? passengerCount, string? missionOrderNumber, DateOnly? missionOrderDate, string actor,
        CancellationToken cancellationToken = default)
        => requestService.CreateRequestAsync(
            title, requesterUsername, requesterName, requesterPhoneSnapshot, requesterOrganizationalUnit,
            requestingGroup, type, purpose, origin, destination, intermediateStops, plannedStartAtUtc,
            plannedEndAtUtc, description, passengerCount, missionOrderNumber, missionOrderDate, actor,
            cancellationToken);

    /// <summary>ویرایش درخواست تأییدنشده.</summary>
    public Task UpdatePendingRequestAsync(
        Guid missionId, string requesterUsername, string title, string requesterPhoneSnapshot,
        string requestingGroup, MissionType type, string purpose, string origin, string destination,
        IEnumerable<string>? intermediateStops, DateTimeOffset plannedStartAtUtc,
        DateTimeOffset plannedEndAtUtc, string? description, int? passengerCount,
        string? missionOrderNumber, DateOnly? missionOrderDate,
        CancellationToken cancellationToken = default)
        => requestService.UpdatePendingRequestAsync(
            missionId, requesterUsername, title, requesterPhoneSnapshot, requestingGroup, type, purpose,
            origin, destination, intermediateStops, plannedStartAtUtc, plannedEndAtUtc, description,
            passengerCount, missionOrderNumber, missionOrderDate, cancellationToken);

    /// <summary>شروع بررسی درخواست مأموریت.</summary>
    public Task StartReviewAsync(Guid missionId, string actor, CancellationToken cancellationToken = default)
        => requestService.StartReviewAsync(missionId, actor, cancellationToken);

    /// <summary>تأیید و تخصیص منابع به مأموریت.</summary>
    public Task ApproveAsync(Guid missionId, Guid driverId, Guid vehicleId, string actor, CancellationToken cancellationToken = default)
        => requestService.ApproveAsync(missionId, driverId, vehicleId, actor, cancellationToken);

    /// <summary>رد درخواست مأموریت.</summary>
    public Task RejectAsync(Guid missionId, string reason, string actor, CancellationToken cancellationToken = default)
        => requestService.RejectAsync(missionId, reason, actor, cancellationToken);

    public Task CancelAsync(Guid missionId, string reason, string actor, CancellationToken cancellationToken = default)
        => withdrawalService.CancelAsync(missionId, reason, actor, cancellationToken);

    /// <summary>ثبت خاتمه زودهنگام مأموریت در حال اجرا.</summary>
    public Task EndEarlyAsync(Guid missionId, DateTimeOffset endedAtUtc, string reason, string actor, CancellationToken cancellationToken = default)
        => withdrawalService.EndEarlyAsync(missionId, endedAtUtc, reason, actor, cancellationToken);

    /// <summary>لغو مستقیم درخواست تأییدنشده توسط درخواست‌کننده.</summary>
    public Task CancelPendingRequestAsync(Guid missionId, string requesterUsername, string reason, CancellationToken cancellationToken = default)
        => withdrawalService.CancelPendingRequestAsync(missionId, requesterUsername, reason, cancellationToken);

    /// <summary>ثبت درخواست انصراف از مأموریت قطعی.</summary>
    public Task RequestWithdrawalAsync(Guid missionId, string requesterUsername, string reason, CancellationToken cancellationToken = default)
        => withdrawalService.RequestWithdrawalAsync(missionId, requesterUsername, reason, cancellationToken);

    /// <summary>تأیید درخواست انصراف.</summary>
    public Task ApproveWithdrawalAsync(Guid missionId, string reviewer, CancellationToken cancellationToken = default)
        => withdrawalService.ApproveWithdrawalAsync(missionId, reviewer, cancellationToken);

    /// <summary>رد درخواست انصراف.</summary>
    public Task RejectWithdrawalAsync(Guid missionId, string reason, string reviewer, CancellationToken cancellationToken = default)
        => withdrawalService.RejectWithdrawalAsync(missionId, reason, reviewer, cancellationToken);

    /// <summary>تمدید مأموریت.</summary>
    public Task ExtendAsync(
        Guid missionId,
        DateTimeOffset newEndAtUtc,
        string reason,
        string actor,
        CancellationToken cancellationToken = default)
        => extensionService.ExtendAsync(missionId, newEndAtUtc, reason, actor, cancellationToken);

    public Task SetOdometerAsync(
        Guid missionId,
        long? startKm,
        long? endKm,
        string actor,
        CancellationToken cancellationToken = default)
        => odometerService.SetOdometerAsync(missionId, startKm, endKm, actor, cancellationToken);

    /// <summary>
    /// تقسیم مأموریت در حال اجرا به دو رکورد مستقل با شماره‌های جداگانه.
    /// </summary>
    /// <summary>تقسیم مأموریت از طریق سرویس تخصصی تقسیم.</summary>
    public Task<Guid> SplitAsync(
        Guid missionId,
        Guid newDriverId,
        Guid newVehicleId,
        DateTimeOffset splitAtUtc,
        long firstSegmentOdometerEndKm,
        long? secondVehicleOdometerStartKm,
        MissionSplitReason splitReason,
        string? explanation,
        string actor,
        CancellationToken cancellationToken = default)
        => splitService.SplitAsync(
            missionId,
            newDriverId,
            newVehicleId,
            splitAtUtc,
            firstSegmentOdometerEndKm,
            secondVehicleOdometerStartKm,
            splitReason,
            explanation,
            actor,
            cancellationToken);

    /// <summary>
    /// شروع خودکار مأموریت‌های تأییدشده و پایان خودکار مأموریت‌های در حال اجرا.
    /// </summary>
    /// <summary>پردازش مأموریت‌های سررسیدشده از طریق سرویس تخصصی چرخه عمر.</summary>
    public Task<int> ProcessDueMissionsAsync(CancellationToken cancellationToken = default)
        => lifecycleProcessorService.ProcessDueMissionsAsync(cancellationToken);

    /// <summary>دریافت بازه زمانی مأموریت‌ها.</summary>
    public Task<IReadOnlyList<Mission>> GetTimelineAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        int take = 1000,
        CancellationToken cancellationToken = default)
        => queryService.GetTimelineAsync(fromUtc, toUtc, take, cancellationToken);

    /// <summary>دریافت مأموریت بر اساس شناسه.</summary>
    public Task<Mission> GetByIdAsync(Guid missionId, CancellationToken cancellationToken = default)
        => queryService.GetByIdAsync(missionId, cancellationToken);

    /// <summary>جست‌وجوی مأموریت‌ها بر اساس فیلترهای درخواست‌کننده، وضعیت و تاریخ.</summary>
    public Task<IReadOnlyList<Mission>> SearchAsync(
        string? requesterUsername = null,
        MissionStatus? status = null,
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        int take = 200,
        CancellationToken cancellationToken = default)
        => queryService.SearchAsync(requesterUsername, status, fromUtc, toUtc, take, cancellationToken);

}
