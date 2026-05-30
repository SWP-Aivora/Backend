using Aivora.Repositories.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aivora.Repositories.Data.Configurations;

public class DeliverableConfiguration : IEntityTypeConfiguration<Deliverable>
{
    public void Configure(EntityTypeBuilder<Deliverable> builder)
    {
        builder.ToTable("Deliverables");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.FileUrl).HasMaxLength(2048);
        builder.Property(x => x.DemoUrl).HasMaxLength(2048);
        builder.Property(x => x.SourceCodeUrl).HasMaxLength(2048);
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.Property(x => x.Status).HasConversion<string>().IsRequired();

        // Relationships
        builder.HasOne(x => x.Expert)
            .WithMany()
            .HasForeignKey(x => x.ExpertId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
