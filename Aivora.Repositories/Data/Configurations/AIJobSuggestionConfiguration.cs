using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aivora.Repositories.Data.Configurations;

public class AIJobSuggestionConfiguration : IEntityTypeConfiguration<AIJobSuggestion>
{
    public void Configure(EntityTypeBuilder<AIJobSuggestion> builder)
    {
        builder.ToTable("AIJobSuggestions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RawInput).IsRequired();
        builder.Property(x => x.SuggestedTitle).HasMaxLength(255);
        builder.Property(x => x.SuggestedBudgetType)
            .HasConversion<string>()
            .IsRequired()
            .HasDefaultValue(BudgetType.FIXED);
        builder.Property(x => x.Currency)
            .HasMaxLength(10)
            .IsRequired()
            .HasDefaultValue("AICOIN");
        builder.Property(x => x.SuggestedExperienceLevel).HasConversion<string>();
        builder.Property(x => x.SuggestedBusinessDomain).HasMaxLength(255);
        builder.Property(x => x.SuggestedExpectedOutcome).HasMaxLength(1000);
        builder.Property(x => x.ClarifyingAnswersJson);
        builder.Property(x => x.RejectionReason).HasMaxLength(500);
        builder.Property(x => x.AIModel).HasMaxLength(100);
        builder.Property(x => x.Status).HasConversion<string>().IsRequired();

        // Relationships
        builder.HasOne(x => x.Client)
            .WithMany()
            .HasForeignKey(x => x.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Job)
            .WithMany()
            .HasForeignKey(x => x.JobId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
