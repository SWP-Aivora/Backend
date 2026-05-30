using Aivora.Repositories.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aivora.Repositories.Data.Configurations;

public class DisputeEvidenceConfiguration : IEntityTypeConfiguration<DisputeEvidence>
{
    public void Configure(EntityTypeBuilder<DisputeEvidence> builder)
    {
        builder.ToTable("DisputeEvidences");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Content).HasMaxLength(4000);
        builder.Property(x => x.FileUrl).HasMaxLength(2048);

        // Relationships
        builder.HasOne(x => x.Dispute)
            .WithMany()
            .HasForeignKey(x => x.DisputeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.SubmittedByUser)
            .WithMany()
            .HasForeignKey(x => x.SubmittedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
