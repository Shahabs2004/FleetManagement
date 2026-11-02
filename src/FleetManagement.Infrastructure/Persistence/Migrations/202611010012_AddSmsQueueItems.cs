using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FleetManagement.Infrastructure.Persistence.Migrations;

/// <summary>
/// ایجاد صف پایدار پیامک برای ارسال، تلاش مجدد و استعلام تحویل.
/// </summary>
[DbContext(typeof(FleetDbContext))]
[Migration("202611010012_AddSmsQueueItems")]
public sealed class AddSmsQueueItems : Migration
{
    /// <summary>ساخت جدول صف پیامک و شاخص‌های پردازش.</summary>
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SmsQueueItems",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EventType = table.Column<int>(type: "int", nullable: false),
                RecipientName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                RecipientPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                MissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ScheduledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                AttemptCount = table.Column<int>(type: "int", nullable: false),
                MaximumRetryCount = table.Column<int>(type: "int", nullable: false),
                RetryIntervalSeconds = table.Column<int>(type: "int", nullable: false),
                NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                LastAttemptAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                AcceptedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                DeliveredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                ProviderMessageId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                ProviderDeliveryStatus = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                ErrorReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                StatusPollCount = table.Column<int>(type: "int", nullable: false),
                MaximumStatusPollingAttempts = table.Column<int>(type: "int", nullable: false),
                NextStatusPollAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SmsQueueItems", item => item.Id);
                table.ForeignKey(
                    name: "FK_SmsQueueItems_Missions_MissionId",
                    column: item => item.MissionId,
                    principalTable: "Missions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_SmsQueueItems_MissionId_CreatedAtUtc",
            table: "SmsQueueItems",
            columns: new[] { "MissionId", "CreatedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_SmsQueueItems_ProviderMessageId",
            table: "SmsQueueItems",
            column: "ProviderMessageId");

        migrationBuilder.CreateIndex(
            name: "IX_SmsQueueItems_Status_NextAttemptAtUtc",
            table: "SmsQueueItems",
            columns: new[] { "Status", "NextAttemptAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_SmsQueueItems_Status_NextStatusPollAtUtc",
            table: "SmsQueueItems",
            columns: new[] { "Status", "NextStatusPollAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_SmsQueueItems_Status_ScheduledAtUtc",
            table: "SmsQueueItems",
            columns: new[] { "Status", "ScheduledAtUtc" });
    }

    /// <summary>حذف جدول صف پیامک در صورت بازگشت Migration.</summary>
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "SmsQueueItems");
    }
}
