using System.IO.Compression;
using System.Xml.Linq;
using FleetManagement.Infrastructure.Reporting;

namespace FleetManagement.Domain.Tests.Reporting;

public sealed class MissionReportExportServiceTests
{
    [Fact]
    public void Excel_export_formats_cancelled_mission_timestamp_as_Persian_Tehran_date()
    {
        var report = new MissionReportResult(
            [],
            [],
            [],
            [new CancelledMissionRow(
                "۱۴۰۵-۰۰۱",
                "عنوان آزمون",
                "علت آزمون",
                new DateTimeOffset(2026, 3, 21, 12, 30, 0, TimeSpan.Zero),
                "integration.requester")],
            new MissionDecisionSummary(0, 0),
            0,
            0);
        var bytes = new MissionReportExportService().CreateExcel(report);

        using var stream = new MemoryStream(bytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        using var sheet = archive.GetEntry("xl/worksheets/sheet4.xml")!.Open();
        var document = XDocument.Load(sheet);
        XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var timestamp = document.Descendants(spreadsheet + "c")
            .Single(cell => (string?)cell.Attribute("r") == "E2")
            .Element(spreadsheet + "is")!
            .Element(spreadsheet + "t")!
            .Value;

        Assert.Equal("۱۴۰۵/۰۱/۰۱ ۱۶:۰۰", timestamp);
    }

    [Fact]
    public void Excel_export_contains_well_formed_UTF8_workbook_parts()
    {
        var report = new MissionReportResult([], [], [], [], new MissionDecisionSummary(0, 0), 0, 0);
        var bytes = new MissionReportExportService().CreateExcel(report);

        using var stream = new MemoryStream(bytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var xmlParts = archive.Entries
            .Where(entry => entry.FullName.EndsWith(".xml", StringComparison.Ordinal)
                || entry.FullName.EndsWith(".rels", StringComparison.Ordinal))
            .ToDictionary(entry => entry.FullName, entry =>
            {
                using var part = entry.Open();
                return XDocument.Load(part);
            });

        Assert.Equal(9, xmlParts.Count);
        XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        Assert.Equal(5, xmlParts["xl/workbook.xml"].Root!
            .Element(spreadsheet + "sheets")!.Elements(spreadsheet + "sheet").Count());
    }
}
