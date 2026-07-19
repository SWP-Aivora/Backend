using Aivora.Repositories.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aivora.Repositories.Data.Configurations;

public class ServiceOfferConfiguration : IEntityTypeConfiguration<ServiceOffer>
{
    public void Configure(EntityTypeBuilder<ServiceOffer> builder)
    {
        builder.ToTable("ServiceOffers");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status).HasConversion<string>().IsRequired();

        // Relationships
        builder.HasOne(x => x.ServiceRequest)
            .WithMany()
            .HasForeignKey(x => x.ServiceRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Expert)
            .WithMany()
            .HasForeignKey(x => x.ExpertId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Milestones)
            .WithOne(x => x.ServiceOffer)
            .HasForeignKey(x => x.ServiceOfferId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
