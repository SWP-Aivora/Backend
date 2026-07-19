using Aivora.Repositories.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aivora.Repositories.Data.Configurations;

public class ServiceRequestConfiguration : IEntityTypeConfiguration<ServiceRequest>
{
    public void Configure(EntityTypeBuilder<ServiceRequest> builder)
    {
        builder.ToTable("ServiceRequests");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Note).HasMaxLength(2000);
        builder.Property(x => x.Status).HasConversion<string>().IsRequired();
        builder.Property(x => x.PackageTitle).IsRequired().HasMaxLength(255);

        // Relationships
        builder.HasOne(x => x.Service)
            .WithMany()
            .HasForeignKey(x => x.ServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Client)
            .WithMany()
            .HasForeignKey(x => x.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Package)
            .WithMany()
            .HasForeignKey(x => x.PackageId)
            .OnDelete(DeleteBehavior.Restrict);

        // Backs the "no duplicate PENDING request for the same Service+Client" rule at the DB
        // level, closing the TOCTOU race the app-level AnyAsync check alone can't (two concurrent
        // requests could both pass the check before either commits).
        builder.HasIndex(x => new { x.ServiceId, x.ClientId })
            .HasFilter("\"IsDeleted\" = false AND \"Status\" = 'PENDING'")
            .IsUnique();
    }
}
