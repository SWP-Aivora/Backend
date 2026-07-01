using Aivora.Repositories.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aivora.Repositories.Data.Configurations;

public class VerificationCertificateConfiguration : IEntityTypeConfiguration<VerificationCertificate>
{
    public void Configure(EntityTypeBuilder<VerificationCertificate> builder)
    {
        builder.ToTable("VerificationCertificates");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CertificateName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.IssuingOrganization).HasMaxLength(255).IsRequired();
        builder.Property(x => x.CertificateUrl).HasMaxLength(2048).IsRequired();
        builder.Property(x => x.VerificationNotes).HasMaxLength(1000);
        builder.Property(x => x.Score).IsRequired();
        builder.Property(x => x.IsVerified).IsRequired();

        // Relationship with ExpertProfile
        builder.HasOne(x => x.Expert)
            .WithMany(x => x.VerificationCertificates)
            .HasForeignKey(x => x.ExpertId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(x => x.ExpertId);
        builder.HasIndex(x => x.IsVerified);
        builder.HasIndex(x => x.IssuingOrganization);
        builder.HasIndex(x => x.ExpiryDate);
    }
}