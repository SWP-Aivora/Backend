# Refactor Entities to AuditableBaseEntity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Refactor all 23 entity classes in `Aivora.Repositories/Entities/` to inherit from `AuditableBaseEntity` instead of `BaseEntity` to enable automatic auditing of creation and update times.

**Architecture:** Update class inheritance in all entity files. Since `AuditableBaseEntity` inherits from `BaseEntity`, this is a direct promotion. None of the current entities define `CreatedAt` or `UpdatedAt` explicitly, so no property removal is needed.

**Tech Stack:** .NET / C#

---

### Task 1: Batch Refactor Entities

**Files:**
- Modify: `Aivora.Repositories/Entities/AIJobSuggestion.cs`
- Modify: `Aivora.Repositories/Entities/Category.cs`
- Modify: `Aivora.Repositories/Entities/ClientProfile.cs`
- Modify: `Aivora.Repositories/Entities/Conversation.cs`
- Modify: `Aivora.Repositories/Entities/Deliverable.cs`
- Modify: `Aivora.Repositories/Entities/Dispute.cs`
- Modify: `Aivora.Repositories/Entities/DisputeEvidence.cs`
- Modify: `Aivora.Repositories/Entities/ExpertProfile.cs`
- Modify: `Aivora.Repositories/Entities/ExpertSkill.cs`
- Modify: `Aivora.Repositories/Entities/JobPost.cs`
- Modify: `Aivora.Repositories/Entities/JobSkill.cs`
- Modify: `Aivora.Repositories/Entities/Message.cs`
- Modify: `Aivora.Repositories/Entities/Milestone.cs`
- Modify: `Aivora.Repositories/Entities/Payment.cs`
- Modify: `Aivora.Repositories/Entities/Project.cs`
- Modify: `Aivora.Repositories/Entities/Proposal.cs`
- Modify: `Aivora.Repositories/Entities/ProposalMilestone.cs`
- Modify: `Aivora.Repositories/Entities/RecommendationResult.cs`
- Modify: `Aivora.Repositories/Entities/Review.cs`
- Modify: `Aivora.Repositories/Entities/Skill.cs`
- Modify: `Aivora.Repositories/Entities/User.cs`
- Modify: `Aivora.Repositories/Entities/Wallet.cs`
- Modify: `Aivora.Repositories/Entities/WalletTransaction.cs`

- [ ] **Step 1: Replace inheritance in all entity files**

Use `sed` or similar to replace `: BaseEntity` with `: AuditableBaseEntity` in all files in `Aivora.Repositories/Entities/`.

```powershell
Get-ChildItem -Path "Aivora.Repositories/Entities/*.cs" | ForEach-Object {
    (Get-Content $_.FullName) -replace ": BaseEntity", ": AuditableBaseEntity" | Set-Content $_.FullName
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build Aivora.Repositories/Aivora.Repositories.csproj`
Expected: Build succeeded.

- [ ] **Step 3: Commit changes**

```bash
git add Aivora.Repositories/Entities/
git commit -m "refactor: change all entities to inherit from AuditableBaseEntity"
```
