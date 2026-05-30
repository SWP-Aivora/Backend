# Proposal Acceptance Design (Orchestrated Workflow)

Standardize the critical transition from **Open Job** to **Active Project** using a dedicated orchestration service to ensure data consistency and clean architecture.

## 1. Core Workflow: `HiringWorkflowService.AcceptProposalAsync`

This service will coordinate between `JobPost`, `Proposal`, and `Project` domains within a single database transaction.

### 1.1 Trigger & Validation
- **Input:** `proposalId`, `currentUserId`.
- **Validation:**
    - Proposal must exist.
    - Associated Job must be in `OPEN` status.
    - Proposal must be in `SUBMITTED` or `SHORTLISTED` status.
    - `currentUserId` must match `Job.ClientId`.

### 1.2 Atomic Operations (Inside Transaction)
1.  **Accept Proposal:** Set target proposal status to `ACCEPTED`.
2.  **Reject Competitors:** Set all other `SUBMITTED`/`SHORTLISTED` proposals for this job to `REJECTED`.
3.  **Close Job:** Update job status to `IN_PROGRESS`.
4.  **Initialize Project:** 
    - Create `Project` linked to the `JobId` and `AcceptedProposalId`.
    - Title/Description inherited from Job.
    - Status: `PENDING_PAYMENT` (Wait for first milestone funding).
5.  **Clone Milestones:** 
    - Convert `ProposalMilestone` records into `Milestone` records for the new project.
    - Maintain `Title`, `Description`, `Amount`, and `OrderIndex`.

## 2. Technical Strategy

- **Service:** `Aivora.Services.HiringWorkflowService.Service`
- **Data Access:** Direct use of `AivoraDbContext` to manage the transaction boundary.
- **Error Handling:** Use `DomainException` (Validation/Unauthorized) for business rule violations. Transaction will auto-rollback on any exception.

## 3. API Contract Update

- **Endpoint:** `PUT /api/v1/proposals/{id}/accept`
- **Controller:** `ProposalController` (will inject `IHiringWorkflowService`).
- **Response:** `ProjectResponse` or the accepted `ProposalResponse`.
