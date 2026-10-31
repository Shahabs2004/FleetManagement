using FleetManagement.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// نگاشت پروفایل کاربران سازمانی به SQL Server.
/// </summary>
public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    /// <summary>
    /// تعریف ستون‌ها، محدودیت‌ها و شاخص جست‌وجوی نام کاربری.
    /// </summary>
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("ApplicationUsers");

        builder.HasKey(user => user.Id);

        builder.Property(user => user.Username)
            .HasMaxLength(64)
            .UseCollation("Latin1_General_100_CI_AS")
            .IsRequired();

        builder.Property(user => user.FirstName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(user => user.LastName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(user => user.OrganizationalUnit)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(user => user.IsOrganizationalUnitManuallyOverridden)
            .IsRequired();

        builder.Property(user => user.DirectoryDistinguishedName)
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(user => user.IsDisabled)
            .IsRequired();

        builder.Property(user => user.CreatedAtUtc)
            .IsRequired();

        builder.Property(user => user.LastDirectorySyncAtUtc)
            .IsRequired();

        builder.HasIndex(user => user.Username)
            .IsUnique();

        builder.HasIndex(user => user.DirectoryDistinguishedName)
            .IsUnique();
    }
}
