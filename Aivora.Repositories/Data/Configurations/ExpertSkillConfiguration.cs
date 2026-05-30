using Aivora.Repositories.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aivora.Repositories.Data.Configurations;

public class ExpertSkillConfiguration : IEntityTypeConfiguration<ExpertSkill>
{
    public void Configure(EntityTypeBuilder<ExpertSkill> builder)
    {
        builder.ToTable("ExpertSkills");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Level).HasConversion<string>().IsRequired();

        // Unique constraint for ExpertId and SkillId
        builder.HasIndex(x => new { x.ExpertId, x.SkillId }).IsUnique();

        // Relationships are handled on the other side or can be repeated here
        builder.HasOne(x => x.Skill)
            .WithMany()
            .HasForeignKey(x => x.SkillId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
