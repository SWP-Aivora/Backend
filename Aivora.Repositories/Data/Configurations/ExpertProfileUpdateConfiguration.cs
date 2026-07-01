using Aivora.Repositories.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aivora.Repositories.Data.Configurations;

public class ExpertProfileUpdateConfiguration : IEntityTypeConfiguration<ExpertProfileUpdate>
{
    public void Configure(EntityTypeBuilder<ExpertProfileUpdate> builder)
    {
        builder.ToTable("ExpertProfileUpdates");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Title).HasMaxLength(255);
        builder.Property(e => e.Bio).HasMaxLength(2000);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.RejectionReason).HasMaxLength(1000);

        builder.HasOne(e => e.ExpertProfile)
            .WithMany()
            .HasForeignKey(e => e.ExpertProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Admin)
            .WithMany()
            .HasForeignKey(e => e.AdminId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
