using Aivora.Repositories.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aivora.Repositories.Data.Configurations;

public class MilestoneConfiguration : IEntityTypeConfiguration<Milestone>
{
    public void Configure(EntityTypeBuilder<Milestone> builder)
    {
        builder.ToTable("Milestones");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title).IsRequired().HasMaxLength(255);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.AcceptanceCriteria).HasMaxLength(2000);
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(10);
        builder.Property(x => x.Status).HasConversion<string>().IsRequired().IsConcurrencyToken();

        // Relationships
        builder.HasMany(x => x.Deliverables)
            .WithOne(x => x.Milestone)
            .HasForeignKey(x => x.MilestoneId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
