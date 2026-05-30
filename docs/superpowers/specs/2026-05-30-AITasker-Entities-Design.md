# AITasker Entity Layer Design Spec

**Date:** 2026-05-30  
**Project:** Aivora (AITasker)  
**Status:** Ready for Entity Implementation

## 1. Overview
This spec defines the entity layer for the AITasker backend. The entities represent the core domain models and are designed for Entity Framework Core targeting a **PostgreSQL** database.

## 2. Architecture & Patterns
- **Location:** `Aivora.Repositories/Entities/` and `Aivora.Repositories/Enums/`.
- **Naming:** PascalCase for C# classes and properties.
- **Approach:** Plain POCOs with Guid (UUID) as Primary Keys.
- **Primary Keys:** All IDs are `Guid`, mapping to PostgreSQL `uuid`.
- **Currency:** Default currency is `"AICOIN"`. Money fields use `decimal(18,2)`.
- **Timestamps:** Use `DateTimeOffset` for PostgreSQL compatibility.
- **Enum Storage:** Enums should be stored as **strings** in the database for readability and easier maintenance.
- **Inheritance:** All entities inherit `Id`, `CreatedAt`, and `UpdatedAt` from `BaseEntity`.

## 3. Base Entity
```csharp
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
```
*Note: `UpdatedAt` should be updated via an EF Core Interceptor or by overriding `SaveChangesAsync` in the `DbContext`.*

## 4. Enums
Each enum must be in its own file in `Aivora.Repositories.Enums`.

- `UserRole`: `CLIENT`, `EXPERT`, `ADMIN`
- `UserStatus`: `PENDING`, `ACTIVE`, `SUSPENDED`, `DELETED`
- `AvailabilityStatus`: `AVAILABLE`, `BUSY`, `UNAVAILABLE`
- `SkillLevel`: `BEGINNER`, `INTERMEDIATE`, `ADVANCED`, `EXPERT`
- `JobStatus`: `DRAFT`, `OPEN`, `IN_PROGRESS`, `COMPLETED`, `CANCELLED`, `CLOSED`
- `JobVisibility`: `PUBLIC`, `PRIVATE`, `INVITED_ONLY`
- `BudgetType`: `FIXED`, `HOURLY`
- `AIJobSuggestionStatus`: `GENERATED`, `ACCEPTED`, `REJECTED`, `FAILED`
- `ProposalStatus`: `SUBMITTED`, `SHORTLISTED`, `ACCEPTED`, `REJECTED`, `WITHDRAWN`
- `ProjectStatus`: `PENDING_PAYMENT`, `ACTIVE`, `IN_REVIEW`, `DISPUTED`, `COMPLETED`, `CANCELLED`
- `MilestoneStatus`: `CREATED`, `FUNDED`, `IN_PROGRESS`, `SUBMITTED`, `REVISION_REQUESTED`, `APPROVED`, `DISPUTED`, `PAID`, `REFUNDED`
- `PaymentStatus`: `PENDING`, `HELD`, `RELEASED`, `REFUNDED`, `FROZEN`, `FAILED`, `PARTIALLY_RELEASED`
- `WalletTransactionType`: `DEMO_DEPOSIT`, `ESCROW_HOLD`, `PAYMENT_RELEASE`, `REFUND`, `WITHDRAWAL_REQUEST`, `WITHDRAWAL_COMPLETED`
- `TransactionDirection`: `CREDIT`, `DEBIT`
- `DeliverableStatus`: `SUBMITTED`, `APPROVED`, `REVISION_REQUESTED`, `REJECTED`
- `DisputeStatus`: `OPEN`, `UNDER_REVIEW`, `RESOLVED`, `CLOSED`
- `DisputeResolutionType`: `RELEASE_TO_EXPERT`, `REFUND_TO_CLIENT`, `SPLIT_PAYMENT`, `REQUEST_REVISION`

## 5. Entities (Property Detail)

### 5.1 Identity
- **User**: `Email` (unique), `PasswordHash`, `FullName`, `AvatarUrl`, `Phone`, `Role`, `Status`, `LastLoginAt`.
- **ClientProfile**: `UserId` (FK), `CompanyName`, `Industry`, `CompanySize`, `Website`, `Description`.
- **ExpertProfile**: `UserId` (FK), `Title`, `Bio`, `HourlyRate` (decimal), `ExperienceYears`, `AvailabilityStatus` (Enum), `RatingAvg` (decimal), `CompletedProjects`, `SuccessRate` (decimal), `ResponseTimeMinutes`.

### 5.2 Taxonomy
- **Category**: `Name` (unique), `Description`, `ParentId` (FK).
- **Skill**: `Name` (unique), `CategoryId` (FK).
- **ExpertSkill**: `ExpertId` (FK), `SkillId` (FK), `Level` (Enum), `YearsExperience`.

### 5.3 Jobs & AI
- **JobPost**: `ClientId` (FK), `CategoryId` (FK), `Title`, `OriginalDescription`, `FinalDescription`, `BusinessDomain`, `ExpectedOutcome`, `BudgetType` (Enum), `BudgetMin` (decimal), `BudgetMax` (decimal), `Currency` (string, default "AICOIN"), `TimelineDays`, `Deadline` (DateOnly), `ExperienceLevel`, `Status` (Enum), `Visibility` (Enum), `PublishedAt`.
- **JobSkill**: `JobId` (FK), `SkillId` (FK), `IsRequired`.
- **AIJobSuggestion**: `JobId` (FK, nullable), `ClientId` (FK), `RawInput`, `SuggestedTitle`, `SuggestedDescription`, `SuggestedBudgetMin`, `SuggestedBudgetMax`, `SuggestedTimelineDays`, `SuggestedSkillsJson`, `SuggestedMilestonesJson`, `ClarifyingQuestionsJson`, `RiskWarningsJson`, `AIModel`, `Status` (Enum).

### 5.4 Proposals & Projects
- **Proposal**: `JobId` (FK), `ExpertId` (FK), `CoverLetter`, `ProposedBudget` (decimal), `ProposedTimelineDays`, `Currency` (default "AICOIN"), `Status` (Enum), `WithdrawnAt`.
- **ProposalMilestone**: `ProposalId` (FK), `Title`, `Description`, `Amount` (decimal), `DueDays`, `AcceptanceCriteria`, `OrderIndex`.
- **Project**: `JobId` (FK, unique), `AcceptedProposalId` (FK, unique), `ClientId` (FK), `ExpertId` (FK), `Title`, `Description`, `TotalBudget` (decimal), `Currency` (default "AICOIN"), `Status` (Enum), `StartDate` (DateOnly), `EndDate` (DateOnly), `CompletedAt`, `CancelledAt`.

### 5.5 Milestones & Deliverables
- **Milestone**: `ProjectId` (FK), `Title`, `Description`, `AcceptanceCriteria`, `Amount` (decimal), `Currency` (default "AICOIN"), `DueDate` (DateOnly), `OrderIndex`, `Status` (Enum), `FundedAt`, `SubmittedAt`, `ApprovedAt`, `PaidAt`.
- **Deliverable**: `MilestoneId` (FK), `ExpertId` (FK), `Description`, `FileUrl`, `DemoUrl`, `SourceCodeUrl`, `Note`, `RevisionNumber`, `Status` (Enum), `ReviewedAt`.

### 5.6 Finance
- **Wallet**: `UserId` (FK, unique), `AvailableBalance` (decimal), `HeldBalance` (decimal), `TotalEarned` (decimal), `Currency` (default "AICOIN").
- **Payment**: `ProjectId` (FK), `MilestoneId` (FK, unique), `PayerId` (FK), `PayeeId` (FK), `Amount` (decimal), `Currency` (default "AICOIN"), `Status` (Enum), `HeldAt`, `ReleasedAt`, `RefundedAt`, `FrozenAt`.
- **WalletTransaction**: `WalletId` (FK), `PaymentId` (FK, nullable), `UserId` (FK), `Type` (Enum), `Direction` (Enum), `Amount` (decimal), `BalanceBefore` (decimal), `BalanceAfter` (decimal), `Description`.

### 5.7 Communication & Feedback
- **Conversation**: `ProjectId` (FK, nullable), `JobId` (FK, nullable), `ClientId` (FK), `ExpertId` (FK).
- **Message**: `ConversationId` (FK), `SenderId` (FK), `Content`, `AttachmentUrl`, `IsRead`, `ReadAt`.
- **Review**: `ProjectId` (FK), `ReviewerId` (FK), `RevieweeId` (FK), `Rating` (1-5), `Comment`, `CommunicationRating`, `QualityRating`, `DeadlineRating`, `RequirementClarityRating`.
- **Dispute**: `ProjectId` (FK), `MilestoneId` (FK), `PaymentId` (FK), `OpenedBy` (FK), `AgainstUserId` (FK), `Reason`, `Description`, `Status` (Enum), `AdminId` (FK, nullable), `ResolutionType` (Enum, nullable), `ResolutionNote`, `ResolvedAt`.
- **DisputeEvidence**: `DisputeId` (FK), `SubmittedBy` (FK), `Content`, `FileUrl`.
- **RecommendationResult**: `JobId` (FK), `ExpertId` (FK), `TotalScore` (decimal), `SkillScore` (decimal), `PortfolioScore` (decimal), `RatingScore` (decimal), `BudgetScore` (decimal), `AvailabilityScore` (decimal), `CompletionScore` (decimal), `Explanation`.

## 6. Constraints & Indexes
- **User**: `Email` (unique).
- **ClientProfile**: `UserId` (unique).
- **ExpertProfile**: `UserId` (unique).
- **Category**: `Name` (unique).
- **Skill**: `Name` (unique).
- **ExpertSkill**: Unique index on (`ExpertId`, `SkillId`).
- **JobSkill**: Unique index on (`JobId`, `SkillId`).
- **Proposal**: Unique index on (`JobId`, `ExpertId`).
- **Project**: `JobId` (unique), `AcceptedProposalId` (unique).
- **Payment**: `MilestoneId` (unique).
- **Review**: Unique index on (`ProjectId`, `ReviewerId`, `RevieweeId`).
- **RecommendationResult**: Unique index on (`JobId`, `ExpertId`).

## 7. Implementation Notes
- **Navigation Properties**: Each FK should have a corresponding navigation property. Use `virtual` only if EF Core lazy loading proxies are enabled.
- **Collections**: Use `ICollection<T>` for one-to-many relationships (e.g., `public virtual ICollection<Proposal> Proposals { get; set; } = new List<Proposal>();`).
- **Required Fields**: Use `null!` (null-forgiving operator) for required string properties.
- **Precision**: Set `HasPrecision(18, 2)` for all decimal fields in the `DbContext`.

## 8. Self-Review Notes
- [x] **Spec coverage:** Comprehensive mapping of all core and support entities.
- [x] **Placeholder scan:** No placeholders.
- [x] **Type consistency:** Consistent use of Guid, DateTimeOffset, and DateOnly.
- [x] **Ambiguity check:** Status set to Ready for Entity Implementation.
