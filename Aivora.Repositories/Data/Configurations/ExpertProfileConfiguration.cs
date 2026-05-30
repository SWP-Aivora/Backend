using Aivora.Repositories.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aivora.Repositories.Data.Configurations;

public class ExpertProfileConfiguration : IEntityTypeConfiguration<ExpertProfile>
{
    public void Configure(EntityTypeBuilder<ExpertProfile> builder)
    {
        builder.ToTable("ExpertProfiles");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title).HasMaxLength(255);
        builder.Property(x => x.Bio).HasMaxLength(5000);
        builder.Property(x => x.AvailabilityStatus).HasConversion<string>().IsRequired();

        // Relationships
        builder.HasMany(x => x.ExpertSkills)
            .WithOne(x => x.Expert)
            .HasForeignKey(x => x.ExpertId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
