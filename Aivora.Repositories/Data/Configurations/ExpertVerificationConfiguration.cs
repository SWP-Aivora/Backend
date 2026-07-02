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

        builder.Property(x => x.EvidenceFileUrl).IsRequired().HasMaxLength(500);
        builder.Property(x => x.EvidencePublicId).IsRequired().HasMaxLength(255);
        builder.Property(x => x.Status).HasConversion<string>().IsRequired();
        builder.Property(x => x.AIReasoning).HasMaxLength(2000);
        builder.Property(x => x.AdminDecisionReason).HasMaxLength(1000);

        builder.HasOne(x => x.ExpertSkill)
            .WithMany()
            .HasForeignKey(x => x.ExpertSkillId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.AdminId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.ExpertSkillId, x.CreatedAt });
    }
}
