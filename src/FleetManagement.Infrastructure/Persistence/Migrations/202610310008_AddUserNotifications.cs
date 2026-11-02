using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FleetManagement.Infrastructure.Persistence.Migrations;

/// <summary>ایجاد جدول اعلان‌های داخلی کاربران.</summary>
[DbContext(typeof(FleetDbContext))]
[Migration("202610310008_AddUserNotifications")]
public sealed class AddUserNotifications : Migration
{
    /// <summary>ایجاد جدول اعلان و شاخص‌های فهرست و خوانده‌نشده.</summary>
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "UserNotifications",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RecipientUsername = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                EventKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                ReferenceType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                ReferenceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ReadAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserNotifications", notification => notification.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_UserNotifications_RecipientUsername_CreatedAtUtc",
            table: "UserNotifications",
            columns: new[] { "RecipientUsername", "CreatedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_UserNotifications_RecipientUsername_ReadAtUtc",
            table: "UserNotifications",
            columns: new[] { "RecipientUsername", "ReadAtUtc" });
    }

    /// <summary>حذف جدول اعلان‌ها در بازگشت Migration.</summary>
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "UserNotifications");
    }
}
