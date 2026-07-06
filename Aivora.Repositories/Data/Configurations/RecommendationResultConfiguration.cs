using Aivora.Repositories.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aivora.Repositories.Data.Configurations;

public class RecommendationResultConfiguration : IEntityTypeConfiguration<RecommendationResult>
{
    public void Configure(EntityTypeBuilder<RecommendationResult> builder)
    {
        builder.ToTable("RecommendationResults");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Explanation).HasMaxLength(2000);
        builder.Property(x => x.OverdueRate).HasPrecision(18, 4);
        builder.Property(x => x.OverduePenalty).HasPrecision(18, 4);
        builder.Property(x => x.DisputeRate).HasPrecision(18, 4);
        builder.Property(x => x.DisputePenalty).HasPrecision(18, 4);

        // Relationships
        builder.HasOne(x => x.Job)
            .WithMany(x => x.RecommendationResults)
            .HasForeignKey(x => x.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Expert)
            .WithMany()
            .HasForeignKey(x => x.ExpertId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
