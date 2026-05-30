# AITasker DbContext & Configuration Design Spec

**Date:** 2026-05-30  
**Project:** Aivora (AITasker)  
**Status:** Ready for Implementation

## 1. Overview
This spec defines the implementation of the `AivoraDbContext`, entity configurations, and automated auditing logic for the AITasker backend.

## 2. Technical Stack
- **Database:** PostgreSQL
- **ORM:** Entity Framework Core (EF Core)
- **NuGet Packages Needed:** 
  - `Microsoft.EntityFrameworkCore`
  - `Npgsql.EntityFrameworkCore.PostgreSQL`
  - `Microsoft.EntityFrameworkCore.Relational`
  - `Microsoft.EntityFrameworkCore.Design` (For migrations)

## 3. Directory Structure
- `Aivora.Repositories/`
  - `Data/`
    - `AivoraDbContext.cs`: Primary database context.
    - `Configurations/`: Entity-specific mapping files implementing `IEntityTypeConfiguration<T>`.
    - `Interceptors/`: EF Core interceptors (e.g., `AuditableEntityInterceptor.cs`).

## 4. Design Decisions

### 4.1 Automated Auditing & Soft Delete
- **Mechanism:** `AuditableEntityInterceptor` inheriting from `SaveChangesInterceptor`.
- **Logic:**
  - On `EntityState.Added`: 
    - Set `CreatedAt` to `DateTimeOffset.UtcNow`.
    - Set `UpdatedAt` to `DateTimeOffset.UtcNow`.
  - On `EntityState.Modified`: 
    - **Do NOT** modify `CreatedAt`.
    - Set `UpdatedAt` to `DateTimeOffset.UtcNow`.
  - On `EntityState.Deleted`:
    - If the entity inherits from `BaseEntity<TKey>`:
      - Set `IsDeleted = true`.
      - Change `EntityState` to `Modified`.
      - Set `UpdatedAt` to `DateTimeOffset.UtcNow`.

### 4.2 Entity Mappings
- **Pattern:** `IEntityTypeConfiguration<T>` for each entity.
- **Table Naming Convention:** All tables will use **Pluralized** names (e.g., `Users`, `JobPosts`, `ExpertProfiles`).
- **Enum Mapping:** All enums will be stored as **strings** in the database using `HasConversion<string>()`.
- **Money Fields:** All `decimal` properties will default to `precision: 18, scale: 2`.

### 4.3 Global Query Filters
- **Soft Delete:** A global query filter `builder.Entity<T>().HasQueryFilter(e => !e.IsDeleted)` will be applied to all entities inheriting from `BaseEntity<TKey>`.

## 5. Implementation Details

### 5.1 DbSets to include in AivoraDbContext
- `Users`
- `ClientProfiles`
- `ExpertProfiles`
- `Categories`
- `Skills`
- `ExpertSkills`
- `JobPosts`
- `JobSkills`
- `AIJobSuggestions`
- `Proposals`
- `ProposalMilestones`
- `Projects`
- `Milestones`
- `Deliverables`
- `Wallets`
- `Payments`
- `WalletTransactions`
- `Conversations`
- `Messages`
- `Reviews`
- `Disputes`
- `DisputeEvidences`
- `RecommendationResults`

### 5.2 DbContext Configuration
In `AivoraDbContext.OnModelCreating`:
- Call `modelBuilder.ApplyConfigurationsFromAssembly(typeof(AivoraDbContext).Assembly)`.
- Use a loop to find all entities inheriting from `BaseEntity<TKey>` and apply the `IsDeleted` query filter.
- Use a loop to find all `decimal` properties and set their precision to `(18, 2)`.

### 5.3 Dependency Injection
The `AivoraDbContext` and the `AuditableEntityInterceptor` must be registered in the `Aivora.api` project's `Program.cs`.

## 6. Self-Review Notes
- [x] **Spec coverage:** Covers DbContext, configurations, interceptors, and global filters.
- [x] **Placeholder scan:** No placeholders.
- [x] **Type consistency:** Matches the `DateTimeOffset` and `Guid` types from the Entity Spec.
- [x] **Scope check:** Focused on the data access infrastructure.
- [x] **Ambiguity check:** Table naming unified to plural. EF Design package added.
