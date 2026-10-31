using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using FleetManagement.Infrastructure.Reporting;

namespace FleetManagement.Infrastructure.Reporting;

/// <summary>
/// تولید فایل استاندارد XLSX بدون وابستگی به کتابخانه جانبی.
/// </summary>
public sealed class MissionReportExportService
{
    private const string SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string RelationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    /// <summary>ساخت فایل اکسل چندبرگی از خروجی گزارش مأموریت.</summary>
    public byte[] CreateExcel(MissionReportResult report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var sheets = new[]
        {
            new SheetData("عملکرد رانندگان", BuildDriverRows(report)),
            new SheetData("عملکرد خودروها", BuildVehicleRows(report)),
            new SheetData("مسافت ماهانه", BuildMonthlyRows(report)),
            new SheetData("مأموریت‌های لغوشده", BuildCancelledRows(report)),
            new SheetData("خلاصه", BuildSummaryRows(report))
        };

        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddTextEntry(archive, "[Content_Types].xml", BuildContentTypes(sheets.Length));
            AddTextEntry(archive, "_rels/.rels", BuildRootRelationships());
            AddTextEntry(archive, "xl/workbook.xml", BuildWorkbook(sheets));
            AddTextEntry(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelationships(sheets.Length));

            for (var index = 0; index < sheets.Length; index++)
            {
                AddBytesEntry(
                    archive,
                    $"xl/worksheets/sheet{index + 1}.xml",
                    BuildWorksheet(sheets[index].Rows));
            }
        }

        return output.ToArray();
    }

    private static IReadOnlyList<object?[]> BuildDriverRows(MissionReportResult report)
    {
        var rows = new List<object?[]>
        {
            ["راننده", "مسافت (کیلومتر)", "تعداد مأموریت", "ساعت مأموریت"]
        };

        rows.AddRange(report.Drivers.Select(row => new object?[]
        {
            row.DriverName,
            row.TotalKilometers,
            row.MissionCount,
            row.TotalHours
        }));

        return rows;
    }

    private static IReadOnlyList<object?[]> BuildVehicleRows(MissionReportResult report)
    {
        var rows = new List<object?[]> { ["خودرو", "مسافت (کیلومتر)", "تعداد مأموریت"] };
        rows.AddRange(report.Vehicles.Select(row => new object?[]
        {
            row.VehicleName,
            row.TotalKilometers,
            row.MissionCount
        }));
        return rows;
    }

    private static IReadOnlyList<object?[]> BuildMonthlyRows(MissionReportResult report)
    {
        var rows = new List<object?[]> { ["راننده", "سال جلالی", "ماه", "مسافت (کیلومتر)"] };
        rows.AddRange(report.MonthlyDriverMileage.Select(row => new object?[]
        {
            row.DriverName,
            row.JalaliYear,
            row.JalaliMonth,
            row.TotalKilometers
        }));
        return rows;
    }

    private static IReadOnlyList<object?[]> BuildCancelledRows(MissionReportResult report)
    {
        var rows = new List<object?[]> { ["شماره مأموریت", "عنوان", "درخواست‌کننده", "علت لغو", "زمان ثبت"] };
        rows.AddRange(report.CancelledMissions.Select(row => new object?[]
        {
            row.MissionNumber,
            row.Title,
            row.RequesterUsername,
            row.Reason,
            row.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
        }));
        return rows;
    }

    private static IReadOnlyList<object?[]> BuildSummaryRows(MissionReportResult report)
    {
        return
        [
            ["شاخص", "مقدار"],
            ["مأموریت بار", report.FreightMissionCount],
            ["مأموریت مسافر", report.PassengerMissionCount],
            ["درخواست تأییدشده", report.Decisions.ApprovedCount],
            ["درخواست ردشده", report.Decisions.RejectedCount],
            ["تعداد رانندگان دارای مأموریت", report.Drivers.Count],
            ["تعداد خودروهای دارای مأموریت", report.Vehicles.Count]
        ];
    }

    private static byte[] BuildWorksheet(IReadOnlyList<object?[]> rows)
    {
        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, CreateXmlSettings()))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("worksheet", SpreadsheetNamespace);
            writer.WriteStartElement("sheetData", SpreadsheetNamespace);

            for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                writer.WriteStartElement("row", SpreadsheetNamespace);
                writer.WriteAttributeString("r", (rowIndex + 1).ToString(CultureInfo.InvariantCulture));

                for (var columnIndex = 0; columnIndex < rows[rowIndex].Length; columnIndex++)
                {
                    WriteCell(writer, rows[rowIndex][columnIndex], ColumnName(columnIndex + 1) + (rowIndex + 1));
                }

                writer.WriteEndElement();
            }

            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        return stream.ToArray();
    }

    private static void WriteCell(XmlWriter writer, object? value, string cellReference)
    {
        if (value is null) return;

        writer.WriteStartElement("c", SpreadsheetNamespace);
        writer.WriteAttributeString("r", cellReference);

        if (value is string text)
        {
            writer.WriteAttributeString("t", "inlineStr");
            writer.WriteStartElement("is", SpreadsheetNamespace);
            writer.WriteStartElement("t", SpreadsheetNamespace);
            writer.WriteAttributeString("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve");
            writer.WriteString(text);
            writer.WriteEndElement();
            writer.WriteEndElement();
        }
        else if (value is DateTime dateTime)
        {
            writer.WriteAttributeString("t", "inlineStr");
            writer.WriteStartElement("is", SpreadsheetNamespace);
            writer.WriteElementString("t", SpreadsheetNamespace, dateTime.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteEndElement();
        }
        else
        {
            writer.WriteStartElement("v", SpreadsheetNamespace);
            writer.WriteString(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }

    private static string ColumnName(int index)
    {
        var result = string.Empty;
        while (index > 0)
        {
            index--;
            result = (char)('A' + index % 26) + result;
            index /= 26;
        }

        return result;
    }

    private static string BuildContentTypes(int sheetCount)
    {
        var builder = new StringBuilder();
        using var writer = XmlWriter.Create(builder, CreateXmlSettings());
        writer.WriteStartDocument();
        writer.WriteStartElement("Types", "http://schemas.openxmlformats.org/package/2006/content-types");
        writer.WriteStartElement("Default");
        writer.WriteAttributeString("Extension", "rels");
        writer.WriteAttributeString("ContentType", "application/vnd.openxmlformats-package.relationships+xml");
        writer.WriteEndElement();
        writer.WriteStartElement("Default");
        writer.WriteAttributeString("Extension", "xml");
        writer.WriteAttributeString("ContentType", "application/xml");
        writer.WriteEndElement();
        writer.WriteStartElement("Override");
        writer.WriteAttributeString("PartName", "/xl/workbook.xml");
        writer.WriteAttributeString("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml");
        writer.WriteEndElement();

        for (var index = 1; index <= sheetCount; index++)
        {
            writer.WriteStartElement("Override");
            writer.WriteAttributeString("PartName", $"/xl/worksheets/sheet{index}.xml");
            writer.WriteAttributeString("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndDocument();
        return builder.ToString();
    }

    private static string BuildRootRelationships()
    {
        var builder = new StringBuilder();
        using var writer = XmlWriter.Create(builder, CreateXmlSettings());
        writer.WriteStartDocument();
        writer.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");
        writer.WriteStartElement("Relationship");
        writer.WriteAttributeString("Id", "rId1");
        writer.WriteAttributeString("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument");
        writer.WriteAttributeString("Target", "xl/workbook.xml");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
        return builder.ToString();
    }

    private static string BuildWorkbook(IReadOnlyList<SheetData> sheets)
    {
        var builder = new StringBuilder();
        using var writer = XmlWriter.Create(builder, CreateXmlSettings());
        writer.WriteStartDocument();
        writer.WriteStartElement("workbook", SpreadsheetNamespace);
        writer.WriteAttributeString("xmlns", "r", null, RelationshipNamespace);
        writer.WriteStartElement("sheets", SpreadsheetNamespace);

        for (var index = 0; index < sheets.Count; index++)
        {
            writer.WriteStartElement("sheet", SpreadsheetNamespace);
            writer.WriteAttributeString("name", sheets[index].Name);
            writer.WriteAttributeString("sheetId", (index + 1).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("r", "id", RelationshipNamespace, $"rId{index + 1}");
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
        return builder.ToString();
    }

    private static string BuildWorkbookRelationships(int sheetCount)
    {
        var builder = new StringBuilder();
        using var writer = XmlWriter.Create(builder, CreateXmlSettings());
        writer.WriteStartDocument();
        writer.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");

        for (var index = 1; index <= sheetCount; index++)
        {
            writer.WriteStartElement("Relationship");
            writer.WriteAttributeString("Id", $"rId{index}");
            writer.WriteAttributeString("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet");
            writer.WriteAttributeString("Target", $"worksheets/sheet{index}.xml");
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndDocument();
        return builder.ToString();
    }

    private static void AddTextEntry(ZipArchive archive, string name, string content)
        => AddBytesEntry(archive, name, Encoding.UTF8.GetBytes(content));

    private static void AddBytesEntry(ZipArchive archive, string name, byte[] content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(content, 0, content.Length);
    }

    private static XmlWriterSettings CreateXmlSettings() => new()
    {
        Encoding = new UTF8Encoding(false),
        OmitXmlDeclaration = false,
        Indent = false
    };

    private sealed record SheetData(string Name, IReadOnlyList<object?[]> Rows);
}
