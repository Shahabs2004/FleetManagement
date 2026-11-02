using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FleetManagement.Infrastructure.Persistence.Migrations;

/// <summary>ایجاد جدول درخواست‌های تغییر مأموریت.</summary>
[DbContext(typeof(FleetDbContext))]
[Migration("202610310009_AddMissionChangeRequests")]
public sealed class AddMissionChangeRequests : Migration
{
    /// <summary>ایجاد جدول درخواست تغییر و رابطه با مأموریت اصلی.</summary>
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "MissionChangeRequests",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                MissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RequesterUsername = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                ProposedValuesJson = table.Column<string>(type: "nvarchar(max)", maxLength: 12000, nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                ReviewerUsername = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                ReviewStartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                ReviewedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                DecisionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MissionChangeRequests", request => request.Id);
                table.ForeignKey(
                    name: "FK_MissionChangeRequests_Missions_MissionId",
                    column: request => request.MissionId,
                    principalTable: "Missions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_MissionChangeRequests_MissionId_Status",
            table: "MissionChangeRequests",
            columns: new[] { "MissionId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_MissionChangeRequests_RequesterUsername_CreatedAtUtc",
            table: "MissionChangeRequests",
            columns: new[] { "RequesterUsername", "CreatedAtUtc" });
    }

    /// <summary>حذف جدول درخواست تغییر در بازگشت Migration.</summary>
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "MissionChangeRequests");
    }
}
