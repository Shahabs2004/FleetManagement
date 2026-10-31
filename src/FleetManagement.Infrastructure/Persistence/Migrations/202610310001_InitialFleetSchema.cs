using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FleetManagement.Infrastructure.Persistence.Migrations;

/// <summary>
/// ساخت جداول پایه رانندگان و خودروها.
/// </summary>
[DbContext(typeof(FleetDbContext))]
[Migration("202610310001_InitialFleetSchema")]
public sealed class InitialFleetSchema : Migration
{
    /// <summary>
    /// ایجاد جدول‌های اصلی ناوگان و محدودیت یکتایی پلاک.
    /// </summary>
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Drivers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                MobilePhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                EmploymentStatus = table.Column<int>(type: "int", nullable: false),
                IsArchived = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Drivers", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Vehicles",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Brand = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                LicensePlate = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                ManualStatus = table.Column<int>(type: "int", nullable: false),
                IsArchived = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Vehicles", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Drivers_LastName_FirstName",
            table: "Drivers",
            columns: new[] { "LastName", "FirstName" });

        migrationBuilder.CreateIndex(
            name: "IX_Vehicles_LicensePlate",
            table: "Vehicles",
            column: "LicensePlate",
            unique: true);
    }

    /// <summary>
    /// حذف ساختار اولیه در صورت بازگشت Migration.
    /// </summary>
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Drivers");
        migrationBuilder.DropTable(name: "Vehicles");
    }
}
