using FleetManagement.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// نگاشت موجودیت خودرو به جدول پایگاه داده.
/// </summary>
public sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("Vehicles");

        builder.HasKey(vehicle => vehicle.Id);
        builder.Property<byte[]>("RowVersion").IsRowVersion();

        builder.Property(vehicle => vehicle.Brand)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(vehicle => vehicle.Model)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(vehicle => vehicle.LicensePlate)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(vehicle => vehicle.ManualStatus)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(vehicle => vehicle.IsArchived)
            .IsRequired();

        // هر پلاک باید در سامانه یکتا باشد.
        builder.HasIndex(vehicle => vehicle.LicensePlate)
            .IsUnique();
    }
}
