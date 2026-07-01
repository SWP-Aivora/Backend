using Aivora.Repositories.Abstractions;
using Aivora.Repositories.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aivora.Repositories.Data;

public class AivoraDbContext : DbContext
{
    public AivoraDbContext(DbContextOptions<AivoraDbContext> options) : base(options) { }

    // Identity
    public DbSet<User> Users => Set<User>();
    public DbSet<ClientProfile> ClientProfiles => Set<ClientProfile>();
    public DbSet<ExpertProfile> ExpertProfiles => Set<ExpertProfile>();
    public DbSet<ExpertVerification> ExpertVerifications => Set<ExpertVerification>();
    public DbSet<VerificationCertificate> VerificationCertificates => Set<VerificationCertificate>();

    // Taxonomy
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<ExpertSkill> ExpertSkills => Set<ExpertSkill>();

    // Jobs
    public DbSet<JobPost> JobPosts => Set<JobPost>();
    public DbSet<JobSkill> JobSkills => Set<JobSkill>();
    public DbSet<JobPostMilestone> JobPostMilestones => Set<JobPostMilestone>();
    public DbSet<AIJobSuggestion> AIJobSuggestions => Set<AIJobSuggestion>();

    // Proposals & Projects
    public DbSet<Proposal> Proposals => Set<Proposal>();
    public DbSet<ProposalMilestone> ProposalMilestones => Set<ProposalMilestone>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Milestone> Milestones => Set<Milestone>();
    public DbSet<Deliverable> Deliverables => Set<Deliverable>();

    // Finance
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();

    // Communication & Support
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Dispute> Disputes => Set<Dispute>();
    public DbSet<DisputeEvidence> DisputeEvidences => Set<DisputeEvidence>();
    public DbSet<RecommendationResult> RecommendationResults => Set<RecommendationResult>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.ConfigureWarnings(w =>
            w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AivoraDbContext).Assembly);

        // Global Query Filters for Soft Delete
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity<Guid>).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = System.Linq.Expressions.Expression.Parameter(entityType.ClrType, "e");
                var property = System.Linq.Expressions.Expression.Property(parameter, nameof(BaseEntity<Guid>.IsDeleted));
                var falseConstant = System.Linq.Expressions.Expression.Constant(false);
                var compare = System.Linq.Expressions.Expression.Equal(property, falseConstant);
                var filter = System.Linq.Expressions.Expression.Lambda(compare, parameter);

                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
            }
        }

        // Global Precision for Decimal
        foreach (var property in modelBuilder.Model.GetEntityTypes()
            .SelectMany(t => t.GetProperties())
            .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            property.SetPrecision(18);
            property.SetScale(2);
        }

        base.OnModelCreating(modelBuilder);
    }
}
