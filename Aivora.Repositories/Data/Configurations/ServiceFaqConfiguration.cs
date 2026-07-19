using Aivora.Repositories.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aivora.Repositories.Data.Configurations;

public class ServiceFaqConfiguration : IEntityTypeConfiguration<ServiceFaq>
{
    public void Configure(EntityTypeBuilder<ServiceFaq> builder)
    {
        builder.ToTable("ServiceFaqs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Question).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Answer).IsRequired().HasMaxLength(2000);
    }
}
