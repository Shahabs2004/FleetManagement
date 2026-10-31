using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FleetManagement.Infrastructure.Persistence.Migrations;

/// <summary>افزودن واحد سازمانی اصلی AD برای پشتیبانی صحیح از override دستی.</summary>
[DbContext(typeof(FleetDbContext))]
[Migration("202610310010_AddDirectoryOrganizationalUnit")]
public sealed class AddDirectoryOrganizationalUnit : Migration
{
    /// <summary>افزودن ستون و مقداردهی اولیه از واحد سازمانی فعلی.</summary>
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "DirectoryOrganizationalUnit",
            table: "ApplicationUsers",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: false,
            defaultValue: "");

        migrationBuilder.Sql("""
            UPDATE dbo.ApplicationUsers
            SET DirectoryOrganizationalUnit = OrganizationalUnit;
            """);
    }

    /// <summary>حذف ستون در بازگشت Migration.</summary>
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "DirectoryOrganizationalUnit",
            table: "ApplicationUsers");
    }
}
