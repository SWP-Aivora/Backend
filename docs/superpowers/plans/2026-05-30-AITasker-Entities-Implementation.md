# AITasker Entity Layer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create the core domain model (Entities and Enums) for the AITasker project in the `Aivora.Repositories` project, targeting PostgreSQL.

**Architecture:** Clean POCO entities inheriting from a common `BaseEntity`. Enums and Entities are separated into distinct folders and files.

**Tech Stack:** .NET 10, Entity Framework Core, PostgreSQL.

---

## File Structure

- `Aivora.Repositories/`
  - `BaseEntity.cs`: The base class for all entities.
  - `Enums/`: Folder for all enum definitions.
  - `Entities/`: Folder for all domain entity definitions.

---

### Task 1: Initialize BaseEntity and Enums

**Files:**
- Create: `Aivora.Repositories/BaseEntity.cs`
- Create: `Aivora.Repositories/Enums/UserRole.cs`
- Create: `Aivora.Repositories/Enums/UserStatus.cs`
- Create: `Aivora.Repositories/Enums/AvailabilityStatus.cs`
- Create: `Aivora.Repositories/Enums/SkillLevel.cs`
- Create: `Aivora.Repositories/Enums/JobStatus.cs`
- Create: `Aivora.Repositories/Enums/JobVisibility.cs`
- Create: `Aivora.Repositories/Enums/BudgetType.cs`
- Create: `Aivora.Repositories/Enums/AIJobSuggestionStatus.cs`
- Create: `Aivora.Repositories/Enums/ProposalStatus.cs`
- Create: `Aivora.Repositories/Enums/ProjectStatus.cs`
- Create: `Aivora.Repositories/Enums/MilestoneStatus.cs`
- Create: `Aivora.Repositories/Enums/PaymentStatus.cs`
- Create: `Aivora.Repositories/Enums/WalletTransactionType.cs`
- Create: `Aivora.Repositories/Enums/TransactionDirection.cs`
- Create: `Aivora.Repositories/Enums/DeliverableStatus.cs`
- Create: `Aivora.Repositories/Enums/DisputeStatus.cs`
- Create: `Aivora.Repositories/Enums/DisputeResolutionType.cs`
- Delete: `Aivora.Repositories/Class1.cs`

- [ ] **Step 1: Create BaseEntity.cs**

```csharp
namespace Aivora.Repositories;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
```

- [ ] **Step 2: Create all Enum files**
Create the following files in `Aivora.Repositories/Enums/`:

`UserRole.cs`:
```csharp
namespace Aivora.Repositories.Enums;
public enum UserRole { CLIENT, EXPERT, ADMIN }
```

`UserStatus.cs`:
```csharp
namespace Aivora.Repositories.Enums;
public enum UserStatus { PENDING, ACTIVE, SUSPENDED, DELETED }
```

*(Repeat for all enums listed in the spec...)*

- [ ] **Step 3: Delete placeholder Class1.cs**

Run: `rm Aivora.Repositories/Class1.cs`

- [ ] **Step 4: Verify Compilation**

Run: `dotnet build Aivora.Repositories/Aivora.Repositories.csproj`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add Aivora.Repositories/
git commit -m "feat: add BaseEntity and core Enums"
```

### Task 2: Create Identity Entities

**Files:**
- Create: `Aivora.Repositories/Entities/User.cs`
- Create: `Aivora.Repositories/Entities/ClientProfile.cs`
- Create: `Aivora.Repositories/Entities/ExpertProfile.cs`

- [ ] **Step 1: Create User.cs**

```csharp
using Aivora.Repositories.Enums;

namespace Aivora.Repositories.Entities;

public class User : BaseEntity
{
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string? AvatarUrl { get; set; }
    public string? Phone { get; set; }
    public UserRole Role { get; set; }
    public UserStatus Status { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }

    public virtual ClientProfile? ClientProfile { get; set; }
    public virtual ExpertProfile? ExpertProfile { get; set; }
    public virtual Wallet? Wallet { get; set; }
}
```

- [ ] **Step 2: Create ClientProfile.cs**

```csharp
namespace Aivora.Repositories.Entities;

public class ClientProfile : BaseEntity
{
    public Guid UserId { get; set; }
    public string? CompanyName { get; set; }
    public string? Industry { get; set; }
    public string? CompanySize { get; set; }
    public string? Website { get; set; }
    public string? Description { get; set; }

    public virtual User User { get; set; } = null!;
}
```

- [ ] **Step 3: Create ExpertProfile.cs**

```csharp
using Aivora.Repositories.Enums;

namespace Aivora.Repositories.Entities;

public class ExpertProfile : BaseEntity
{
    public Guid UserId { get; set; }
    public string? Title { get; set; }
    public string? Bio { get; set; }
    public decimal? HourlyRate { get; set; }
    public int ExperienceYears { get; set; }
    public AvailabilityStatus AvailabilityStatus { get; set; }
    public decimal RatingAvg { get; set; }
    public int CompletedProjects { get; set; }
    public decimal SuccessRate { get; set; }
    public int? ResponseTimeMinutes { get; set; }

    public virtual User User { get; set; } = null!;
    public virtual ICollection<ExpertSkill> ExpertSkills { get; set; } = new List<ExpertSkill>();
}
```

- [ ] **Step 4: Verify Compilation**

Run: `dotnet build Aivora.Repositories/Aivora.Repositories.csproj`

- [ ] **Step 5: Commit**

```bash
git add Aivora.Repositories/Entities/
git commit -m "feat: add Identity and Profile entities"
```

### Task 3: Create Taxonomy Entities

**Files:**
- Create: `Aivora.Repositories/Entities/Category.cs`
- Create: `Aivora.Repositories/Entities/Skill.cs`
- Create: `Aivora.Repositories/Entities/ExpertSkill.cs`

- [ ] **Step 1: Create Category.cs**

```csharp
namespace Aivora.Repositories.Entities;

public class Category : BaseEntity
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public Guid? ParentId { get; set; }

    public virtual Category? Parent { get; set; }
    public virtual ICollection<Category> SubCategories { get; set; } = new List<Category>();
    public virtual ICollection<Skill> Skills { get; set; } = new List<Skill>();
}
```

- [ ] **Step 2: Create Skill.cs**

```csharp
namespace Aivora.Repositories.Entities;

public class Skill : BaseEntity
{
    public string Name { get; set; } = null!;
    public Guid? CategoryId { get; set; }

    public virtual Category? Category { get; set; }
}
```

- [ ] **Step 3: Create ExpertSkill.cs**

```csharp
using Aivora.Repositories.Enums;

namespace Aivora.Repositories.Entities;

public class ExpertSkill : BaseEntity
{
    public Guid ExpertId { get; set; }
    public Guid SkillId { get; set; }
    public SkillLevel Level { get; set; }
    public int YearsExperience { get; set; }

    public virtual ExpertProfile Expert { get; set; } = null!;
    public virtual Skill Skill { get; set; } = null!;
}
```

- [ ] **Step 4: Commit**

```bash
git add Aivora.Repositories/Entities/
git commit -m "feat: add Taxonomy entities"
```

### Task 4: Create Job and Proposal Entities

**Files:**
- Create: `Aivora.Repositories/Entities/JobPost.cs`
- Create: `Aivora.Repositories/Entities/JobSkill.cs`
- Create: `Aivora.Repositories/Entities/AIJobSuggestion.cs`
- Create: `Aivora.Repositories/Entities/Proposal.cs`
- Create: `Aivora.Repositories/Entities/ProposalMilestone.cs`

*(Implementation details following the same pattern as Task 2/3...)*

### Task 5: Create Project and Milestone Entities

**Files:**
- Create: `Aivora.Repositories/Entities/Project.cs`
- Create: `Aivora.Repositories/Entities/Milestone.cs`
- Create: `Aivora.Repositories/Entities/Deliverable.cs`

### Task 6: Create Financial and Communication Entities

**Files:**
- Create: `Aivora.Repositories/Entities/Wallet.cs`
- Create: `Aivora.Repositories/Entities/Payment.cs`
- Create: `Aivora.Repositories/Entities/WalletTransaction.cs`
- Create: `Aivora.Repositories/Entities/Conversation.cs`
- Create: `Aivora.Repositories/Entities/Message.cs`
- Create: `Aivora.Repositories/Entities/Review.cs`
- Create: `Aivora.Repositories/Entities/Dispute.cs`
- Create: `Aivora.Repositories/Entities/DisputeEvidence.cs`
- Create: `Aivora.Repositories/Entities/RecommendationResult.cs`

- [ ] **Step 1: Implement remaining entities per design spec.**
- [ ] **Step 2: Final build check.**
- [ ] **Step 3: Final commit.**
