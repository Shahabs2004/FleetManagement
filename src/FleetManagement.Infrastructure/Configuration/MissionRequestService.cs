using System.Data;
using System.Text.Json;
using FleetManagement.Application.Auditing;
using FleetManagement.Application.Missions;
using FleetManagement.Domain.Locations;
using FleetManagement.Domain.Missions;
using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using ApplicationMissionRequestService = FleetManagement.Application.Missions.MissionRequestService;

namespace FleetManagement.Infrastructure.Configuration;

/// <summary>
/// آداپتور گردش درخواست مأموریت؛ ایجاد و بررسی در Application اجرا می‌شود.
/// ویرایش درخواست Pending تا انتقال مستقل آن به Application در این لایه باقی می‌ماند.
/// </summary>
public sealed class MissionRequestService(
    IDbContextFactory<FleetDbContext> dbContextFactory,
    IMissionRulesService rulesService,
    IAuditLogWriter auditLog,
    ApplicationMissionRequestService requestWorkflow,
    MissionReviewService reviewWorkflow) : IMissionRequestService
{
    public Task<Guid> CreateRequestAsync(
        string title,
        string requesterUsername,
        string requesterName,
        string requesterPhoneSnapshot,
        string requesterOrganizationalUnit,
        string requestingGroup,
        MissionType type,
        string purpose,
        string origin,
        string destination,
        IEnumerable<string>? intermediateStops,
        DateTimeOffset plannedStartAtUtc,
        DateTimeOffset plannedEndAtUtc,
        string? description,
        int? passengerCount,
        string? missionOrderNumber,
        DateOnly? missionOrderDate,
        string actor,
        CancellationToken cancellationToken = default)
        => requestWorkflow.CreateRequestAsync(
            title,
            requesterUsername,
            requesterName,
            requesterPhoneSnapshot,
            requesterOrganizationalUnit,
            requestingGroup,
            type,
            purpose,
            origin,
            destination,
            intermediateStops,
            plannedStartAtUtc,
            plannedEndAtUtc,
            description,
            passengerCount,
            missionOrderNumber,
            missionOrderDate,
            actor,
            cancellationToken);

    public async Task UpdatePendingRequestAsync(
        Guid missionId,
        string requesterUsername,
        string title,
        string requesterPhoneSnapshot,
        string requestingGroup,
        MissionType type,
        string purpose,
        string origin,
        string destination,
        IEnumerable<string>? intermediateStops,
        DateTimeOffset plannedStartAtUtc,
        DateTimeOffset plannedEndAtUtc,
        string? description,
        int? passengerCount,
        string? missionOrderNumber,
        DateOnly? missionOrderDate,
        CancellationToken cancellationToken = default)
    {
        var rules = await rulesService.GetRulesAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        if (plannedStartAtUtc < now)
        {
            throw new InvalidOperationException("زمان شروع مأموریت نمی‌تواند در گذشته باشد.");
        }

        if (plannedStartAtUtc > now.AddDays(rules.BookingHorizonDays)
            || plannedEndAtUtc > now.AddDays(rules.BookingHorizonDays))
        {
            throw new InvalidOperationException("بازه انتخاب‌شده از افق مجاز رزرو مأموریت دورتر است.");
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var mission = await GetMissionAsync(db, missionId, cancellationToken);
        if (!string.Equals(
            mission.RequesterUsername,
            requesterUsername,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("فقط درخواست‌کننده می‌تواند درخواست خود را ویرایش کند.");
        }

        var stops = intermediateStops?.Select(value => value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray() ?? [];

        var requestedLocations = stops
            .Append(origin.Trim())
            .Append(destination.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var approvedLocations = await db.Locations
            .Where(location => location.Status == LocationStatus.Approved
                && requestedLocations.Contains(location.Name))
            .Select(location => location.Name)
            .ToListAsync(cancellationToken);

        if (approvedLocations.Count != requestedLocations.Length)
        {
            throw new InvalidOperationException("مبدأ، مقصد و توقف‌ها باید از فهرست مکان‌های تأییدشده انتخاب شوند.");
        }

        var before = JsonSerializer.Serialize(new
        {
            mission.Title,
            mission.RequesterPhoneSnapshot,
            mission.RequestingGroup,
            mission.Type,
            mission.Purpose,
            mission.Origin,
            mission.Destination,
            mission.RouteJson,
            mission.PlannedStartAtUtc,
            mission.PlannedEndAtUtc,
            mission.Description,
            mission.PassengerCount,
            mission.MissionOrderNumber,
            mission.MissionOrderDate
        });

        mission.EditPendingRequest(
            title,
            requesterPhoneSnapshot,
            requestingGroup,
            type,
            purpose,
            origin,
            destination,
            stops,
            plannedStartAtUtc,
            plannedEndAtUtc,
            description,
            passengerCount,
            missionOrderNumber,
            missionOrderDate);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await auditLog.RecordAsync(
            requesterUsername,
            "EditPendingRequest",
            "Mission",
            mission.Id.ToString(),
            "Success",
            beforeJson: before,
            afterJson: JsonSerializer.Serialize(new
            {
                mission.Title,
                mission.RequesterPhoneSnapshot,
                mission.RequestingGroup,
                mission.Type,
                mission.Purpose,
                mission.Origin,
                mission.Destination,
                mission.RouteJson,
                mission.PlannedStartAtUtc,
                mission.PlannedEndAtUtc,
                mission.Description,
                mission.PassengerCount,
                mission.MissionOrderNumber,
                mission.MissionOrderDate
            }),
            cancellationToken: cancellationToken);
    }

    public Task StartReviewAsync(
        Guid missionId,
        string actor,
        CancellationToken cancellationToken = default)
        => reviewWorkflow.StartReviewAsync(missionId, actor, cancellationToken);

    public Task ApproveAsync(
        Guid missionId,
        Guid driverId,
        Guid vehicleId,
        string actor,
        CancellationToken cancellationToken = default)
        => reviewWorkflow.ApproveAsync(missionId, driverId, vehicleId, actor, cancellationToken);

    public Task RejectAsync(
        Guid missionId,
        string reason,
        string actor,
        CancellationToken cancellationToken = default)
        => reviewWorkflow.RejectAsync(missionId, reason, actor, cancellationToken);

    private static async Task<Mission> GetMissionAsync(
        FleetDbContext db,
        Guid missionId,
        CancellationToken cancellationToken)
    {
        return await db.Missions.SingleOrDefaultAsync(
            mission => mission.Id == missionId,
            cancellationToken)
            ?? throw new KeyNotFoundException("مأموریت موردنظر پیدا نشد.");
    }
}
