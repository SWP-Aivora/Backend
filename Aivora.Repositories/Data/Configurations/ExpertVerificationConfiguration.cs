using Aivora.Repositories.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aivora.Repositories.Data.Configurations;

public class ExpertVerificationConfiguration : IEntityTypeConfiguration<ExpertVerification>
{
    public void Configure(EntityTypeBuilder<ExpertVerification> builder)
    {
        builder.ToTable("ExpertVerifications");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status).HasConversion<string>().IsRequired();
        builder.Property(x => x.Feedback).HasMaxLength(2000);
        builder.Property(x => x.FailureReason).HasMaxLength(1000);
        builder.Property(x => x.AiProcessingId).HasMaxLength(255);
        builder.Property(x => x.AppealReason).HasMaxLength(1000);
        builder.Property(x => x.AppealAdminFeedback).HasMaxLength(1000);
        builder.Property(x => x.ProcessingStatus).HasMaxLength(50);

        // Score validation (will be handled in application layer)
        builder.Property(x => x.TotalScore).IsRequired();
        builder.Property(x => x.ProfileScore).IsRequired();
        builder.Property(x => x.SkillsScore).IsRequired();
        builder.Property(x => x.CertificatesScore).IsRequired();

        // Weight validation
        builder.Property(x => x.ProfileWeight).IsRequired();
        builder.Property(x => x.SkillsWeight).IsRequired();
        builder.Property(x => x.CertificatesWeight).IsRequired();

        // Relationship with ExpertProfile
        builder.HasOne(x => x.Expert)
            .WithMany()
            .HasForeignKey(x => x.ExpertId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for performance
        builder.HasIndex(x => x.ExpertId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.AiProcessingId);
        builder.HasIndex(x => x.LastProcessedAt);
    }
}