using Aivora.Repositories.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aivora.Repositories.Data.Configurations;

public class ProposalMilestoneConfiguration : IEntityTypeConfiguration<ProposalMilestone>
{
    public void Configure(EntityTypeBuilder<ProposalMilestone> builder)
    {
        builder.ToTable("ProposalMilestones");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title).IsRequired().HasMaxLength(255);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.AcceptanceCriteria).HasMaxLength(2000);
    }
}
