using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FleetManagement.Infrastructure.Persistence.Migrations;

/// <summary>
/// ایجاد جدول پروفایل کاربران سازمانی و شاخص‌های یکتای هویت دامنه.
/// </summary>
[DbContext(typeof(FleetDbContext))]
[Migration("202610310003_AddApplicationUsers")]
public sealed class AddApplicationUsers : Migration
{
    /// <summary>
    /// ایجاد جدول کاربران برای نگهداری وضعیت داخلی و اطلاعات همگام‌شده از AD.
    /// </summary>
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ApplicationUsers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Username = table.Column<string>(
                    type: "nvarchar(64)",
                    maxLength: 64,
                    nullable: false,
                    collation: "Latin1_General_100_CI_AS"),
                FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                OrganizationalUnit = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                IsOrganizationalUnitManuallyOverridden = table.Column<bool>(type: "bit", nullable: false),
                DirectoryDistinguishedName = table.Column<string>(
                    type: "nvarchar(2048)",
                    maxLength: 2048,
                    nullable: false),
                IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                LastDirectorySyncAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ApplicationUsers", user => user.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ApplicationUsers_Username",
            table: "ApplicationUsers",
            column: "Username",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ApplicationUsers_DirectoryDistinguishedName",
            table: "ApplicationUsers",
            column: "DirectoryDistinguishedName",
            unique: true);
    }

    /// <summary>
    /// حذف جدول پروفایل کاربران در صورت بازگشت Migration.
    /// </summary>
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ApplicationUsers");
    }
}
