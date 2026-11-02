using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FleetManagement.Infrastructure.Persistence.Migrations;

/// <summary>
/// ایجاد ساختار پایه درخواست و مأموریت ناوگان.
/// </summary>
[DbContext(typeof(FleetDbContext))]
[Migration("202610310006_AddMissions")]
public sealed class AddMissions : Migration
{
    /// <summary>ایجاد جدول مأموریت و روابط راننده و خودرو.</summary>
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Missions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                MissionNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                RequesterUsername = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                RequesterName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                RequesterPhoneSnapshot = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                RequesterOrganizationalUnit = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                RequestingGroup = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Type = table.Column<int>(type: "int", nullable: false),
                Purpose = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                Origin = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Destination = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                RouteJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                PlannedStartAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                PlannedEndAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                DriverId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                PassengerCount = table.Column<int>(type: "int", nullable: true),
                MissionOrderNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                MissionOrderDate = table.Column<DateOnly>(type: "date", nullable: true),
                Status = table.Column<int>(type: "int", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ReviewStartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                ApprovedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                ApprovedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                ActualStartAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                ActualEndAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                OdometerStartKm = table.Column<long>(type: "bigint", nullable: true),
                OdometerEndKm = table.Column<long>(type: "bigint", nullable: true),
                DecisionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                StatusBeforeWithdrawal = table.Column<int>(type: "int", nullable: true),
                RelatedMissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                SplitReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                SplitAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                SplitBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Missions", mission => mission.Id);
                table.ForeignKey(
                    name: "FK_Missions_Drivers_DriverId",
                    column: mission => mission.DriverId,
                    principalTable: "Drivers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Missions_Vehicles_VehicleId",
                    column: mission => mission.VehicleId,
                    principalTable: "Vehicles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_Missions_MissionNumber", table: "Missions", column: "MissionNumber", unique: true);
        migrationBuilder.CreateIndex(name: "IX_Missions_Status_PlannedStartAtUtc_PlannedEndAtUtc", table: "Missions", columns: new[] { "Status", "PlannedStartAtUtc", "PlannedEndAtUtc" });
        migrationBuilder.CreateIndex(name: "IX_Missions_DriverId_PlannedStartAtUtc_PlannedEndAtUtc", table: "Missions", columns: new[] { "DriverId", "PlannedStartAtUtc", "PlannedEndAtUtc" });
        migrationBuilder.CreateIndex(name: "IX_Missions_VehicleId_PlannedStartAtUtc_PlannedEndAtUtc", table: "Missions", columns: new[] { "VehicleId", "PlannedStartAtUtc", "PlannedEndAtUtc" });
        migrationBuilder.CreateIndex(name: "IX_Missions_RequesterUsername", table: "Missions", column: "RequesterUsername");
    }

    /// <summary>حذف جدول مأموریت در بازگشت Migration.</summary>
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Missions");
    }
}
