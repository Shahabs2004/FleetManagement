using FleetManagement.Domain.Drivers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// نگاشت موجودیت راننده به جدول پایگاه داده.
/// </summary>
public sealed class DriverConfiguration : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> builder)
    {
        builder.ToTable("Drivers");

        builder.HasKey(driver => driver.Id);

        builder.Property(driver => driver.FirstName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(driver => driver.LastName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(driver => driver.MobilePhone)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(driver => driver.EmploymentStatus)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(driver => driver.IsArchived)
            .IsRequired();

        // برای جست‌وجوی سریع راننده بر اساس نام و نام خانوادگی
        builder.HasIndex(driver => new { driver.LastName, driver.FirstName });
    }
}
