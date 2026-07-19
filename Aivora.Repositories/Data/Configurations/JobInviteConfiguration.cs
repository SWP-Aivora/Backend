using Aivora.Repositories.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aivora.Repositories.Data.Configurations;

public class JobInviteConfiguration : IEntityTypeConfiguration<JobInvite>
{
    public void Configure(EntityTypeBuilder<JobInvite> builder)
    {
        builder.ToTable("JobInvites");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status).HasConversion<string>().IsRequired();

        // Relationships
        builder.HasOne(x => x.Job)
            .WithMany()
            .HasForeignKey(x => x.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Expert)
            .WithMany()
            .HasForeignKey(x => x.ExpertId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Client)
            .WithMany()
            .HasForeignKey(x => x.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        // Backs the "no duplicate invite for the same Job+Expert" rule at the DB level,
        // closing the TOCTOU race the app-level AnyAsync check alone can't.
        builder.HasIndex(x => new { x.JobId, x.ExpertId })
            .HasFilter("\"IsDeleted\" = false")
            .IsUnique();
    }
}
