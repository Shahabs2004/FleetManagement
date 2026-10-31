using FleetManagement.Domain.Drivers;
using FleetManagement.Domain.Missions;
using FleetManagement.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// نگاشت درخواست‌ها و مأموریت‌های تأییدشده به SQL Server.
/// </summary>
public sealed class MissionConfiguration : IEntityTypeConfiguration<Mission>
{
    /// <summary>تعریف ستون‌ها، روابط و شاخص‌های جست‌وجوی مأموریت.</summary>
    public void Configure(EntityTypeBuilder<Mission> builder)
    {
        builder.ToTable("Missions");
        builder.HasKey(mission => mission.Id);

        builder.Property(mission => mission.MissionNumber).HasMaxLength(20).IsRequired();
        builder.Property(mission => mission.Title).HasMaxLength(200).IsRequired();
        builder.Property(mission => mission.RequesterUsername).HasMaxLength(64).IsRequired();
        builder.Property(mission => mission.RequesterName).HasMaxLength(200).IsRequired();
        builder.Property(mission => mission.RequesterPhoneSnapshot).HasMaxLength(20).IsRequired();
        builder.Property(mission => mission.RequesterOrganizationalUnit).HasMaxLength(200).IsRequired();
        builder.Property(mission => mission.RequestingGroup).HasMaxLength(200).IsRequired();
        builder.Property(mission => mission.Type).HasConversion<int>().IsRequired();
        builder.Property(mission => mission.Purpose).HasMaxLength(500).IsRequired();
        builder.Property(mission => mission.Origin).HasMaxLength(200).IsRequired();
        builder.Property(mission => mission.Destination).HasMaxLength(200).IsRequired();
        builder.Property(mission => mission.RouteJson).IsRequired();
        builder.Property(mission => mission.Description).HasMaxLength(2000);
        builder.Property(mission => mission.MissionOrderNumber).HasMaxLength(50);
        builder.Property(mission => mission.Status).HasConversion<int>().IsRequired();
        builder.Property(mission => mission.ApprovedBy).HasMaxLength(64);
        builder.Property(mission => mission.DecisionReason).HasMaxLength(1000);
        builder.Property(mission => mission.SplitReason).HasMaxLength(1000);
        builder.Property(mission => mission.SplitBy).HasMaxLength(64);

        builder.HasOne<Driver>()
            .WithMany()
            .HasForeignKey(mission => mission.DriverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Vehicle>()
            .WithMany()
            .HasForeignKey(mission => mission.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(mission => mission.MissionNumber).IsUnique();
        builder.HasIndex(mission => new { mission.Status, mission.PlannedStartAtUtc, mission.PlannedEndAtUtc });
        builder.HasIndex(mission => new { mission.DriverId, mission.PlannedStartAtUtc, mission.PlannedEndAtUtc });
        builder.HasIndex(mission => new { mission.VehicleId, mission.PlannedStartAtUtc, mission.PlannedEndAtUtc });
        builder.HasIndex(mission => mission.RequesterUsername);
    }
}
