using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FleetManagement.Infrastructure.Persistence.Migrations;

/// <summary>افزودن تصویر گروه‌های مجاز AD به پروفایل کاربران.</summary>
[DbContext(typeof(FleetDbContext))]
[Migration("202610310011_AddDirectoryGroups")]
public sealed class AddDirectoryGroups : Migration
{
    /// <summary>افزودن ستون JSON گروه‌های مجاز.</summary>
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "DirectoryGroupsJson",
            table: "ApplicationUsers",
            type: "nvarchar(max)",
            maxLength: 12000,
            nullable: false,
            defaultValue: "[]");
    }

    /// <summary>حذف ستون در بازگشت Migration.</summary>
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "DirectoryGroupsJson", table: "ApplicationUsers");
    }
}
