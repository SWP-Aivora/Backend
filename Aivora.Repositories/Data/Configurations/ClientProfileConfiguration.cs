using Aivora.Repositories.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aivora.Repositories.Data.Configurations;

public class ClientProfileConfiguration : IEntityTypeConfiguration<ClientProfile>
{
    public void Configure(EntityTypeBuilder<ClientProfile> builder)
    {
        builder.ToTable("ClientProfiles");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CompanyName).HasMaxLength(255);
        builder.Property(x => x.Industry).HasMaxLength(100);
        builder.Property(x => x.CompanySize).HasMaxLength(50);
        builder.Property(x => x.Website).HasMaxLength(2048);
        builder.Property(x => x.Description).HasMaxLength(2000);
    }
}
