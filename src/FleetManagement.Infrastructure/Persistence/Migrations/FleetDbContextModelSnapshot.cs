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

            entity.Property<byte[]>("RowVersion")
                .IsConcurrencyToken()
                .ValueGeneratedOnAddOrUpdate()
                .HasColumnType("rowversion");

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

            entity.Property<byte[]>("RowVersion")
                .IsConcurrencyToken()
                .ValueGeneratedOnAddOrUpdate()
                .HasColumnType("rowversion");

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

            entity.Property<string>("DirectoryOrganizationalUnit")
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnType("nvarchar(200)");

            entity.Property<bool>("IsOrganizationalUnitManuallyOverridden")
                .HasColumnType("bit");

            entity.Property<string>("DirectoryGroupsJson")
                .IsRequired()
                .HasMaxLength(12000)
                .HasColumnType("nvarchar(max)");

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

        modelBuilder.Entity("FleetManagement.Domain.Missions.MissionChangeRequest", entity =>
        {
            entity.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uniqueidentifier");
            entity.Property<Guid>("MissionId").HasColumnType("uniqueidentifier");
            entity.Property<string>("RequesterUsername").IsRequired().HasMaxLength(64).HasColumnType("nvarchar(64)");
            entity.Property<string>("Reason").IsRequired().HasMaxLength(1000).HasColumnType("nvarchar(1000)");
            entity.Property<string>("ProposedValuesJson").IsRequired().HasMaxLength(12000).HasColumnType("nvarchar(max)");
            entity.Property<int>("Status").HasColumnType("int");
            entity.Property<string>("ReviewerUsername").HasMaxLength(64).HasColumnType("nvarchar(64)");
            entity.Property<DateTimeOffset?>("ReviewStartedAtUtc").HasColumnType("datetimeoffset");
            entity.Property<DateTimeOffset?>("ReviewedAtUtc").HasColumnType("datetimeoffset");
            entity.Property<string>("DecisionReason").HasMaxLength(1000).HasColumnType("nvarchar(1000)");
            entity.Property<DateTimeOffset>("CreatedAtUtc").HasColumnType("datetimeoffset");
            entity.HasKey("Id");
            entity.HasIndex("MissionId", "Status");
            entity.HasIndex("RequesterUsername", "CreatedAtUtc");
            entity.ToTable("MissionChangeRequests");
        });

        modelBuilder.Entity("FleetManagement.Domain.Missions.MissionChangeRequest", entity =>
        {
            entity.HasOne("FleetManagement.Domain.Missions.Mission", null)
                .WithMany()
                .HasForeignKey("MissionId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired()
                .HasConstraintName("FK_MissionChangeRequests_Missions_MissionId");
        });

        modelBuilder.Entity("FleetManagement.Domain.Missions.MissionChangeRequest", entity =>
        {
            entity.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uniqueidentifier");
            entity.Property<Guid>("MissionId").HasColumnType("uniqueidentifier");
            entity.Property<string>("RequesterUsername").IsRequired().HasMaxLength(64).HasColumnType("nvarchar(64)");
            entity.Property<string>("Reason").IsRequired().HasMaxLength(1000).HasColumnType("nvarchar(1000)");
            entity.Property<string>("ProposedValuesJson").IsRequired().HasMaxLength(12000).HasColumnType("nvarchar(max)");
            entity.Property<int>("Status").HasColumnType("int");
            entity.Property<string>("ReviewerUsername").HasMaxLength(64).HasColumnType("nvarchar(64)");
            entity.Property<DateTimeOffset?>("ReviewStartedAtUtc").HasColumnType("datetimeoffset");
            entity.Property<DateTimeOffset?>("ReviewedAtUtc").HasColumnType("datetimeoffset");
            entity.Property<string>("DecisionReason").HasMaxLength(1000).HasColumnType("nvarchar(1000)");
            entity.Property<DateTimeOffset>("CreatedAtUtc").HasColumnType("datetimeoffset");
            entity.HasKey("Id");
            entity.HasIndex("MissionId", "Status");
            entity.HasIndex("RequesterUsername", "CreatedAtUtc");
            entity.ToTable("MissionChangeRequests");
        });

        modelBuilder.Entity("FleetManagement.Domain.Missions.MissionChangeRequest", entity =>
        {
            entity.HasOne("FleetManagement.Domain.Missions.Mission", null)
                .WithMany()
                .HasForeignKey("MissionId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired()
                .HasConstraintName("FK_MissionChangeRequests_Missions_MissionId");
        });

        modelBuilder.Entity("FleetManagement.Domain.Locations.Location", entity =>
        {
            entity.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uniqueidentifier");
            entity.Property<string>("Name").IsRequired().HasMaxLength(200).HasColumnType("nvarchar(200)");
            entity.Property<int>("Status").HasColumnType("int");
            entity.Property<string>("ProposedBy").IsRequired().HasMaxLength(64).HasColumnType("nvarchar(64)");
            entity.Property<string>("ReviewedBy").HasMaxLength(64).HasColumnType("nvarchar(64)");
            entity.Property<string>("ReviewReason").HasMaxLength(500).HasColumnType("nvarchar(500)");
            entity.Property<DateTimeOffset>("CreatedAtUtc").HasColumnType("datetimeoffset");
            entity.Property<DateTimeOffset?>("ReviewedAtUtc").HasColumnType("datetimeoffset");
            entity.HasKey("Id");
            entity.HasIndex("Name");
            entity.HasIndex("Status", "CreatedAtUtc");
            entity.ToTable("Locations");
        });

        modelBuilder.Entity("FleetManagement.Domain.Missions.Mission", entity =>
        {
            entity.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uniqueidentifier");
            entity.Property<byte[]>("RowVersion").IsConcurrencyToken().ValueGeneratedOnAddOrUpdate().HasColumnType("rowversion");
            entity.Property<string>("MissionNumber").IsRequired().HasMaxLength(20).HasColumnType("nvarchar(20)");
            entity.Property<string>("Title").IsRequired().HasMaxLength(200).HasColumnType("nvarchar(200)");
            entity.Property<string>("RequesterUsername").IsRequired().HasMaxLength(64).HasColumnType("nvarchar(64)");
            entity.Property<string>("RequesterName").IsRequired().HasMaxLength(200).HasColumnType("nvarchar(200)");
            entity.Property<string>("RequesterPhoneSnapshot").IsRequired().HasMaxLength(20).HasColumnType("nvarchar(20)");
            entity.Property<string>("RequesterOrganizationalUnit").IsRequired().HasMaxLength(200).HasColumnType("nvarchar(200)");
            entity.Property<string>("RequestingGroup").IsRequired().HasMaxLength(200).HasColumnType("nvarchar(200)");
            entity.Property<int>("Type").HasColumnType("int");
            entity.Property<string>("Purpose").IsRequired().HasMaxLength(500).HasColumnType("nvarchar(500)");
            entity.Property<string>("Origin").IsRequired().HasMaxLength(200).HasColumnType("nvarchar(200)");
            entity.Property<string>("Destination").IsRequired().HasMaxLength(200).HasColumnType("nvarchar(200)");
            entity.Property<string>("RouteJson").IsRequired().HasColumnType("nvarchar(max)");
            entity.Property<DateTimeOffset>("PlannedStartAtUtc").HasColumnType("datetimeoffset");
            entity.Property<DateTimeOffset>("PlannedEndAtUtc").HasColumnType("datetimeoffset");
            entity.Property<Guid?>("DriverId").HasColumnType("uniqueidentifier");
            entity.Property<Guid?>("VehicleId").HasColumnType("uniqueidentifier");
            entity.Property<string>("Description").HasMaxLength(2000).HasColumnType("nvarchar(2000)");
            entity.Property<int?>("PassengerCount").HasColumnType("int");
            entity.Property<string>("MissionOrderNumber").HasMaxLength(50).HasColumnType("nvarchar(50)");
            entity.Property<DateOnly?>("MissionOrderDate").HasColumnType("date");
            entity.Property<int>("Status").HasColumnType("int");
            entity.Property<DateTimeOffset>("CreatedAtUtc").HasColumnType("datetimeoffset");
            entity.Property<DateTimeOffset?>("ReviewStartedAtUtc").HasColumnType("datetimeoffset");
            entity.Property<string>("ApprovedBy").HasMaxLength(64).HasColumnType("nvarchar(64)");
            entity.Property<DateTimeOffset?>("ApprovedAtUtc").HasColumnType("datetimeoffset");
            entity.Property<DateTimeOffset?>("ActualStartAtUtc").HasColumnType("datetimeoffset");
            entity.Property<DateTimeOffset?>("ActualEndAtUtc").HasColumnType("datetimeoffset");
            entity.Property<long?>("OdometerStartKm").HasColumnType("bigint");
            entity.Property<long?>("OdometerEndKm").HasColumnType("bigint");
            entity.Property<string>("DecisionReason").HasMaxLength(1000).HasColumnType("nvarchar(1000)");
            entity.Property<int?>("StatusBeforeWithdrawal").HasColumnType("int");
            entity.Property<Guid?>("RelatedMissionId").HasColumnType("uniqueidentifier");
            entity.Property<string>("SplitReason").HasMaxLength(1000).HasColumnType("nvarchar(1000)");
            entity.Property<DateTimeOffset?>("SplitAtUtc").HasColumnType("datetimeoffset");
            entity.Property<string>("SplitBy").HasMaxLength(64).HasColumnType("nvarchar(64)");
            entity.HasKey("Id");
            entity.HasIndex("MissionNumber").IsUnique();
            entity.HasIndex("Status", "PlannedStartAtUtc", "PlannedEndAtUtc");
            entity.HasIndex("DriverId", "PlannedStartAtUtc", "PlannedEndAtUtc");
            entity.HasIndex("VehicleId", "PlannedStartAtUtc", "PlannedEndAtUtc");
            entity.HasIndex("RequesterUsername");
            entity.ToTable("Missions", table =>
            {
                table.HasCheckConstraint("CK_Missions_PlannedTimeRange", "[PlannedEndAtUtc] > [PlannedStartAtUtc]");
                table.HasCheckConstraint("CK_Missions_OdometerRange", "[OdometerStartKm] IS NULL OR [OdometerEndKm] IS NULL OR [OdometerEndKm] >= [OdometerStartKm]");
                table.HasCheckConstraint("CK_Missions_PassengerCount_NonNegative", "[PassengerCount] IS NULL OR [PassengerCount] >= 0");
            });
        });

        modelBuilder.Entity("FleetManagement.Domain.Missions.Mission", entity =>
        {
            entity.HasOne("FleetManagement.Domain.Drivers.Driver", null)
                .WithMany()
                .HasForeignKey("DriverId")
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_Missions_Drivers_DriverId");

            entity.HasOne("FleetManagement.Domain.Vehicles.Vehicle", null)
                .WithMany()
                .HasForeignKey("VehicleId")
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_Missions_Vehicles_VehicleId");
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

        modelBuilder.Entity("FleetManagement.Domain.Notifications.UserNotification", entity =>
        {
            entity.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uniqueidentifier");
            entity.Property<string>("RecipientUsername").IsRequired().HasMaxLength(64).HasColumnType("nvarchar(64)");
            entity.Property<string>("Title").IsRequired().HasMaxLength(200).HasColumnType("nvarchar(200)");
            entity.Property<string>("Message").IsRequired().HasMaxLength(1000).HasColumnType("nvarchar(1000)");
            entity.Property<string>("EventKey").IsRequired().HasMaxLength(80).HasColumnType("nvarchar(80)");
            entity.Property<string>("ReferenceType").HasMaxLength(100).HasColumnType("nvarchar(100)");
            entity.Property<string>("ReferenceId").HasMaxLength(100).HasColumnType("nvarchar(100)");
            entity.Property<DateTimeOffset>("CreatedAtUtc").HasColumnType("datetimeoffset");
            entity.Property<DateTimeOffset?>("ReadAtUtc").HasColumnType("datetimeoffset");
            entity.HasKey("Id");
            entity.HasIndex("RecipientUsername", "CreatedAtUtc");
            entity.HasIndex("RecipientUsername", "ReadAtUtc");
            entity.ToTable("UserNotifications");
        });

        modelBuilder.Entity("FleetManagement.Domain.Auditing.AuditEntry", entity =>
        {
            entity.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("uniqueidentifier");

            entity.Property<string>("ActorUsername")
                .IsRequired()
                .HasMaxLength(64)
                .HasColumnType("nvarchar(64)");

            entity.Property<string>("Action")
                .IsRequired()
                .HasMaxLength(80)
                .HasColumnType("nvarchar(80)");

            entity.Property<string>("EntityType")
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnType("nvarchar(100)");

            entity.Property<string>("EntityId")
                .HasMaxLength(100)
                .HasColumnType("nvarchar(100)");

            entity.Property<DateTimeOffset>("OccurredAtUtc")
                .HasColumnType("datetimeoffset");

            entity.Property<string>("Outcome")
                .IsRequired()
                .HasMaxLength(40)
                .HasColumnType("nvarchar(40)");

            entity.Property<string>("Reason")
                .HasMaxLength(1000)
                .HasColumnType("nvarchar(1000)");

            entity.Property<string>("BeforeJson")
                .HasMaxLength(12000)
                .HasColumnType("nvarchar(max)");

            entity.Property<string>("AfterJson")
                .HasMaxLength(12000)
                .HasColumnType("nvarchar(max)");

            entity.Property<string>("ClientIpAddress")
                .HasMaxLength(45)
                .HasColumnType("nvarchar(45)");

            entity.HasKey("Id");
            entity.HasIndex("OccurredAtUtc");
            entity.HasIndex("EntityType", "EntityId");
            entity.HasIndex("ActorUsername", "OccurredAtUtc");
            entity.HasIndex("Action", "Outcome");
            entity.ToTable("AuditEntries");
        });

        modelBuilder.Entity("FleetManagement.Domain.Notifications.SmsQueueItem", entity =>
        {
            entity.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uniqueidentifier");
            entity.Property<int>("EventType").HasColumnType("int");
            entity.Property<string>("RecipientName").IsRequired().HasMaxLength(200).HasColumnType("nvarchar(200)");
            entity.Property<string>("RecipientPhone").IsRequired().HasMaxLength(20).HasColumnType("nvarchar(20)");
            entity.Property<string>("Body").IsRequired().HasMaxLength(2000).HasColumnType("nvarchar(2000)");
            entity.Property<Guid>("MissionId").HasColumnType("uniqueidentifier");
            entity.Property<DateTimeOffset>("ScheduledAtUtc").HasColumnType("datetimeoffset");
            entity.Property<DateTimeOffset>("CreatedAtUtc").HasColumnType("datetimeoffset");
            entity.Property<int>("Status").HasColumnType("int");
            entity.Property<int>("AttemptCount").HasColumnType("int");
            entity.Property<int>("MaximumRetryCount").HasColumnType("int");
            entity.Property<int>("RetryIntervalSeconds").HasColumnType("int");
            entity.Property<DateTimeOffset?>("NextAttemptAtUtc").HasColumnType("datetimeoffset");
            entity.Property<DateTimeOffset?>("LastAttemptAtUtc").HasColumnType("datetimeoffset");
            entity.Property<DateTimeOffset?>("AcceptedAtUtc").HasColumnType("datetimeoffset");
            entity.Property<DateTimeOffset?>("DeliveredAtUtc").HasColumnType("datetimeoffset");
            entity.Property<string>("ProviderMessageId").HasMaxLength(100).HasColumnType("nvarchar(100)");
            entity.Property<string>("ProviderDeliveryStatus").HasMaxLength(100).HasColumnType("nvarchar(100)");
            entity.Property<string>("ErrorReason").HasMaxLength(1000).HasColumnType("nvarchar(1000)");
            entity.Property<int>("StatusPollCount").HasColumnType("int");
            entity.Property<int>("MaximumStatusPollingAttempts").HasColumnType("int");
            entity.Property<DateTimeOffset?>("NextStatusPollAtUtc").HasColumnType("datetimeoffset");
            entity.HasKey("Id");
            entity.HasIndex("MissionId", "CreatedAtUtc");
            entity.HasIndex("ProviderMessageId");
            entity.HasIndex("Status", "NextAttemptAtUtc");
            entity.HasIndex("Status", "NextStatusPollAtUtc");
            entity.HasIndex("Status", "ScheduledAtUtc");
            entity.ToTable("SmsQueueItems");
        });

        modelBuilder.Entity("FleetManagement.Domain.Notifications.SmsQueueItem", entity =>
        {
            entity.HasOne("FleetManagement.Domain.Missions.Mission", null)
                .WithMany()
                .HasForeignKey("MissionId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired()
                .HasConstraintName("FK_SmsQueueItems_Missions_MissionId");
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
