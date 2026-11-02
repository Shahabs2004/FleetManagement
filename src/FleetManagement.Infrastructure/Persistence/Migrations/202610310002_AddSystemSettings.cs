using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FleetManagement.Infrastructure.Persistence.Migrations;

/// <summary>
/// افزودن جدول تنظیمات عمومی سامانه.
/// </summary>
[DbContext(typeof(FleetDbContext))]
[Migration("202610310002_AddSystemSettings")]
public sealed class AddSystemSettings : Migration
{
    /// <summary>
    /// ایجاد جدول کلید و مقدار برای تنظیمات ماژول‌ها.
    /// </summary>
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SystemSettings",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Key = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                Value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SystemSettings", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_SystemSettings_Key",
            table: "SystemSettings",
            column: "Key",
            unique: true);
    }

    /// <summary>
    /// حذف جدول تنظیمات در صورت بازگشت Migration.
    /// </summary>
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "SystemSettings");
    }
}
