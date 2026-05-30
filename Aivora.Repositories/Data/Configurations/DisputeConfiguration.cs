using Aivora.Repositories.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aivora.Repositories.Data.Configurations;

public class DisputeConfiguration : IEntityTypeConfiguration<Dispute>
{
    public void Configure(EntityTypeBuilder<Dispute> builder)
    {
        builder.ToTable("Disputes");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Reason).IsRequired().HasMaxLength(255);
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.ResolutionNote).HasMaxLength(2000);
        builder.Property(x => x.Status).HasConversion<string>().IsRequired();
        builder.Property(x => x.ResolutionType).HasConversion<string>();

        // Relationships
        builder.HasOne(x => x.Project)
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Milestone)
            .WithMany()
            .HasForeignKey(x => x.MilestoneId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Payment)
            .WithMany()
            .HasForeignKey(x => x.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Opener)
            .WithMany()
            .HasForeignKey(x => x.OpenedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Admin)
            .WithMany()
            .HasForeignKey(x => x.AdminId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
