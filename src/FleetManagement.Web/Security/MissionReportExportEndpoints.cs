using System.Globalization;
using FleetManagement.Domain.Missions;
using FleetManagement.Infrastructure.Reporting;

namespace FleetManagement.Web.Security;

/// <summary>
/// مسیر امن دریافت خروجی اکسل گزارش مأموریت‌ها.
/// </summary>
public static class MissionReportExportEndpoints
{
    /// <summary>ثبت endpoint خروجی Excel با مجوز مستقل.</summary>
    public static IEndpointRouteBuilder MapMissionReportExport(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/reports/missions/export.xlsx", ExportExcelAsync)
            .RequireAuthorization("Permission:reports.export");
        return endpoints;
    }

    /// <summary>ساخت فایل اکسل با همان فیلترهای گزارش.</summary>
    private static async Task<IResult> ExportExcelAsync(
        HttpContext context,
        MissionReportService reportService,
        MissionReportExportService exportService)
    {
        var query = context.Request.Query;
        var filter = new MissionReportFilter
        {
            FromUtc = ParseDate(query["from"]),
            ToUtc = ParseDate(query["to"]),
            DriverId = Guid.TryParse(query["driverId"], out var driverId) ? driverId : null,
            VehicleId = Guid.TryParse(query["vehicleId"], out var vehicleId) ? vehicleId : null,
            Type = int.TryParse(query["type"], out var type)
                && Enum.IsDefined((MissionType)type) ? (MissionType)type : null,
            Status = int.TryParse(query["status"], out var status)
                && Enum.IsDefined((MissionStatus)status) ? (MissionStatus)status : null,
            OrganizationalUnit = query["unit"].ToString()
        };

        var report = await reportService.BuildAsync(filter, context.RequestAborted);
        var bytes = exportService.CreateExcel(report);

        return Results.File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "گزارش-عملکرد-ناوگان.xlsx");
    }

    /// <summary>خواندن تاریخ ISO از query string.</summary>
    private static DateTimeOffset? ParseDate(string? value)
    {
        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var parsed)
            ? parsed
            : null;
    }
}
