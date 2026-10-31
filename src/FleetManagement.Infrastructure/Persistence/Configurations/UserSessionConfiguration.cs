using FleetManagement.Domain.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetManagement.Infrastructure.Persistence.Configurations;

/// <summary>
/// نگاشت سابقه نشست‌ها و تلاش‌های ورود به SQL Server.
/// </summary>
public sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    /// <summary>
    /// تعریف ستون‌ها و شاخص‌های جست‌وجوی سوابق نشست.
    /// </summary>
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("UserSessions");

        builder.HasKey(session => session.Id);

        builder.Property(session => session.Username)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(session => session.LoginMethod)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(session => session.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(session => session.LoginAtUtc).IsRequired();
        builder.Property(session => session.LastActivityAtUtc).IsRequired();
        builder.Property(session => session.EndedAtUtc);

        builder.Property(session => session.ClientIpAddress)
            .HasMaxLength(45);

        builder.Property(session => session.Reason)
            .HasMaxLength(300);

        builder.HasIndex(session => session.Username);
        builder.HasIndex(session => session.LoginAtUtc);
        builder.HasIndex(session => new { session.Status, session.LastActivityAtUtc });
    }
}
