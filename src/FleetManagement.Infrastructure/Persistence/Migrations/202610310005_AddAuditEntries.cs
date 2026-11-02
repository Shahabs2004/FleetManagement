using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FleetManagement.Infrastructure.Persistence.Migrations;

/// <summary>
/// ایجاد جدول دائمی رویدادهای ممیزی و شاخص‌های جست‌وجو.
/// </summary>
[DbContext(typeof(FleetDbContext))]
[Migration("202610310005_AddAuditEntries")]
public sealed class AddAuditEntries : Migration
{
    /// <summary>
    /// ایجاد جدول رویدادهای ممیزی.
    /// </summary>
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AuditEntries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ActorUsername = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                Action = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                EntityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Outcome = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                BeforeJson = table.Column<string>(type: "nvarchar(max)", maxLength: 12000, nullable: true),
                AfterJson = table.Column<string>(type: "nvarchar(max)", maxLength: 12000, nullable: true),
                ClientIpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuditEntries", entry => entry.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AuditEntries_OccurredAtUtc",
            table: "AuditEntries",
            column: "OccurredAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_AuditEntries_EntityType_EntityId",
            table: "AuditEntries",
            columns: new[] { "EntityType", "EntityId" });

        migrationBuilder.CreateIndex(
            name: "IX_AuditEntries_ActorUsername_OccurredAtUtc",
            table: "AuditEntries",
            columns: new[] { "ActorUsername", "OccurredAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_AuditEntries_Action_Outcome",
            table: "AuditEntries",
            columns: new[] { "Action", "Outcome" });

        // جلوگیری از تغییر یا حذف سوابق ممیزی حتی در صورت خطای برنامه.
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_AuditEntries_Immutable
            ON dbo.AuditEntries
            AFTER UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                THROW 51000, N'سوابق ممیزی قابل ویرایش یا حذف نیستند.', 1;
            END;
            """);
    }

    /// <summary>
    /// حذف جدول ممیزی فقط در بازگشت Migration.
    /// </summary>
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AuditEntries");
    }
}
