using Aivora.Repositories.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aivora.Repositories.Data.Configurations;

public class ProposalConfiguration : IEntityTypeConfiguration<Proposal>
{
    public void Configure(EntityTypeBuilder<Proposal> builder)
    {
        builder.ToTable("Proposals");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CoverLetter).IsRequired();
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(10);
        builder.Property(x => x.Status).HasConversion<string>().IsRequired();

        // Relationships
        builder.HasOne(x => x.Expert)
            .WithMany()
            .HasForeignKey(x => x.ExpertId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Milestones)
            .WithOne(x => x.Proposal)
            .HasForeignKey(x => x.ProposalId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
