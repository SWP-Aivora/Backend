# AITasker DbContext Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the database access layer using EF Core and PostgreSQL.

**Architecture:** 
- Modular entity configurations using `IEntityTypeConfiguration<T>`.
- Automated auditing and soft delete handling via a `SaveChangesInterceptor`.
- Global query filters for soft delete.
- Pluralized table naming convention.

**Tech Stack:** .NET 10, EF Core, PostgreSQL.

---

## File Structure

- `Aivora.Repositories/`
  - `Data/`
    - `AivoraDbContext.cs`
    - `Interceptors/`
      - `AuditableEntityInterceptor.cs`
    - `Configurations/`
      - (23 individual configuration files)

---

### Task 1: Setup NuGet Packages and Folders

**Files:**
- Modify: `Aivora.Repositories/Aivora.Repositories.csproj`

- [ ] **Step 1: Add NuGet Packages**

Run:
```bash
dotnet add Aivora.Repositories/Aivora.Repositories.csproj package Microsoft.EntityFrameworkCore
dotnet add Aivora.Repositories/Aivora.Repositories.csproj package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add Aivora.Repositories/Aivora.Repositories.csproj package Microsoft.EntityFrameworkCore.Design
```

- [ ] **Step 2: Create Folders**

Run:
```bash
mkdir Aivora.Repositories/Data
mkdir Aivora.Repositories/Data/Interceptors
mkdir Aivora.Repositories/Data/Configurations
```

- [ ] **Step 3: Commit**

```bash
git add Aivora.Repositories/Aivora.Repositories.csproj
git commit -m "chore: add EF Core and PostgreSQL NuGet packages"
```

### Task 2: Implement AuditableEntityInterceptor

**Files:**
- Create: `Aivora.Repositories/Data/Interceptors/AuditableEntityInterceptor.cs`

- [ ] **Step 1: Implement the Interceptor**

```csharp
using Aivora.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Aivora.Repositories.Data.Interceptors;

public sealed class AuditableEntityInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateEntities(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void UpdateEntities(DbContext? context)
    {
        if (context == null) return;

        var entries = context.ChangeTracker.Entries<IAuditableEntity>();

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = DateTimeOffset.UtcNow;
                entry.Entity.UpdatedAt = DateTimeOffset.UtcNow;
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTimeOffset.UtcNow;
            }

            if (entry.State == EntityState.Deleted)
            {
                // Soft Delete check
                if (entry.Entity is BaseEntity<Guid> baseEntity)
                {
                    entry.State = EntityState.Modified;
                    baseEntity.IsDeleted = true;
                    entry.Entity.UpdatedAt = DateTimeOffset.UtcNow;
                }
            }
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Aivora.Repositories/Data/Interceptors/
git commit -m "feat: implement AuditableEntityInterceptor for auditing and soft delete"
```

### Task 3: Implement AivoraDbContext

**Files:**
- Create: `Aivora.Repositories/Data/AivoraDbContext.cs`

- [ ] **Step 1: Implement AivoraDbContext**

```csharp
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

    // Taxonomy
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<ExpertSkill> ExpertSkills => Set<ExpertSkill>();

    // Jobs
    public DbSet<JobPost> JobPosts => Set<JobPost>();
    public DbSet<JobSkill> JobSkills => Set<JobSkill>();
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
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Dispute> Disputes => Set<Dispute>();
    public DbSet<DisputeEvidence> DisputeEvidences => Set<DisputeEvidence>();
    public DbSet<RecommendationResult> RecommendationResults => Set<RecommendationResult>();

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
```

- [ ] **Step 2: Commit**

```bash
git add Aivora.Repositories/Data/AivoraDbContext.cs
git commit -m "feat: implement AivoraDbContext with DbSets and global filters"
```

### Task 4: Implement Entity Configurations

**Files:**
- Create: `Aivora.Repositories/Data/Configurations/*.cs` (23 files)

- [ ] **Step 1: Implement configurations for all entities.**
Each configuration should follow this pattern:
```csharp
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users"); // Plural
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.Email).IsRequired().HasMaxLength(255);
        builder.HasIndex(x => x.Email).IsUnique();

        builder.Property(x => x.Role).HasConversion<string>();
        builder.Property(x => x.Status).HasConversion<string>();
    }
}
```

- [ ] **Step 2: Commit in batches.**

### Task 5: Register Services in Program.cs

**Files:**
- Modify: `Aivora.api/Program.cs`
- Modify: `Aivora.api/Aivora.api.csproj`

- [ ] **Step 1: Add project reference**

Run: `dotnet add Aivora.api/Aivora.api.csproj reference Aivora.Repositories/Aivora.Repositories.csproj`

- [ ] **Step 2: Register DbContext and Interceptor**

```csharp
// Program.cs
builder.Services.AddSingleton<AuditableEntityInterceptor>();

builder.Services.AddDbContext<AivoraDbContext>((sp, options) => {
    var interceptor = sp.GetRequiredService<AuditableEntityInterceptor>();
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
           .AddInterceptors(interceptor);
});
```

- [ ] **Step 3: Verify Build**

Run: `dotnet build`

- [ ] **Step 4: Commit**

```bash
git add .
git commit -m "feat: register DbContext and Interceptor in API project"
```
