using System.Text.Json;
using FleetManagement.Application.Auditing;
using FleetManagement.Application.Configuration;
using FleetManagement.Application.Notifications;
using FleetManagement.Domain.Drivers;
using FleetManagement.Domain.Missions;
using FleetManagement.Domain.Vehicles;

namespace FleetManagement.Application.Missions;

/// <summary>
/// هماهنگ‌کننده تقسیم مأموریت و ایجاد بخش دوم با منابع و شماره مستقل.
/// </summary>
public sealed class MissionSplitService(
    IMissionSplitStore store,
    IMissionRulesService rulesService,
    IAuditLogWriter auditLogWriter,
    IMissionNotificationSender notificationSender) : IMissionSplitService
{
    /// <summary>تقسیم مأموریت جاری؛ قواعد دامنه در Application و تراکنش SQL در Infrastructure اجرا می‌شود.</summary>
    public async Task<Guid> SplitAsync(
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
    {
        var rules = await rulesService.GetRulesAsync(cancellationToken);
        var result = await store.ExecuteSerializableAsync(async (unitOfWork, token) =>
        {
            var first = await unitOfWork.GetMissionAsync(missionId, token)
                ?? throw new KeyNotFoundException("مأموریت موردنظر پیدا نشد.");

            if (first.DriverId is null || first.VehicleId is null)
            {
                throw new InvalidOperationException("مأموریت جاری راننده و خودرو ندارد.");
            }

            var driverChanged = first.DriverId.Value != newDriverId;
            var vehicleChanged = first.VehicleId.Value != newVehicleId;
            if (!driverChanged && !vehicleChanged)
            {
                throw new InvalidOperationException("برای تقسیم باید راننده یا خودرو تغییر کند.");
            }

            var expectedReason = (driverChanged, vehicleChanged) switch
            {
                (true, false) => MissionSplitReason.DriverChange,
                (false, true) => MissionSplitReason.VehicleBreakdown,
                (true, true) => MissionSplitReason.DriverAndVehicleChange,
                _ => splitReason
            };

            if (splitReason != MissionSplitReason.Other && splitReason != expectedReason)
            {
                throw new InvalidOperationException("دلیل انتخاب‌شده با نوع تغییر راننده یا خودرو سازگار نیست.");
            }

            if (splitReason == MissionSplitReason.Other && string.IsNullOrWhiteSpace(explanation))
            {
                throw new InvalidOperationException("برای دلیل «سایر موارد» توضیح الزامی است.");
            }

            var reasonText = splitReason == MissionSplitReason.Other
                ? $"سایر موارد: {explanation!.Trim()}"
                : splitReason switch
                {
                    MissionSplitReason.VehicleBreakdown => "خرابی خودرو",
                    MissionSplitReason.DriverChange => "تعویض راننده",
                    MissionSplitReason.DriverAndVehicleChange => "تعویض هم‌زمان راننده و خودرو",
                    _ => throw new ArgumentOutOfRangeException(nameof(splitReason))
                };

            var newDriver = await unitOfWork.GetDriverAsync(newDriverId, token)
                ?? throw new KeyNotFoundException("راننده جدید پیدا نشد.");
            var newVehicle = await unitOfWork.GetVehicleAsync(newVehicleId, token)
                ?? throw new KeyNotFoundException("خودروی جدید پیدا نشد.");

            if (!newDriver.CanBeAssignedToMission())
            {
                throw new InvalidOperationException("راننده جدید فعال و قابل تخصیص نیست.");
            }

            if (!newVehicle.CanBeAssignedToMission(hasActiveMission: false))
            {
                throw new InvalidOperationException("خودروی جدید آماده تخصیص نیست.");
            }

            var originalEnd = first.PlannedEndAtUtc;
            first.CloseFirstSegmentForSplit(
                splitAtUtc,
                firstSegmentOdometerEndKm,
                reasonText,
                actor);

            // پایان بخش اول در همان تراکنش ذخیره می‌شود تا در بررسی منابع، بخش اول فعال محسوب نشود.
            await unitOfWork.SaveChangesAsync(token);

            var missionNumber = await unitOfWork.GenerateMissionNumberAsync(
                DateTimeOffset.UtcNow,
                token);
            var second = new Mission(
                missionNumber,
                first.Title,
                first.RequesterUsername,
                first.RequesterName,
                first.RequesterPhoneSnapshot,
                first.RequesterOrganizationalUnit,
                first.RequestingGroup,
                first.Type,
                first.Purpose,
                first.Origin,
                first.Destination,
                first.GetIntermediateStops(),
                splitAtUtc,
                originalEnd,
                first.Description,
                first.PassengerCount,
                first.MissionOrderNumber,
                first.MissionOrderDate);

            if (newVehicleId == first.VehicleId)
            {
                second.SetOdometerStart(firstSegmentOdometerEndKm);
            }
            else
            {
                if (!secondVehicleOdometerStartKm.HasValue)
                {
                    throw new InvalidOperationException("برای خودروی جدید باید کیلومترشمار شروع ثبت شود.");
                }

                var lastRecordedOdometer = await unitOfWork.GetLastOdometerEndAsync(
                    newVehicleId,
                    token);
                if (lastRecordedOdometer.HasValue
                    && secondVehicleOdometerStartKm.Value < lastRecordedOdometer.Value)
                {
                    throw new InvalidOperationException("کیلومترشمار شروع خودروی جدید از آخرین مقدار ثبت‌شده کمتر است.");
                }

                second.SetOdometerStart(secondVehicleOdometerStartKm.Value);
            }

            var candidates = await unitOfWork.GetResourceConflictCandidatesAsync(
                second.Id,
                newDriverId,
                newVehicleId,
                token);
            var minimumGap = TimeSpan.FromHours(rules.MinimumGapHours);
            var hasConflict = candidates.Any(existing =>
                existing.Status is (MissionStatus.Confirmed
                    or MissionStatus.Running
                    or MissionStatus.WithdrawalPending)
                && splitAtUtc < existing.PlannedEndAtUtc.Add(minimumGap)
                && originalEnd.Add(minimumGap) > existing.PlannedStartAtUtc);

            if (hasConflict)
            {
                throw new InvalidOperationException("منابع جدید در بازه بخش دوم مأموریت تعارض زمانی دارند.");
            }

            var now = DateTimeOffset.UtcNow;
            second.Approve(newDriverId, newVehicleId, actor, now);
            second.StartAutomatically(splitAtUtc);
            second.MarkAsSecondSplitSegment(first.Id, splitAtUtc, reasonText, actor);
            first.SetRelatedMission(second.Id);
            unitOfWork.Add(second);

            return new SplitResult(first, second, reasonText);
        }, cancellationToken);

        await auditLogWriter.RecordAsync(
            actor,
            "SplitFirstSegment",
            "Mission",
            result.First.Id.ToString(),
            "Success",
            result.ReasonText,
            afterJson: JsonSerializer.Serialize(new
            {
                result.First.MissionNumber,
                RelatedMissionId = result.Second.Id,
                result.First.SplitAtUtc,
                result.First.OdometerStartKm,
                result.First.OdometerEndKm
            }),
            cancellationToken: cancellationToken);

        await auditLogWriter.RecordAsync(
            actor,
            "SplitSecondSegment",
            "Mission",
            result.Second.Id.ToString(),
            "Success",
            result.ReasonText,
            afterJson: JsonSerializer.Serialize(new
            {
                result.Second.MissionNumber,
                RelatedMissionId = result.First.Id,
                result.Second.DriverId,
                result.Second.VehicleId,
                result.Second.PlannedStartAtUtc,
                result.Second.PlannedEndAtUtc,
                result.Second.OdometerStartKm
            }),
            cancellationToken: cancellationToken);

        await notificationSender.CreateForRolesAsync(
            ["FleetManager", "Operator"],
            "مأموریت تقسیم شد",
            $"مأموریت {result.First.MissionNumber} به دو مأموریت مستقل تقسیم شد. شماره بخش دوم: {result.Second.MissionNumber}.",
            "MissionSplit",
            "Mission",
            result.First.Id.ToString(),
            cancellationToken);

        return result.Second.Id;
    }

    private sealed record SplitResult(Mission First, Mission Second, string ReasonText);
}
