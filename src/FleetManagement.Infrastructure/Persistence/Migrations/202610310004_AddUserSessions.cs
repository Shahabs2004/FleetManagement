using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FleetManagement.Infrastructure.Persistence.Migrations;

/// <summary>
/// افزودن جدول سابقه دائمی ورود، خروج و تلاش‌های ناموفق.
/// </summary>
[DbContext(typeof(FleetDbContext))]
[Migration("202610310004_AddUserSessions")]
public sealed class AddUserSessions : Migration
{
    /// <summary>
    /// ایجاد جدول نشست‌ها و شاخص‌های جست‌وجوی سوابق.
    /// </summary>
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "UserSessions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Username = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                LoginMethod = table.Column<int>(type: "int", nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                LoginAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                LastActivityAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                EndedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                ClientIpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserSessions", session => session.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_UserSessions_Username",
            table: "UserSessions",
            column: "Username");

        migrationBuilder.CreateIndex(
            name: "IX_UserSessions_LoginAtUtc",
            table: "UserSessions",
            column: "LoginAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_UserSessions_Status_LastActivityAtUtc",
            table: "UserSessions",
            columns: new[] { "Status", "LastActivityAtUtc" });
    }

    /// <summary>
    /// حذف جدول نشست‌ها در صورت بازگشت Migration.
    /// </summary>
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "UserSessions");
    }
}
