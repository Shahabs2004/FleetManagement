using FleetManagement.Domain.Missions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetManagement.Infrastructure.Persistence.Configurations;

/// <summary>نگاشت درخواست‌های تغییر مأموریت.</summary>
public sealed class MissionChangeRequestConfiguration : IEntityTypeConfiguration<MissionChangeRequest>
{
    /// <summary>تعریف فیلدها، رابطه مأموریت و شاخص‌های بررسی.</summary>
    public void Configure(EntityTypeBuilder<MissionChangeRequest> builder)
    {
        builder.ToTable("MissionChangeRequests");
        builder.HasKey(request => request.Id);
        builder.Property(request => request.RequesterUsername).HasMaxLength(64).IsRequired();
        builder.Property(request => request.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(request => request.ProposedValuesJson).HasMaxLength(12000).IsRequired();
        builder.Property(request => request.Status).HasConversion<int>().IsRequired();
        builder.Property(request => request.ReviewerUsername).HasMaxLength(64);
        builder.Property(request => request.DecisionReason).HasMaxLength(1000);
        builder.Property(request => request.CreatedAtUtc).IsRequired();

        builder.HasOne<Mission>()
            .WithMany()
            .HasForeignKey(request => request.MissionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(request => new { request.MissionId, request.Status });
        builder.HasIndex(request => new { request.RequesterUsername, request.CreatedAtUtc });
    }
}
