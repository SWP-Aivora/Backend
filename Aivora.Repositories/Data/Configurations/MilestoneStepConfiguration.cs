using Aivora.Repositories.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aivora.Repositories.Data.Configurations;

public class MilestoneStepConfiguration : IEntityTypeConfiguration<MilestoneStep>
{
    public void Configure(EntityTypeBuilder<MilestoneStep> builder)
    {
        builder.ToTable("MilestoneSteps");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title).IsRequired().HasMaxLength(255);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.Status).HasConversion<string>().IsRequired();
        builder.Property(x => x.BlockedReason).HasMaxLength(1000);

        // Relationships
        builder.HasOne(x => x.Milestone)
            .WithMany(x => x.Steps)
            .HasForeignKey(x => x.MilestoneId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.CompletedByUser)
            .WithMany()
            .HasForeignKey(x => x.CompletedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
