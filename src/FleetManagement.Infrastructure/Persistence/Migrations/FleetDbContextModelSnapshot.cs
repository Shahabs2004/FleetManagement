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

        modelBuilder.Entity("FleetManagement.Domain.Users.ApplicationUser", entity =>
        {
            entity.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("uniqueidentifier");

            entity.Property<string>("Username")
                .IsRequired()
                .HasMaxLength(64)
                .UseCollation("Latin1_General_100_CI_AS")
                .HasColumnType("nvarchar(64)");

            entity.Property<string>("FirstName")
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnType("nvarchar(100)");

            entity.Property<string>("LastName")
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnType("nvarchar(100)");

            entity.Property<string>("OrganizationalUnit")
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnType("nvarchar(200)");

            entity.Property<bool>("IsOrganizationalUnitManuallyOverridden")
                .HasColumnType("bit");

            entity.Property<string>("DirectoryDistinguishedName")
                .IsRequired()
                .HasMaxLength(2048)
                .HasColumnType("nvarchar(2048)");

            entity.Property<bool>("IsDisabled")
                .HasColumnType("bit");

            entity.Property<DateTimeOffset>("CreatedAtUtc")
                .HasColumnType("datetimeoffset");

            entity.Property<DateTimeOffset>("LastDirectorySyncAtUtc")
                .HasColumnType("datetimeoffset");

            entity.HasKey("Id");
            entity.HasIndex("DirectoryDistinguishedName").IsUnique();
            entity.HasIndex("Username").IsUnique();
            entity.ToTable("ApplicationUsers");
        });

        modelBuilder.Entity("FleetManagement.Domain.Sessions.UserSession", entity =>
        {
            entity.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("uniqueidentifier");

            entity.Property<string>("Username")
                .IsRequired()
                .HasMaxLength(64)
                .HasColumnType("nvarchar(64)");

            entity.Property<int>("LoginMethod")
                .HasColumnType("int");

            entity.Property<int>("Status")
                .HasColumnType("int");

            entity.Property<DateTimeOffset>("LoginAtUtc")
                .HasColumnType("datetimeoffset");

            entity.Property<DateTimeOffset>("LastActivityAtUtc")
                .HasColumnType("datetimeoffset");

            entity.Property<DateTimeOffset?>("EndedAtUtc")
                .HasColumnType("datetimeoffset");

            entity.Property<string>("ClientIpAddress")
                .HasMaxLength(45)
                .HasColumnType("nvarchar(45)");

            entity.Property<string>("Reason")
                .HasMaxLength(300)
                .HasColumnType("nvarchar(300)");

            entity.HasKey("Id");
            entity.HasIndex("LoginAtUtc");
            entity.HasIndex("Status", "LastActivityAtUtc");
            entity.HasIndex("Username");
            entity.ToTable("UserSessions");
        });

        modelBuilder.Entity("FleetManagement.Domain.Configuration.SystemSetting", entity =>
        {
            entity.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("uniqueidentifier");

            entity.Property<string>("Key")
                .IsRequired()
                .HasMaxLength(128)
                .HasColumnType("nvarchar(128)");

            entity.Property<string>("Value")
                .IsRequired()
                .HasColumnType("nvarchar(max)");

            entity.Property<DateTimeOffset>("UpdatedAtUtc")
                .HasColumnType("datetimeoffset");

            entity.HasKey("Id");
            entity.HasIndex("Key").IsUnique();
            entity.ToTable("SystemSettings");
        });
    }
}
