using Aivora.Repositories.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aivora.Repositories.Data.Configurations;

public class JobPostMilestoneConfiguration : IEntityTypeConfiguration<JobPostMilestone>
{
    public void Configure(EntityTypeBuilder<JobPostMilestone> builder)
    {
        builder.ToTable("JobPostMilestones");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title).IsRequired().HasMaxLength(255);
        builder.Property(x => x.Description).HasMaxLength(1000);

        builder.HasOne(x => x.JobPost)
            .WithMany(j => j.Milestones)
            .HasForeignKey(x => x.JobPostId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
