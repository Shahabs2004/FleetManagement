using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FleetManagement.Infrastructure.Persistence.Migrations;

/// <summary>
/// افزودن توکن هم‌زمانی خوش‌بینانه به منابع اصلی و قیود نهایی پایگاه داده مأموریت.
/// </summary>
[DbContext(typeof(FleetDbContext))]
[Migration("202611020013_AddRowVersionAndMissionConstraints")]
public sealed class AddRowVersionAndMissionConstraints : Migration
{
    /// <summary>افزودن RowVersion و قیود صحت بازه زمانی، کیلومترشمار و تعداد مسافر.</summary>
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<byte[]>(
            name: "RowVersion",
            table: "Missions",
            type: "rowversion",
            rowVersion: true,
            nullable: false);

        migrationBuilder.AddColumn<byte[]>(
            name: "RowVersion",
            table: "Drivers",
            type: "rowversion",
            rowVersion: true,
            nullable: false);

        migrationBuilder.AddColumn<byte[]>(
            name: "RowVersion",
            table: "Vehicles",
            type: "rowversion",
            rowVersion: true,
            nullable: false);

        migrationBuilder.AddCheckConstraint(
            name: "CK_Missions_PlannedTimeRange",
            table: "Missions",
            sql: "[PlannedEndAtUtc] > [PlannedStartAtUtc]");

        migrationBuilder.AddCheckConstraint(
            name: "CK_Missions_OdometerRange",
            table: "Missions",
            sql: "[OdometerStartKm] IS NULL OR [OdometerEndKm] IS NULL OR [OdometerEndKm] >= [OdometerStartKm]");

        migrationBuilder.AddCheckConstraint(
            name: "CK_Missions_PassengerCount_NonNegative",
            table: "Missions",
            sql: "[PassengerCount] IS NULL OR [PassengerCount] >= 0");
    }

    /// <summary>حذف قیود و توکن‌های هم‌زمانی در صورت بازگشت Migration.</summary>
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_Missions_PassengerCount_NonNegative",
            table: "Missions");

        migrationBuilder.DropCheckConstraint(
            name: "CK_Missions_OdometerRange",
            table: "Missions");

        migrationBuilder.DropCheckConstraint(
            name: "CK_Missions_PlannedTimeRange",
            table: "Missions");

        migrationBuilder.DropColumn(name: "RowVersion", table: "Missions");
        migrationBuilder.DropColumn(name: "RowVersion", table: "Drivers");
        migrationBuilder.DropColumn(name: "RowVersion", table: "Vehicles");
    }
}
