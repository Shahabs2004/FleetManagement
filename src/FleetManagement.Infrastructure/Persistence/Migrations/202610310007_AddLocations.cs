using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FleetManagement.Infrastructure.Persistence.Migrations;

/// <summary>
/// ایجاد جدول مکان‌های مشترک و پیشنهادهای قابل بررسی.
/// </summary>
[DbContext(typeof(FleetDbContext))]
[Migration("202610310007_AddLocations")]
public sealed class AddLocations : Migration
{
    /// <summary>ایجاد جدول مکان‌ها و شاخص‌های جست‌وجو.</summary>
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Locations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                ProposedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                ReviewedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                ReviewReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ReviewedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Locations", location => location.Id);
            });

        migrationBuilder.CreateIndex(name: "IX_Locations_Name", table: "Locations", column: "Name");
        migrationBuilder.CreateIndex(name: "IX_Locations_Status_CreatedAtUtc", table: "Locations", columns: new[] { "Status", "CreatedAtUtc" });
    }

    /// <summary>حذف جدول مکان‌ها در بازگشت Migration.</summary>
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Locations");
    }
}
