using Aivora.Repositories.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aivora.Repositories.Data.Configurations;

public class ServiceOfferMilestoneConfiguration : IEntityTypeConfiguration<ServiceOfferMilestone>
{
    public void Configure(EntityTypeBuilder<ServiceOfferMilestone> builder)
    {
        builder.ToTable("ServiceOfferMilestones");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title).IsRequired().HasMaxLength(255);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.AcceptanceCriteria).HasMaxLength(2000);
    }
}
