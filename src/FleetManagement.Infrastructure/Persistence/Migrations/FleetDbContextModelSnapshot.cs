using FleetManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace FleetManagement.Infrastructure.Persistence.Migrations;

/// <summary>
/// تصویر مدل داده در زمان Migration اولیه.
/// EF Core از این فایل برای تولید Migrationهای بعدی استفاده می‌کند.
/// </summary>
[DbContext(typeof(FleetDbContext))]
public sealed class FleetDbContextModelSnapshot : ModelSnapshot
{
    /// <summary>
    /// تعریف ساختار فعلی مدل داده در Snapshot.
    /// </summary>
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasAnnotation("ProductVersion", "10.0.14")
            .HasAnnotation("Relational:MaxIdentifierLength", 128);

        modelBuilder.Entity("FleetManagement.Domain.Drivers.Driver", entity =>
        {
            entity.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("uniqueidentifier");

            entity.Property<string>("FirstName")
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnType("nvarchar(100)");

            entity.Property<string>("LastName")
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnType("nvarchar(100)");

            entity.Property<string>("MobilePhone")
                .IsRequired()
                .HasMaxLength(20)
                .HasColumnType("nvarchar(20)");

            entity.Property<int>("EmploymentStatus")
                .HasColumnType("int");

            entity.Property<bool>("IsArchived")
                .HasColumnType("bit");

            entity.HasKey("Id");
            entity.HasIndex("LastName", "FirstName");
            entity.ToTable("Drivers");
        });

        modelBuilder.Entity("FleetManagement.Domain.Vehicles.Vehicle", entity =>
        {
            entity.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("uniqueidentifier");

            entity.Property<string>("Brand")
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnType("nvarchar(100)");

            entity.Property<string>("Model")
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnType("nvarchar(100)");

            entity.Property<string>("LicensePlate")
                .IsRequired()
                .HasMaxLength(30)
                .HasColumnType("nvarchar(30)");

            entity.Property<int>("ManualStatus")
                .HasColumnType("int");

            entity.Property<bool>("IsArchived")
                .HasColumnType("bit");

            entity.HasKey("Id");
            entity.HasIndex("LicensePlate").IsUnique();
            entity.ToTable("Vehicles");
        });
    }
}
