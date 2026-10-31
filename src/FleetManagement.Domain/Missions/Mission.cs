using System.Text.Json;
using FleetManagement.Domain.Common;

namespace FleetManagement.Domain.Missions;

/// <summary>
/// درخواست مأموریت و سابقه مستقل اجرای آن.
/// قوانین تعارض منابع در سرویس کاربردی بررسی می‌شوند.
/// </summary>
public sealed class Mission : Entity
{
    private Mission()
    {
        MissionNumber = string.Empty;
        Title = string.Empty;
        RequesterUsername = string.Empty;
        RequesterName = string.Empty;
        RequesterPhoneSnapshot = string.Empty;
        RequesterOrganizationalUnit = string.Empty;
        RequestingGroup = string.Empty;
        Purpose = string.Empty;
        Origin = string.Empty;
        Destination = string.Empty;
        RouteJson = "[]";
    }

    /// <summary>شماره یکتای مأموریت در سال شمسی.</summary>
    public string MissionNumber { get; private set; }

    /// <summary>عنوان مأموریت.</summary>
    public string Title { get; private set; }

    /// <summary>نام کاربری درخواست‌کننده.</summary>
    public string RequesterUsername { get; private set; }

    /// <summary>نام نمایشی درخواست‌کننده در زمان ثبت.</summary>
    public string RequesterName { get; private set; }

    /// <summary>شماره همراه درخواست‌کننده به‌صورت تصویر ثابت زمان ثبت.</summary>
    public string RequesterPhoneSnapshot { get; private set; }

    /// <summary>واحد سازمانی درخواست‌کننده در زمان ثبت.</summary>
    public string RequesterOrganizationalUnit { get; private set; }

    /// <summary>گروه سازمانی انتخاب‌شده از گروه‌های مجاز درخواست‌کننده.</summary>
    public string RequestingGroup { get; private set; }

    /// <summary>نوع مأموریت بار یا مسافر.</summary>
    public MissionType Type { get; private set; }

    /// <summary>هدف یا علت مأموریت.</summary>
    public string Purpose { get; private set; }

    /// <summary>مبدأ مأموریت.</summary>
    public string Origin { get; private set; }

    /// <summary>مقصد نهایی مأموریت.</summary>
    public string Destination { get; private set; }

    /// <summary>فهرست مرتب مقصدهای میانی به‌صورت JSON.</summary>
    public string RouteJson { get; private set; }

    /// <summary>زمان برنامه‌ریزی‌شده شروع با دقت ساعت.</summary>
    public DateTimeOffset PlannedStartAtUtc { get; private set; }

    /// <summary>زمان برنامه‌ریزی‌شده پایان با دقت ساعت.</summary>
    public DateTimeOffset PlannedEndAtUtc { get; private set; }

    /// <summary>شناسه راننده پس از تأیید.</summary>
    public Guid? DriverId { get; private set; }

    /// <summary>شناسه خودرو پس از تأیید.</summary>
    public Guid? VehicleId { get; private set; }

    /// <summary>شرح تکمیلی مأموریت.</summary>
    public string? Description { get; private set; }

    /// <summary>تعداد همراهان یا مسافران در صورت کاربرد.</summary>
    public int? PassengerCount { get; private set; }

    /// <summary>شماره نامه یا دستور مأموریت اختیاری.</summary>
    public string? MissionOrderNumber { get; private set; }

    /// <summary>تاریخ نامه یا دستور مأموریت اختیاری.</summary>
    public DateOnly? MissionOrderDate { get; private set; }

    /// <summary>وضعیت جاری چرخه عمر.</summary>
    public MissionStatus Status { get; private set; }

    /// <summary>زمان ایجاد درخواست.</summary>
    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>زمان شروع بررسی توسط نخستین بازبین.</summary>
    public DateTimeOffset? ReviewStartedAtUtc { get; private set; }

    /// <summary>نام کاربری آخرین تأییدکننده.</summary>
    public string? ApprovedBy { get; private set; }

    /// <summary>زمان تأیید مأموریت.</summary>
    public DateTimeOffset? ApprovedAtUtc { get; private set; }

    /// <summary>زمان واقعی شروع؛ در شروع خودکار برابر زمان برنامه‌ریزی‌شده است.</summary>
    public DateTimeOffset? ActualStartAtUtc { get; private set; }

    /// <summary>زمان واقعی پایان یا خاتمه زودهنگام.</summary>
    public DateTimeOffset? ActualEndAtUtc { get; private set; }

    /// <summary>کیلومترشمار شروع.</summary>
    public long? OdometerStartKm { get; private set; }

    /// <summary>کیلومترشمار پایان.</summary>
    public long? OdometerEndKm { get; private set; }

    /// <summary>علت رد، لغو یا تغییر زمان.</summary>
    public string? DecisionReason { get; private set; }

    /// <summary>وضعیت پیشین هنگام درخواست انصراف.</summary>
    public MissionStatus? StatusBeforeWithdrawal { get; private set; }

    /// <summary>شناسه مأموریت مرتبط در صورت تقسیم.</summary>
    public Guid? RelatedMissionId { get; private set; }

    /// <summary>دلیل تقسیم مأموریت.</summary>
    public string? SplitReason { get; private set; }

    /// <summary>زمان تقسیم مأموریت.</summary>
    public DateTimeOffset? SplitAtUtc { get; private set; }

    /// <summary>نام کاربری انجام‌دهنده تقسیم.</summary>
    public string? SplitBy { get; private set; }

    /// <summary>
    /// ایجاد درخواست جدید؛ منابع در این مرحله رزرو نمی‌شوند.
    /// </summary>
    public Mission(
        string missionNumber,
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
        string? description = null,
        int? passengerCount = null,
        string? missionOrderNumber = null,
        DateOnly? missionOrderDate = null)
    {
        MissionNumber = Required(missionNumber, 20, nameof(missionNumber));
        Title = Required(title, 200, nameof(title));
        RequesterUsername = Required(requesterUsername, 64, nameof(requesterUsername));
        RequesterName = Required(requesterName, 200, nameof(requesterName));
        RequesterPhoneSnapshot = Required(requesterPhoneSnapshot, 20, nameof(requesterPhoneSnapshot));
        RequesterOrganizationalUnit = Required(requesterOrganizationalUnit, 200, nameof(requesterOrganizationalUnit));
        RequestingGroup = Required(requestingGroup, 200, nameof(requestingGroup));
        Purpose = Required(purpose, 500, nameof(purpose));
        Origin = Required(origin, 200, nameof(origin));
        Destination = Required(destination, 200, nameof(destination));

        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        ValidateHour(plannedStartAtUtc, nameof(plannedStartAtUtc));
        ValidateHour(plannedEndAtUtc, nameof(plannedEndAtUtc));
        if (plannedEndAtUtc <= plannedStartAtUtc)
        {
            throw new ArgumentException("زمان پایان باید بعد از زمان شروع باشد.");
        }

        if (passengerCount is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(passengerCount));
        }

        Type = type;
        PlannedStartAtUtc = plannedStartAtUtc;
        PlannedEndAtUtc = plannedEndAtUtc;
        Description = Optional(description, 2000);
        PassengerCount = passengerCount;
        MissionOrderNumber = Optional(missionOrderNumber, 50);
        MissionOrderDate = missionOrderDate;
        RouteJson = JsonSerializer.Serialize(NormalizeStops(intermediateStops));
        Status = MissionStatus.Pending;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>آغاز بررسی درخواست و قفل‌کردن امکان ویرایش توسط درخواست‌کننده.</summary>
    public void StartReview(DateTimeOffset nowUtc)
    {
        EnsureStatus(MissionStatus.Pending);
        ReviewStartedAtUtc ??= nowUtc;
        Status = MissionStatus.UnderReview;
    }

    /// <summary>تأیید درخواست پس از بررسی تخصیص منابع و تعارض زمانی.</summary>
    public void Approve(Guid driverId, Guid vehicleId, string approver, DateTimeOffset nowUtc)
    {
        if (Status is not (MissionStatus.Pending or MissionStatus.UnderReview))
        {
            throw new InvalidOperationException("وضعیت فعلی برای تأیید مأموریت مجاز نیست.");
        }

        if (driverId == Guid.Empty || vehicleId == Guid.Empty)
        {
            throw new ArgumentException("راننده و خودرو باید مشخص باشند.");
        }

        DriverId = driverId;
        VehicleId = vehicleId;
        ApprovedBy = Required(approver, 64, nameof(approver));
        ApprovedAtUtc = nowUtc;
        Status = MissionStatus.Confirmed;
    }

    /// <summary>رد درخواست با ثبت علت اجباری.</summary>
    public void Reject(string reason)
    {
        if (Status is not (MissionStatus.Pending or MissionStatus.UnderReview))
        {
            throw new InvalidOperationException("فقط درخواست در انتظار بررسی قابل رد است.");
        }

        DecisionReason = Required(reason, 1000, nameof(reason));
        Status = MissionStatus.Rejected;
    }

    /// <summary>شروع خودکار مأموریت در زمان برنامه‌ریزی‌شده.</summary>
    public void StartAutomatically(DateTimeOffset nowUtc)
    {
        EnsureStatus(MissionStatus.Confirmed);
        if (nowUtc < PlannedStartAtUtc)
        {
            throw new InvalidOperationException("زمان شروع برنامه‌ریزی‌شده هنوز فرا نرسیده است.");
        }

        if (DriverId is null || VehicleId is null)
        {
            throw new InvalidOperationException("برای شروع مأموریت راننده و خودرو باید تعیین شده باشند.");
        }

        ActualStartAtUtc = PlannedStartAtUtc;
        Status = MissionStatus.Running;
    }

    /// <summary>تغییر زمان پایان با ثبت علت؛ بررسی تعارض در سرویس کاربردی انجام می‌شود.</summary>
    public void Extend(DateTimeOffset newPlannedEndAtUtc, string reason)
    {
        EnsureStatus(MissionStatus.Running);
        ValidateHour(newPlannedEndAtUtc, nameof(newPlannedEndAtUtc));
        if (newPlannedEndAtUtc <= PlannedEndAtUtc)
        {
            throw new ArgumentException("زمان جدید باید بعد از زمان پایان فعلی باشد.");
        }

        DecisionReason = Required(reason, 1000, nameof(reason));
        PlannedEndAtUtc = newPlannedEndAtUtc;
    }

    /// <summary>خاتمه زودهنگام مأموریت و آزادسازی منابع در لایه کاربرد.</summary>
    public void EndEarly(DateTimeOffset endedAtUtc, string reason)
    {
        EnsureStatus(MissionStatus.Running);
        ValidateHour(endedAtUtc, nameof(endedAtUtc));
        if (ActualStartAtUtc.HasValue && endedAtUtc <= ActualStartAtUtc.Value)
        {
            throw new ArgumentException("زمان خاتمه باید بعد از شروع مأموریت باشد.");
        }

        ActualEndAtUtc = endedAtUtc;
        DecisionReason = Required(reason, 1000, nameof(reason));
        Status = MissionStatus.Completed;
    }

    /// <summary>ثبت پایان عادی مأموریت در زمان برنامه‌ریزی‌شده.</summary>
    public void CompleteAutomatically()
    {
        EnsureStatus(MissionStatus.Running);
        ActualEndAtUtc = PlannedEndAtUtc;
        Status = MissionStatus.Completed;
    }

    /// <summary>ثبت کیلومترشمار شروع با کنترل پیوستگی در سرویس کاربردی.</summary>
    public void SetOdometerStart(long value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        if (OdometerEndKm.HasValue && value > OdometerEndKm.Value)
        {
            throw new InvalidOperationException("کیلومترشمار شروع نمی‌تواند از کیلومترشمار پایان بیشتر باشد.");
        }

        OdometerStartKm = value;
    }

    /// <summary>ثبت کیلومترشمار پایان با کنترل عدم کاهش.</summary>
    public void SetOdometerEnd(long value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        if (!OdometerStartKm.HasValue || value < OdometerStartKm.Value)
        {
            throw new InvalidOperationException("کیلومترشمار پایان باید پس از ثبت شروع و مساوی یا بیشتر از آن باشد.");
        }

        OdometerEndKm = value;
    }

    /// <summary>مسافت طی‌شده در صورت ثبت هر دو کیلومترشمار.</summary>
    public long? DistanceKm => OdometerStartKm.HasValue && OdometerEndKm.HasValue
        ? OdometerEndKm.Value - OdometerStartKm.Value
        : null;

    /// <summary>ثبت درخواست انصراف توسط درخواست‌کننده.</summary>
    public void RequestWithdrawal(string reason)
    {
        if (Status is MissionStatus.Completed or MissionStatus.Cancelled or MissionStatus.Rejected
            or MissionStatus.WithdrawalPending)
        {
            throw new InvalidOperationException("وضعیت فعلی امکان درخواست انصراف ندارد.");
        }

        StatusBeforeWithdrawal = Status;
        DecisionReason = Required(reason, 1000, nameof(reason));
        Status = MissionStatus.WithdrawalPending;
    }

    /// <summary>تأیید انصراف و لغو مأموریت.</summary>
    public void ApproveWithdrawal(DateTimeOffset nowUtc)
    {
        EnsureStatus(MissionStatus.WithdrawalPending);
        DecisionReason = Required(DecisionReason ?? "درخواست انصراف تأیید شد", 1000, nameof(DecisionReason));
        Status = MissionStatus.Cancelled;
        ActualEndAtUtc ??= ActualStartAtUtc.HasValue ? nowUtc : null;
        StatusBeforeWithdrawal = null;
    }

    /// <summary>رد انصراف و بازگرداندن وضعیت قبلی مأموریت.</summary>
    public void RejectWithdrawal(string reason)
    {
        EnsureStatus(MissionStatus.WithdrawalPending);
        DecisionReason = Required(reason, 1000, nameof(reason));
        Status = StatusBeforeWithdrawal ?? MissionStatus.Confirmed;
        StatusBeforeWithdrawal = null;
    }

    /// <summary>
    /// بستن خودکار درخواست انصرافی که تا پایان برنامه‌ریزی‌شده مأموریت تعیین تکلیف نشده است.
    /// </summary>
    public void AutoCloseExpiredWithdrawal(DateTimeOffset nowUtc)
    {
        EnsureStatus(MissionStatus.WithdrawalPending);
        if (nowUtc < PlannedEndAtUtc)
        {
            throw new InvalidOperationException("زمان پایان برنامه‌ریزی‌شده هنوز فرا نرسیده است.");
        }

        if (StatusBeforeWithdrawal == MissionStatus.Running)
        {
            ActualEndAtUtc = nowUtc;
        }

        DecisionReason = "درخواست انصراف تا پایان زمان برنامه‌ریزی‌شده تعیین تکلیف نشد.";
        Status = MissionStatus.Cancelled;
        StatusBeforeWithdrawal = null;
    }

    /// <summary>لغو مأموریت با علت اجباری.</summary>
    public void Cancel(string reason, DateTimeOffset nowUtc)
    {
        if (Status is MissionStatus.Completed or MissionStatus.Cancelled or MissionStatus.Rejected)
        {
            throw new InvalidOperationException("مأموریت پایان‌یافته یا لغوشده قابل لغو مجدد نیست.");
        }

        DecisionReason = Required(reason, 1000, nameof(reason));
        if (Status == MissionStatus.Running)
        {
            ActualEndAtUtc = nowUtc;
        }

        Status = MissionStatus.Cancelled;
    }

    /// <summary>اصلاح اطلاعات درخواست تا پیش از شروع بررسی.</summary>
    public void EditPendingRequest(
        string title,
        string purpose,
        string origin,
        string destination,
        IEnumerable<string>? intermediateStops,
        DateTimeOffset plannedStartAtUtc,
        DateTimeOffset plannedEndAtUtc,
        string? description,
        int? passengerCount)
    {
        EnsureStatus(MissionStatus.Pending);
        Title = Required(title, 200, nameof(title));
        Purpose = Required(purpose, 500, nameof(purpose));
        Origin = Required(origin, 200, nameof(origin));
        Destination = Required(destination, 200, nameof(destination));
        ValidateHour(plannedStartAtUtc, nameof(plannedStartAtUtc));
        ValidateHour(plannedEndAtUtc, nameof(plannedEndAtUtc));
        if (plannedEndAtUtc <= plannedStartAtUtc)
        {
            throw new ArgumentException("زمان پایان باید بعد از زمان شروع باشد.");
        }

        if (passengerCount is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(passengerCount));
        }

        PlannedStartAtUtc = plannedStartAtUtc;
        PlannedEndAtUtc = plannedEndAtUtc;
        RouteJson = JsonSerializer.Serialize(NormalizeStops(intermediateStops));
        Description = Optional(description, 2000);
        PassengerCount = passengerCount;
    }

    /// <summary>
    /// پایان بخش اول مأموریت در زمان تقسیم و ثبت کیلومترشمار اجباری آن.
    /// </summary>
    public void CloseFirstSegmentForSplit(
        DateTimeOffset splitAtUtc,
        long odometerEndKm,
        string reason,
        string actor)
    {
        EnsureStatus(MissionStatus.Running);
        ValidateHour(splitAtUtc, nameof(splitAtUtc));
        if (!ActualStartAtUtc.HasValue
            || splitAtUtc <= ActualStartAtUtc.Value
            || splitAtUtc >= PlannedEndAtUtc
            || splitAtUtc > DateTimeOffset.UtcNow)
        {
            throw new ArgumentException("زمان تقسیم باید پس از شروع و پیش از پایان برنامه‌ریزی‌شده و حداکثر تا زمان جاری باشد.");
        }

        SetOdometerEnd(odometerEndKm);
        ActualEndAtUtc = splitAtUtc;
        PlannedEndAtUtc = splitAtUtc;
        SplitAtUtc = splitAtUtc;
        SplitReason = Required(reason, 1000, nameof(reason));
        SplitBy = Required(actor, 64, nameof(actor));
        DecisionReason = SplitReason;
        Status = MissionStatus.Completed;
    }

    /// <summary>ثبت شناسه مأموریت مستقل مرتبط با این بخش.</summary>
    public void SetRelatedMission(Guid relatedMissionId)
    {
        if (relatedMissionId == Guid.Empty || relatedMissionId == Id)
        {
            throw new ArgumentException("شناسه مأموریت مرتبط معتبر نیست.", nameof(relatedMissionId));
        }

        RelatedMissionId = relatedMissionId;
    }

    /// <summary>ثبت نشانگر تقسیم برای بخش دوم بدون ایجاد رابطه والد و فرزند.</summary>
    public void MarkAsSecondSplitSegment(
        Guid relatedMissionId,
        DateTimeOffset splitAtUtc,
        string reason,
        string actor)
    {
        SetRelatedMission(relatedMissionId);
        SplitAtUtc = splitAtUtc;
        SplitReason = Required(reason, 1000, nameof(reason));
        SplitBy = Required(actor, 64, nameof(actor));
        DecisionReason = SplitReason;
    }

    /// <summary>
    /// اعمال یکپارچه مقادیر پیشنهادی پس از تأیید درخواست تغییر.
    /// کنترل تعارض منابع و پیوستگی کیلومترشمار در سرویس کاربردی انجام می‌شود.
    /// </summary>
    public void ApplyApprovedChanges(
        string title,
        MissionType type,
        string purpose,
        string origin,
        string destination,
        IEnumerable<string>? intermediateStops,
        DateTimeOffset plannedStartAtUtc,
        DateTimeOffset plannedEndAtUtc,
        Guid driverId,
        Guid vehicleId,
        string requesterName,
        string requesterPhoneSnapshot,
        string requesterOrganizationalUnit,
        string requestingGroup,
        string? description,
        int? passengerCount,
        string? missionOrderNumber,
        DateOnly? missionOrderDate,
        long? odometerStartKm)
    {
        if (Status is not (MissionStatus.Confirmed or MissionStatus.Running))
        {
            throw new InvalidOperationException("درخواست تغییر فقط برای مأموریت تأییدشده یا در حال اجرا قابل اعمال است.");
        }

        if (!Enum.IsDefined(type) || driverId == Guid.Empty || vehicleId == Guid.Empty)
        {
            throw new ArgumentException("نوع مأموریت، راننده یا خودرو معتبر نیست.");
        }

        ValidateHour(plannedStartAtUtc, nameof(plannedStartAtUtc));
        ValidateHour(plannedEndAtUtc, nameof(plannedEndAtUtc));
        if (plannedEndAtUtc <= plannedStartAtUtc)
        {
            throw new ArgumentException("زمان پایان باید بعد از زمان شروع باشد.");
        }

        if (ActualStartAtUtc.HasValue && plannedEndAtUtc <= ActualStartAtUtc.Value)
        {
            throw new ArgumentException("زمان پایان جدید نمی‌تواند پیش از شروع واقعی مأموریت باشد.");
        }

        if (passengerCount is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(passengerCount));
        }

        var vehicleChanged = VehicleId != vehicleId;
        if (vehicleChanged && !odometerStartKm.HasValue)
        {
            throw new InvalidOperationException("برای تغییر خودرو باید کیلومترشمار شروع خودروی جدید مشخص شود.");
        }

        Title = Required(title, 200, nameof(title));
        Type = type;
        Purpose = Required(purpose, 500, nameof(purpose));
        Origin = Required(origin, 200, nameof(origin));
        Destination = Required(destination, 200, nameof(destination));
        RouteJson = JsonSerializer.Serialize(NormalizeStops(intermediateStops));
        PlannedStartAtUtc = plannedStartAtUtc;
        PlannedEndAtUtc = plannedEndAtUtc;
        DriverId = driverId;
        VehicleId = vehicleId;
        RequesterName = Required(requesterName, 200, nameof(requesterName));
        RequesterPhoneSnapshot = Required(requesterPhoneSnapshot, 20, nameof(requesterPhoneSnapshot));
        RequesterOrganizationalUnit = Required(requesterOrganizationalUnit, 200, nameof(requesterOrganizationalUnit));
        RequestingGroup = Required(requestingGroup, 200, nameof(requestingGroup));
        Description = Optional(description, 2000);
        PassengerCount = passengerCount;
        MissionOrderNumber = Optional(missionOrderNumber, 50);
        MissionOrderDate = missionOrderDate;

        if (vehicleChanged)
        {
            OdometerStartKm = odometerStartKm;
            OdometerEndKm = null;
        }
        else if (odometerStartKm.HasValue)
        {
            SetOdometerStart(odometerStartKm.Value);
        }
    }

    /// <summary>خواندن مقصدهای میانی با حفظ ترتیب ثبت.</summary>
    public IReadOnlyList<string> GetIntermediateStops()
        => JsonSerializer.Deserialize<List<string>>(RouteJson) ?? [];

    private void EnsureStatus(MissionStatus expected)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException("عملیات با وضعیت فعلی مأموریت سازگار نیست.");
        }
    }

    private static List<string> NormalizeStops(IEnumerable<string>? stops)
    {
        return (stops ?? [])
            .Select(stop => Required(stop, 200, nameof(stops)))
            .ToList();
    }

    private static void ValidateHour(DateTimeOffset value, string parameterName)
    {
        // کنترل دقت ساعت بر مبنای ساعت رسمی تهران انجام می‌شود، نه دقیقه UTC.
        var tehranTime = value.ToOffset(TimeSpan.FromMinutes(210));
        if (tehranTime.Minute != 0 || tehranTime.Second != 0 || tehranTime.Millisecond != 0)
        {
            throw new ArgumentException("زمان مأموریت باید با دقت ساعت کامل به وقت تهران ثبت شود.", parameterName);
        }
    }

    private static string Required(string value, int maxLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentOutOfRangeException(parameterName, $"حداکثر طول مجاز {maxLength} نویسه است.");
        }

        return normalized;
    }

    private static string? Optional(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (normalized.Length > maxLength)
        {
            throw new ArgumentOutOfRangeException(nameof(value), $"حداکثر طول مجاز {maxLength} نویسه است.");
        }

        return normalized;
    }
}
