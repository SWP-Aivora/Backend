# Aivora — Main Flow

> Purpose: This document is the canonical **main business flow** for Aivora.
> It is written for AI coding agents, backend developers, frontend developers, testers, and report writers.
> **v2** — supersedes `MAINFLOW_v1.md` (removed). Rewritten to match current code: dispute resolution no longer auto-moves money (issue #94), project renamed AITasker → Aivora, table names corrected.
> **v2.1** — Flow 2 now covers two independent paths that both create a `Project`: Path A (Job → Proposal, Client-initiated) and Path B (Service → Request → Offer, Expert-initiated). `Service Publishing` was previously listed as "not main flow"; it is now part of Main Flow 2 since the code implements the full Service → Project path.
> **v2.2** — Flow 3/4 rewritten to match the actual `Treasury` module (`Aivora.Services/Treasury/Treasury.cs`), which replaced the "hold-then-release escrow" model this document previously described. There is no `HELD`/`FROZEN` payment state anywhere in the real pipeline: every `Payment` is created `RELEASED` immediately, money moves wallet-to-wallet in two installments (deposit % on Fund, remaining % minus platform commission on Approve). See §3 of `Main Flow 3` below for the exact mechanism.

---

## 0. Scope Lock

The core system flow is:

```text
Client đăng nhu cầu HOẶC Expert đăng service
→ Đối phương phản hồi (Expert gửi proposal / Client chọn service & gửi request)
→ Project được tạo
→ Thanh toán / review / dispute
```

There are exactly **two ways to create a Project**:

```text
Path A — Client-initiated:  Job (Client) → Proposal (Expert) → Accept → Project
Path B — Expert-initiated:  Service (Expert) → Request (Client) → Offer (Expert) → Accept → Project
```

Both paths converge on the same `Projects` table and the same downstream flows (Milestone/Escrow/Deliverable, Completion/Payment/Review). Everything after project creation is identical regardless of which path was used.

Important rule:

```text
AI is NOT a separate actor.
AI is an internal System capability used to support requirement clarification and expert matching.
```

Aivora should be analyzed as a marketplace + project delivery system, not as an "AI actor" system.

---

## 1. Final Main Flow List

Aivora has exactly **4 main business flows**:

| No. | Main Flow | Main Actors | Purpose |
|---:|---|---|---|
| 1 | **Create Job & Match Expert** | Client, System, Expert | Client creates a job, System refines requirements and recommends suitable experts |
| 2 | **Project Creation** (Path A: Proposal, Path B: Service) | Expert, Client, System | Path A: Expert submits a proposal on a job, Client accepts. Path B: Expert publishes a service, Client requests it, Expert offers, Client accepts. Either path creates a project |
| 3 | **Milestone, Escrow & Deliverable** | Client, Expert, System | Client and Expert confirm milestones, Client funds escrow, Expert submits deliverable, Client reviews result |
| 4 | **Completion, Payment, and Review** | Client, Expert, System, Admin | System releases payment, completes project, handles dispute if needed, and allows both sides to review |

Do not split these into 10–15 separate main flows.
Do not use an old 6-flow version as the main version.
Do not treat Path A and Path B as separate main flows — they are two entry points into the same Main Flow 2 (Project Creation), and both converge on Flow 3.

---

## 2. End-to-End Summary

```text
1. Client creates a job and the System helps refine the requirement, suggest skills, budget, timeline, milestones, and recommend suitable experts.

2. A project gets created through one of two paths:
   - **Path A (Proposal):** Expert views the job, submits a proposal, and Client reviews proposals to select the most suitable expert. The System creates a project from the accepted proposal.
   - **Path B (Service):** Expert publishes a service with packages. Client browses services, sends a request on a package. Expert accepts the request and sends a price/milestone offer. Client accepts the offer. The System creates a project from the accepted offer.

3. Client and Expert confirm milestones. Client funds each milestone using escrow. Expert completes the work and submits deliverables. Client reviews the deliverable and either approves, requests revision, or opens a dispute.

4. If a dispute occurs, Admin only observes and documents — the platform does not decide the outcome. Admin marks the dispute resolved for the record, which unlocks the milestone; Client and Expert then settle it themselves through the normal milestone approve/revision flow. After all milestones are completed, the project is marked as completed and both Client and Expert can review each other.
```

One-line description:

```text
Aivora has four main business flows: job creation and expert matching, project creation (via proposal on a job, or via a service request/offer), milestone-based delivery with escrow, and dispute review with project completion and peer review.
```

---

# MAIN FLOW 1: Create Job & Match Expert

## Actor chính

**Client**

## Actor phụ

**Expert**

## Internal system capability

**AI-assisted requirement clarification and expert matching**

## Goal

Client creates a job post for hiring an AI expert.
The System helps clarify the requirement, suggest skills/budget/timeline/milestones, and recommend suitable experts.

## Preconditions

```text
1. Client has an active account.
2. Client is authenticated.
3. Expert profiles and skills already exist in the system.
```

## Main Flow

```text
1. Client logs in.

2. Client selects Create Job.

3. Client enters initial requirement.
   Example:
   "Tôi muốn làm chatbot AI cho shop mỹ phẩm."

4. Client enters basic project information:
   - Business domain
   - Expected outcome
   - Estimated budget
   - Estimated timeline
   - Existing data
   - Required language support

5. System helps clarify the requirement:
   - Rewrite the job description more clearly
   - Suggest required skills
   - Suggest reasonable budget
   - Suggest timeline
   - Suggest milestones
   - Generate clarifying questions

6. Client reviews the System suggestion.

7. Client edits the job content if needed.

8. Client publishes the job.

9. System saves the job with status OPEN.
   System emits JobStatusUpdated (status=open) to the Client over SignalR.

10. System calculates suitable experts based on:
    - Skill match
    - Portfolio relevance
    - Rating
    - Budget fit
    - Availability
    - Completed projects

11. System shows recommended experts to Client.

12. Expert can view the OPEN job on the marketplace.
```

## Status Transition

```text
Job: NULL → DRAFT → OPEN
```

Simplified version:

```text
Job: NULL → DRAFT / OPEN
```

## Main Tables

| Table | Purpose |
|---|---|
| `JobPosts` | Stores client job post |
| `JobSkills` | Stores required skills for the job |
| `JobPostMilestones` | Stores milestone plan attached to the job (manual create or AI-accept) |
| `AIJobSuggestions` | Stores AI-assisted suggestion output |
| `RecommendationResults` | Stores matching score and explanation |
| `ExpertProfiles` | Provides expert profile data |
| `ExpertSkills` | Provides expert skill data |
| `Skills` | Skill master data |
| `Categories` | Job/service category data |

## Expected Output

```text
1. Job is created and published.
2. Job status is OPEN.
3. AI suggestion is stored.
4. Job skills are linked.
5. Recommended experts are generated.
6. Experts can discover the job.
```

## Business Meaning

This flow combines:

```text
Create Job
+ AI-assisted Requirement
+ Expert Recommendation
```

This is the entry point of the system.
Without an OPEN job, proposal, project, milestone, escrow, deliverable, payment, and review flows cannot happen.

---

# MAIN FLOW 2: Project Creation

Project creation has **two independent paths**. Both end with a `Project` row created and linked to Client + Expert; everything from Main Flow 3 onward is identical regardless of path.

```text
Path A (Proposal, Client-initiated): Job (OPEN) → Expert Proposal → Client Accept → Project
Path B (Service, Expert-initiated):  Expert Service (PUBLISHED) → Client Request → Expert Offer → Client Accept → Project
```

---

## Path A: Proposal & Project Creation

## Actor chính

**Expert**, **Client**

## Internal system capability

**Proposal validation and atomic project creation**

## Goal

Expert submits a proposal for an OPEN job.
Client compares proposals, accepts one expert, and the System creates a project.

## Preconditions

```text
1. Job exists.
2. Job status is OPEN.
3. Expert has an active account.
4. Expert is allowed to submit proposal.
5. Client is the owner of the job.
```

## Main Flow

```text
1. Expert logs in.

2. Expert opens Find Jobs.

3. Expert views list of OPEN jobs.

4. Expert selects a suitable job.

5. System displays job details:
   - Title
   - Description
   - Required skills
   - Budget
   - Timeline
   - Suggested milestones
   - Client information

6. Expert selects Submit Proposal.

7. Expert enters proposal details:
   - Cover letter
   - Proposed budget
   - Proposed timeline
   - Proposed milestones
   - Portfolio/demo link if available

8. Expert submits proposal.

9. System validates proposal.

10. System saves proposal with status SUBMITTED.

11. Client receives notification about the new proposal.

12. Client opens their job and views proposal list.

13. Client compares proposals by:
    - Match score
    - Expert rating
    - Proposed budget
    - Proposed timeline
    - Portfolio
    - Proposal content

14. Client can:
    - Reject proposal
    - Shortlist proposal
    - Message expert
    - Accept proposal

15. Client selects Accept Proposal.

16. System validates:
    - Job is still OPEN
    - Proposal is valid
    - Expert is active
    - Client is the job owner

17. System updates selected proposal to ACCEPTED.

18. System updates remaining proposals to REJECTED.

19. System updates Job status to IN_PROGRESS.
    System emits JobStatusUpdated (status=in_progress) to the Client over SignalR.

20. System creates Project from:
    - Job
    - Accepted proposal
    - Client
    - Expert
    - Budget
    - Timeline
    - Proposed milestones

21. Client and Expert move to project management phase.
```

## Status Transition

```text
Proposal: NULL → SUBMITTED → ACCEPTED / REJECTED / WITHDRAWN

Job: OPEN → IN_PROGRESS

Project: NULL → PENDING_PAYMENT
```

## Main Tables

| Table | Purpose |
|---|---|
| `JobPosts` | Source job data |
| `Proposals` | Stores expert proposal |
| `ProposalMilestones` | Stores milestone plan proposed by expert |
| `Projects` | Created after proposal acceptance |
| `Milestones` | Generated from proposal milestones when accepted |
| `Conversations` | Optional conversation between Client and Expert |
| `Messages` | Optional message history |

## Transaction Rule

Accepting a proposal must be atomic.

```text
Accept proposal transaction:
1. Validate job/proposal/client/expert.
2. Set selected proposal = ACCEPTED.
3. Set sibling proposals (SUBMITTED/SHORTLISTED) = REJECTED.
4. Set job = IN_PROGRESS.
5. Create project (status = PENDING_PAYMENT).
6. Create milestones from proposal milestones (status = CREATED).
7. Commit transaction.
```

If any step fails, rollback everything.

## Expected Output

```text
1. One proposal becomes ACCEPTED.
2. Other active proposals become REJECTED.
3. Job becomes IN_PROGRESS.
4. Project is created.
5. Project is linked to job, accepted proposal, client, and expert.
```

## Business Meaning

This flow combines:

```text
Submit Proposal
+ Client Accept Proposal
+ Create Project
```

This is the transition from **marketplace phase** to **project execution phase**.

Before this flow, Aivora is only a place to post jobs and find experts.
After this flow, Aivora starts managing real project delivery.

---

## Path B: Service & Project Creation

## Actor chính

**Expert**, **Client**

## Internal system capability

**Service catalog management and atomic project creation from an accepted offer**

## Goal

Expert publishes a reusable service (with pricing packages) instead of waiting for a job post.
Client browses services, requests one on a specific package, Expert reviews and sends a price/milestone offer, Client accepts, and the System creates a project — no Job or Proposal involved.

## Preconditions

```text
1. Expert has an active account.
2. Expert has created a service with at least one package and one FAQ.
3. Client has an active account.
4. Client is not the owner of the service (cannot request own service).
```

## Main Flow

```text
1. Expert selects Create Service.

2. Expert enters service details:
   - Title
   - Description
   - Attachment (optional)
   - One or more pricing packages (tier: BASIC/STANDARD/PREMIUM, title, price, delivery days, features)
   - One or more FAQs

3. System saves service with status DRAFT.

4. Expert edits service if needed (title, description, packages, FAQs — partial update).

5. Expert selects Publish Service.

6. System validates the service has at least one package and one FAQ.

7. System sets service status to PUBLISHED, PublishedAt = now.

8. Client browses published services on the marketplace.

9. Client opens a service and selects a package.

10. Client selects Request Service, optionally with a note.

11. System validates:
    - Service is PUBLISHED
    - Client is not the service owner
    - Client has no other PENDING request on this service

12. System creates ServiceRequest with status PENDING, snapshotting the package's
    title/price/delivery days at request time (so a later Expert edit to the
    package can't retroactively change what the Client already requested).

13. Expert receives notification of the new request.

14. Expert opens the request and chooses Accept or Decline.

15a. If Expert declines:
     System sets ServiceRequest status to DECLINED. Flow ends.

15b. If Expert accepts:
     System sets ServiceRequest status to ACCEPTED.
     System opens a Conversation between Client and Expert for this request.

16. Expert selects Send Offer, entering:
    - Total amount
    - One or more milestones (title, description, amount, due days, acceptance criteria)

17. System validates the ServiceRequest is ACCEPTED, then saves the offer with
    status PENDING.

18. Client receives notification of the offer and reviews it.

19. Client selects Accept Offer.

20. System validates the offer is still PENDING.

21. System creates Project from:
    - Service title/description
    - Offer amount and milestones
    - Client (requester) and Expert (offer sender)
    - ServiceRequestId (no JobId, no ProposalId)

22. Client and Expert move to project management phase (Main Flow 3).
```

## Status Transition

```text
Service: NULL → DRAFT → PUBLISHED (→ DRAFT again via Unpublish)

ServiceRequest: NULL → PENDING → ACCEPTED / DECLINED

ServiceOffer: NULL → PENDING → ACCEPTED

Project: NULL → PENDING_PAYMENT (created directly ACTIVE-bound, same as Path A)
```

## Main Tables

| Table | Purpose |
|---|---|
| `Services` (`ServiceListing`) | Stores the expert's published service |
| `ServicePackages` | Pricing tiers (BASIC/STANDARD/PREMIUM) for a service |
| `ServiceFaqs` | FAQ entries shown on the service page |
| `ServiceRequests` | Stores a client's request against one service package (snapshots price/delivery at request time) |
| `ServiceOffers` | Stores the expert's price/milestone offer sent in response to an accepted request |
| `ServiceOfferMilestones` | Milestone plan proposed inside a service offer |
| `Projects` | Created after offer acceptance (same table as Path A) |
| `Milestones` | Generated from offer milestones when accepted |
| `Conversations` | Opened automatically when Expert accepts a service request |

## Transaction Rule

Accepting a service offer must be atomic, mirroring the proposal-accept transaction in Path A.

```text
Accept offer transaction:
1. Validate offer is PENDING and requester owns the underlying ServiceRequest.
2. Set offer = ACCEPTED.
3. Create project (status = PENDING_PAYMENT), linked via ServiceRequestId.
4. Create milestones from offer milestones.
5. Commit transaction.
```

A partial unique index on `Projects.ServiceRequestId` guards against a race where two concurrent accepts on the same service request would otherwise create two projects — the second commit fails with a unique violation and is surfaced as a validation error ("This service request already has an accepted offer.").

## Expected Output

```text
1. Service offer becomes ACCEPTED.
2. Project is created.
3. Project is linked to service request, accepted offer, client, and expert.
4. Job and Proposal tables are untouched — this path never creates a JobPosts or Proposals row.
```

## Business Meaning

This flow combines:

```text
Publish Service
+ Client Request
+ Expert Offer
+ Client Accept Offer
+ Create Project
```

This is the Expert-initiated counterpart to Path A: instead of a Client posting a need and waiting for proposals, the Expert pre-packages their offering and Clients discover and request it directly. Both paths converge on the same project execution phase.

---

# MAIN FLOW 3: Milestone, Escrow & Deliverable

## Actor chính

**Client**, **Expert**

## Internal system capability

**Milestone tracking, escrow, deliverable management**

## Goal

Client and Expert confirm project milestones.
Client funds a milestone with a **deposit installment** (not a full-amount hold).
Expert completes the work and submits deliverable, optionally tracked through granular **milestone steps**.
Client reviews the deliverable; approving it releases the **remaining installment minus platform commission**.

> **Money model — read this before anything else.** All money movement for Flow 3/4 goes through one module, `Aivora.Services/Treasury/Treasury.cs` ("Deep Module chịu trách nhiệm duy nhất về tính toàn vẹn tài chính"). It is **not** an escrow-hold system despite the entity being called `Payment` with a `HELD`/`FROZEN` status in the enum — those enum values exist but are **never assigned** by the current pipeline. Instead:
> - **Fund** (`PUT /milestones/{id}/fund` → `Treasury.PayDepositAsync`) transfers `milestone.Amount × DepositRate` (default **30%**, `EscrowOptions.DepositRate`) directly **Client wallet → Expert wallet**, right away. The `Payment` row created here has `Status = RELEASED` from the start.
> - **Approve** (`PUT /milestones/{id}/approve` → `Treasury.PayRemainingAsync`) transfers the remaining `milestone.Amount × RemainingRate` (default **70%**) from Client, splits it into `expertAmount = remaining − commission` to Expert and `commission = milestone.Amount × CommissionRate` (default **10% of the full milestone amount**, not of the remaining 70%) to a platform system wallet (`SystemConstants.SystemUserId`). Another `Payment` row is created, again `Status = RELEASED` immediately.
> - Net result at default rates: Expert receives 30% + (70% − 10%) = **90%** of `milestone.Amount`; platform keeps **10%**.
> - `Treasury` also has `RefundMilestoneAsync` and `SplitMilestoneFundsAsync` fully implemented (clawback from Expert wallet with a configurable `MaxDebtLimit`, default 1000 AICOIN) — **but no controller or service currently calls either method**. They exist as ready-to-wire logic for a future "Admin force-refund/split" feature, not as part of today's dispute flow (see Main Flow 4 §4B).

## Preconditions

```text
1. Project exists.
2. Project is linked to one accepted proposal (Path A) or one accepted service offer (Path B).
3. Project has Client and Expert.
4. Client has wallet with sufficient balance (deposited via VNPay, or credited via demo deposit in dev/test).
5. Expert has wallet.
6. Milestone plan exists or can be created from proposal/job suggestion/service offer.
```

## Main Flow

```text
1. After project is created, Client opens Project Detail.

2. System displays initial milestone plan.
   Milestone can come from:
   - System-suggested milestones during job creation
   - Expert-proposed milestones in proposal
   - Client-edited milestones

3. Client and Expert review milestone plan.

4. Each milestone includes:
   - Title
   - Description
   - Amount
   - Due date
   - Acceptance criteria
   - Optional list of milestone steps (Expert can ask AI to suggest steps, add/reorder/update them)
   - The system also auto-inserts a `MilestoneStep` titled `"Created"` (status `COMPLETED`) the moment the milestone itself is created — this and two other reserved titles (`"Funded"`, `"Completed"`, added later at fund/approve time) are system-generated markers that Expert cannot create, rename, or delete through the milestone-step endpoints.

5. Client confirms milestone plan.

6. System creates milestones with status CREATED.

7. Project moves to PENDING_PAYMENT.

8. Client selects the first milestone to fund. Client tops up their wallet first if needed
   (VNPay redirect flow: POST /wallet/vnpay/deposit → pay on VNPay → IPN callback credits wallet;
   or a demo credit via POST /wallet/deposit-demo in dev/test).

9. Client clicks Fund Milestone.

10. System checks Client wallet balance against the **deposit installment** (`milestone.Amount × DepositRate`, default 30%) — not the full milestone amount.

11. If balance is enough (`Treasury.PayDepositAsync`):
    - System debits the deposit amount from Client's available balance
    - System credits the SAME amount directly to Expert's available balance (no platform hold)
    - A `Payments` row is created with `Status = RELEASED` immediately (never `HELD`)
    - System auto-adds a `"Funded"` milestone step (status `COMPLETED`) if not already present
    - Milestone status becomes IN_PROGRESS (not `FUNDED` — that enum value is never assigned by the current code)
    - Project status becomes ACTIVE (if it was PENDING_PAYMENT)

12. Expert receives notification that the deposit was paid and can start working.

13. Expert starts working, optionally tracking granular progress via milestone steps
    (PENDING → IN_PROGRESS → COMPLETED / SKIPPED / BLOCKED per step; unblocking BLOCKED → IN_PROGRESS is the one transition that belongs to Client, every other transition is Expert's).

14. When finished, Expert submits deliverable:
    - Description
    - Demo URL
    - GitHub/source code link
    - File/document link
    - Setup instruction
    - Note to Client

15. System saves deliverable with status SUBMITTED.

16. Milestone status becomes SUBMITTED.

17. Project status becomes IN_REVIEW.

18. Client receives notification that deliverable is ready for review.

19. Client opens deliverable and compares it with acceptance criteria.

20. Client chooses one of three actions:
    A. Approve
    B. Request Revision
    C. Open Dispute
```

## Case A: Approve

```text
1. Client selects Approve.

2. System does NOT change Deliverable.Status — it stays SUBMITTED forever. No code path in
   the current pipeline (Treasury.PayRemainingAsync, MilestoneService, DeliverableService)
   ever sets Deliverable.Status to APPROVED/REVISION_REQUESTED/REJECTED, and
   Deliverable.ReviewedAt stays null. "Was this deliverable approved?" can only be answered
   by checking Milestone.Status == RELEASED, not the Deliverable row itself.

3. System releases the remaining installment via Treasury.PayRemainingAsync:
   - remainingAmount = milestone.Amount × RemainingRate (default 70%)
   - commissionAmount = milestone.Amount × CommissionRate (default 10%, computed on the
     FULL milestone amount, not on remainingAmount)
   - expertAmount = remainingAmount − commissionAmount
   - Client wallet debited remainingAmount; Expert wallet credited expertAmount;
     platform system wallet credited commissionAmount (WalletTransaction Type=PLATFORM_FEE)
   - A new Payments row created, Status = RELEASED

4. Milestone status becomes RELEASED (skips the FUNDED/APPROVED enum values entirely).

5. System auto-adds a "Completed" milestone step (status COMPLETED) if not already present,
   and auto-completes every remaining PENDING/IN_PROGRESS/BLOCKED step on that milestone.

6. Expert earning (TotalEarned) increases by expertAmount; platform wallet TotalEarned
   increases by commissionAmount.

7. Treasury.SyncProjectStatusAsync recalculates Project.Status from milestone states:
   if there are remaining un-settled milestones, Project stays/returns to ACTIVE.

8. If ALL milestones on the project are settled (RELEASED or REFUNDED), Project becomes
   COMPLETED automatically (no explicit "finish project" action exists on the backend —
   see the gotcha below) and, if the Project has a JobId, JobPosts.Status becomes COMPLETED
   too, with JobStatusUpdated emitted over SignalR. Projects created via Flow 2 Path B
   (Service offer) have no JobId, so no Job-side update happens for them.
```

> **Gotcha:** the frontend has a "Finish Project" button (`ProjectWorkspacePage.tsx`, `completeProject` → `PUT /projects/{id}/complete`) but `ProjectController.cs` has no such route. Since completion is already automatic via `SyncProjectStatusAsync`, this button currently 404s if clicked — it needs to either be removed or backed by a real endpoint.

## Case B: Request Revision

```text
1. Client selects Request Revision.

2. Client enters revision reason.

3. System stores revision request — but again, only on Milestone, not on Deliverable
   (Deliverable.Status stays SUBMITTED; there is no REVISION_REQUESTED write anywhere for it).

4. Milestone status becomes REVISION_REQUESTED.

5. Project status returns to ACTIVE (via Treasury.SyncProjectStatusAsync).

6. No money moves: both installments (deposit already paid, remaining not yet paid) are
   untouched — there is nothing "HELD" to keep, the deposit already sits in Expert's wallet
   from step 11 of the main flow above.

7. Expert fixes the deliverable.

8. Expert submits again (same endpoint, new Deliverable row with RevisionNumber + 1).

9. Milestone returns to SUBMITTED.

10. Client reviews again.
```

## Case C: Open Dispute

```text
1. Client or Expert selects Open Dispute (from the milestone, or directly via POST /disputes).

2. User enters dispute reason and evidence if available. Precondition: a Payments row for
   this milestone must exist with Status RELEASED or HELD (in practice always RELEASED,
   since the deposit payment is created RELEASED the moment the milestone is funded).

3. System creates dispute (status = OPEN).

4. Milestone status becomes DISPUTED.

5. Project status becomes DISPUTED.

6. Payment status is NOT changed — it stays RELEASED. There is no FROZEN transition anywhere
   in DisputeService.OpenDisputeAsync; money already transferred (the deposit installment)
   stays in Expert's wallet while the dispute is open. Only the milestone/project are locked
   from further action (fund/approve/request-revision/submit-deliverable all reject while
   DISPUTED).

7. Admin receives notification to review the dispute.
```

## Status Transition

```text
Project:
CREATED → PENDING_PAYMENT → ACTIVE → IN_REVIEW → COMPLETED
or
ACTIVE / IN_REVIEW → DISPUTED
DISPUTED → ACTIVE (via dispute resolve/close) → ... → COMPLETED
(project completion is fully automatic once every milestone is settled — there is no
manual "complete project" action on the backend)

Milestone (as actually assigned by code — FUNDED/APPROVED/COMPLETED enum values exist but
are never set by any current code path):
CREATED → IN_PROGRESS → SUBMITTED → RELEASED

Milestone revision path:
SUBMITTED → REVISION_REQUESTED → SUBMITTED

Milestone dispute path:
IN_PROGRESS / SUBMITTED → DISPUTED
DISPUTED → SUBMITTED (if a deliverable was already submitted) / IN_PROGRESS (otherwise),
           via admin dispute resolve — RELEASED/REFUNDED only through a subsequent
           normal approve action, never automatically by the dispute itself
DISPUTED → IN_PROGRESS unconditionally via dispute close (opener backs out) — see Gotcha below

> **Gotcha:** `ResolveDisputeAsync` and `CloseDisputeAsync` (`DisputeService/Service.cs`) unlock
> the milestone inconsistently. Resolve checks `milestone.SubmittedAt` and restores `SUBMITTED`
> if a deliverable was already submitted before the dispute; close always sets `IN_PROGRESS`
> regardless. If the opener closes a dispute after a deliverable was submitted, the milestone
> loses its SUBMITTED (review-pending) state and the deliverable needs to be resubmitted.

Payment (as actually assigned by code — HELD/FROZEN/PARTIALLY_RELEASED enum values exist
but are never set by the current pipeline; every Payment row is created RELEASED):
NULL → RELEASED (created RELEASED at Fund time for the deposit installment,
                 and again at Approve time for the remaining installment —
                 two separate Payment rows per milestone in the happy path)

Payment dispute path:
No transition happens — Payment status is untouched while a dispute is open or resolved.
REFUNDED only happens if Treasury.RefundMilestoneAsync is ever wired up and called
(currently no caller exists anywhere in the codebase).
```

## Main Tables

| Table | Purpose |
|---|---|
| `Projects` | Stores project lifecycle |
| `Milestones` | Stores project milestone status |
| `MilestoneSteps` | Granular per-milestone task tracking — includes 3 system-generated marker steps (`Created`/`Funded`/`Completed`) mixed in with Expert-authored ones, distinguishable only by `Title` |
| `Payments` | One row per money movement (deposit, remaining) — always created `RELEASED`, never used as a real "hold" ledger despite the enum having `HELD`/`FROZEN`/`PARTIALLY_RELEASED` |
| `Wallets` | User wallet balance, including one system wallet (`SystemConstants.SystemUserId`) that accumulates platform commission |
| `WalletTransactions` | Wallet movement logs — `Type = PLATFORM_FEE` marks the commission cut, separate from `PAYMENT_RELEASE` |
| `Deliverables` | Stores expert submission — `Status` never advances past `SUBMITTED` in the current code |
| `Disputes` | Created only if dispute is opened |
| `DisputeEvidences` | Stores dispute evidence if available |

## Business Meaning

This is the most important business execution flow.

It combines:

```text
Confirm Milestones
+ Fund Escrow
+ Submit Deliverable
+ Review Deliverable
+ Approve / Revision / Dispute
```

This flow proves that Aivora is not only a job board.
It manages project delivery, milestone validation, and payment safety.

---

# MAIN FLOW 4: Completion, Payment, and Review

## Actor chính

**Client**, **Expert**, **System**

## Actor phụ

**Admin**
Admin participates only when a dispute occurs — and only as an **observer**, not a decision-maker. The platform does not adjudicate disputes; Client and Expert settle them directly.

## Goal

When work is completed and approved, the System releases the remaining payment installment (minus platform commission), marks milestone/project as completed, and allows both sides to review each other.

If a dispute occurs, Admin reviews the evidence and writes a resolution note for the record, then unlocks the milestone/project so Client and Expert can continue on their own. **The platform does not decide who is right or move any money** — Client (fund the milestone further, approve it) and Expert (resubmit, negotiate) resolve the disagreement themselves through the normal milestone actions once unlocked.

---

## 4A. Happy Path: Approved Deliverable → Released Payment → Completed Project → Reviews

### Preconditions

```text
1. Job status = IN_PROGRESS (if the project came from Flow 2 Path A / has a JobId).
2. Project status = ACTIVE or IN_REVIEW.
3. Milestone status = SUBMITTED.
4. Deliverable status = SUBMITTED (it will still be SUBMITTED after approval too — see gotcha below).
5. A Payments row for the deposit installment already exists with Status = RELEASED
   (created at Fund time — there is no HELD state to check).
6. Client wallet has already paid the deposit installment at Fund time, and has enough
   available balance to cover the remaining installment for Approve to succeed.
7. Expert wallet can receive the released payment.
```

### Step 4.1 — Expert submits deliverable

Action:

```text
Expert submits final work for milestone.
```

Example deliverable:

```text
Demo URL: https://demo.beauty-chatbot.com
Source Code URL: https://github.com/demo/beauty-chatbot
Note: Chatbot MVP completed with FAQ, product recommendation, and admin prompt config.
```

Expected database result:

| Table | Field | Expected Value |
|---|---|---|
| `Deliverables` | `Status` | `SUBMITTED` |
| `Deliverables` | `RevisionNumber` | `1` |
| `Milestones` | `Status` | `SUBMITTED` |
| `Milestones` | `SubmittedAt` | Not null |
| `Projects` | `Status` | `IN_REVIEW` |

### Step 4.2 — Client reviews submitted work

Client checks:

```text
1. Chatbot answers beauty product questions.
2. Chatbot can recommend skincare products.
3. Demo URL works.
4. Source code is provided.
5. Work matches acceptance criteria.
```

Available actions:

```text
Approve
Request Revision
Open Dispute
```

For happy path, Client chooses:

```text
Approve
```

### Step 4.3 — Client approves deliverable

Expected database result — **`Deliverables` is untouched**, this is the biggest divergence from the old design:

| Table | Field | Expected Value |
|---|---|---|
| `Deliverables` | `Status` | Stays `SUBMITTED` — never becomes `APPROVED` in the current code |
| `Deliverables` | `ReviewedAt` | Stays `null` — never written by any code path |
| `Milestones` | `Status` | `RELEASED` (goes straight from `SUBMITTED`, the `APPROVED` enum value is skipped) |
| `Milestones` | `PaidAt` | Not null |
| `Milestones` | `ApprovedAt` | Not null |

### Step 4.4 — System releases the remaining installment (with commission)

Approving a deliverable and releasing its payment happen in the **same atomic transaction** (`PUT /milestones/{id}/approve` → `MilestoneService.ApproveMilestoneAsync` → `Treasury.PayRemainingAsync`) — there is no separate release-payment endpoint. This is the **second** `Payments` row for this milestone; the deposit installment already created its own `Payments` row back at Fund time.

Expected database result:

| Table | Field | Expected Value |
|---|---|---|
| `Payments` (new row) | `Status` | `RELEASED` (created RELEASED, never passes through HELD) |
| `Payments` | `ReleasedAt` | Not null |
| `Payments` | `Amount` | `milestone.Amount × RemainingRate` (default 70% of the milestone amount — NOT the full amount) |
| `Milestones` | `Status` | `RELEASED` |
| `Milestones` | `PaidAt` | Not null |
| `WalletTransactions` (Client) | `Type` / `Direction` | `PAYMENT_RELEASE` / `DEBIT`, amount = remaining installment |
| `WalletTransactions` (Expert) | `Type` / `Direction` | `PAYMENT_RELEASE` / `CREDIT`, amount = remaining installment **minus commission** |
| `WalletTransactions` (Platform system wallet) | `Type` / `Direction` | `PLATFORM_FEE` / `CREDIT`, amount = commission (only written if commission > 0) |

Example wallet result — milestone amount 900, default rates (30% deposit / 10% commission):

```text
At Fund time (deposit = 900 × 0.30 = 270):
  Client available -= 270 ; Expert available += 270

At Approve time (remaining = 900 × 0.70 = 630 ; commission = 900 × 0.10 = 90 ; expertAmount = 630 - 90 = 540):
  Client available -= 630 ; Expert available += 540 ; Platform available += 90
```

| Wallet | Available Balance change | Total Earned change |
|---|---:|---:|
| Client | `-270` (Fund) then `-630` (Approve) = `-900` total | unchanged |
| Expert | `+270` (Fund) then `+540` (Approve) = `+810` total (= 90% of milestone.Amount) | `+810` |
| Platform (system wallet) | `+90` (Approve only) | `+90` (10% of milestone.Amount) |

> There is **no "Held Balance" column moving** anywhere in this flow — `Wallet.HeldBalance` exists on the entity but the Treasury pipeline debits/credits `AvailableBalance` directly for both the deposit and the remaining installment. If `Wallet.HeldBalance` is used elsewhere in the codebase (e.g. a different feature), it is not this one.

### Step 4.5 — System completes project (fully automatic, no manual trigger)

`Treasury.SyncProjectStatusAsync` runs at the end of every money-moving operation (Fund, Approve, and also after Request Revision / Submit Deliverable) and checks:

```text
If every Milestone on the Project is "settled" (Status RELEASED or REFUNDED),
mark Project as COMPLETED.
```

Expected database result:

| Table | Field | Expected Value |
|---|---|---|
| `Projects` | `Status` | `COMPLETED` |
| `Projects` | `CompletedAt` | Not null |
| `JobPosts` | `Status` | `COMPLETED` — **only if the Project has a `JobId`** (Flow 2 Path A). Projects created via Flow 2 Path B (Service offer) have no `JobId` and this step is simply skipped for them — there is no Job row to update. |

System emits `JobStatusUpdated` (status=completed) to the Client over SignalR — again, only when `JobId` is present.

> **There is no "Client clicks Finish Project" step.** The frontend has a button wired to `PUT /projects/{id}/complete`, but that route does not exist on `ProjectController`. Completion always happens as an automatic side effect of the last milestone being settled.

### Step 4.6 — Client reviews Expert

Example review:

```text
Rating: 5
Comment: Expert delivered a working chatbot on time with good quality.
Communication rating: 5
Quality rating: 5
Deadline rating: 5
```

Expected database result:

| Table | Field | Expected Value |
|---|---|---|
| `Reviews` | `ReviewerId` | Client user id |
| `Reviews` | `RevieweeId` | Expert user id |
| `Reviews` | `Rating` | `5` |
| `Reviews` | `QualityRating` | `5` |
| `Reviews` | `DeadlineRating` | `5` |

### Step 4.7 — Expert reviews Client

Example review:

```text
Rating: 5
Comment: Client provided clear requirements and fast feedback.
Requirement clarity rating: 5
Communication rating: 5
```

Expected database result:

| Table | Field | Expected Value |
|---|---|---|
| `Reviews` | `ReviewerId` | Expert user id |
| `Reviews` | `RevieweeId` | Client user id |
| `Reviews` | `Rating` | `5` |
| `Reviews` | `RequirementClarityRating` | `5` |

---

## 4B. Dispute Path: Dispute → Admin Observes → Client/Expert Settle Directly

> **Rewritten for current code.** The earlier version of this document described an automatic resolution enum (`RELEASE_TO_EXPERT` / `REFUND_TO_CLIENT` / `SPLIT_PAYMENT` / `REQUEST_REVISION`) that moved money as part of resolving the dispute. **That behavior was removed in issue #94.** `PUT /disputes/{id}/resolve` today only accepts a `ResolutionNote`, moves the dispute to `RESOLVED`, and **unlocks** the milestone (→ `SUBMITTED` if a deliverable was already submitted, else `IN_PROGRESS`) and the project (→ `ACTIVE` if no other milestone on the project is still `DISPUTED`) — but it never touches `Payments` or `Wallets`. Any money movement after a dispute still has to go through the normal milestone flow (approve / request-revision) as a separate step, now that the milestone is unlocked.

### Trigger

```text
Client or Expert opens a dispute from a submitted milestone/deliverable, or directly via POST /disputes.
```

### Preconditions

```text
1. Project exists.
2. Milestone exists.
3. Payment status = HELD.
4. Deliverable has been submitted or milestone has a conflict.
```

### Main Flow

```text
1. Client or Expert opens dispute.

2. User enters:
   - Reason
   - Description
   - Evidence if available

3. System creates Dispute (status = OPEN).

4. System updates:
   - Milestone status = DISPUTED
   - Payment status = FROZEN
   - Project status = DISPUTED

5. Admin opens dispute detail — as an **observer**, not a judge.

6. Admin reviews (for the record, not to decide an outcome):
   - Job description
   - Proposal
   - Milestone acceptance criteria
   - Deliverable
   - Message history if available
   - Payment status
   - Evidence from both sides (can request more evidence — dispute moves to UNDER_REVIEW)

7. Admin writes a ResolutionNote documenting what was observed, and resolves the dispute record:
   PUT /disputes/{id}/resolve { "resolutionNote": "..." }
   This does not declare a winner or move money — it just closes the dispute record and unlocks the milestone.

8. System sets Disputes.Status = RESOLVED, records ResolutionNote + AdminId + ResolvedAt.
   System also unlocks Milestone (→ SUBMITTED if a deliverable was already submitted, else → IN_PROGRESS)
   and, if no other milestone on the project is still DISPUTED, unlocks Project (→ ACTIVE).
   Payment/Wallet status are NOT changed by this step — no money moves automatically.

9. Client and Expert now settle the disagreement themselves through the normal milestone
   lifecycle — the platform does not do this for them. In practice this means Client approves
   the milestone (releases payment) once satisfied, or Expert resubmits after Client requests
   revision. Both of those actions are `ClientPolicy`-only; Admin has no endpoint to force either
   outcome.

10. Alternatively, the dispute opener can close it themselves via PUT /disputes/{id}/close
    at any point before an admin resolution, if the disagreement is settled directly between
    the parties without needing Admin involvement at all.
```

> **Inconsistency to know about:** `ResolveDisputeAsync` unlocks the milestone smartly — `SUBMITTED` if a deliverable was already submitted (`Milestone.SubmittedAt != null`), else `IN_PROGRESS`. `CloseDisputeAsync` (self-close by the opener) **always** sets `IN_PROGRESS` regardless of `SubmittedAt` — if a deliverable had already been submitted before the dispute opened, closing it this way drops the milestone back to `IN_PROGRESS` and Expert has to submit again even though the earlier `Deliverable` row is still sitting in the DB (unreachable by status, only by direct query). Not confirmed whether this is intentional; flag it if it causes confusion during testing.

> **`Treasury.RefundMilestoneAsync` / `SplitMilestoneFundsAsync` are not part of this flow.** Both methods exist fully implemented in `Treasury.cs` (clawback logic, `MaxDebtLimit` guard, wallet transaction logging) but **no controller or service in the codebase calls either one** — `DisputeService.ResolveDisputeAsync` only ever touches `Disputes`/`Milestones`/`Projects`, never `Treasury`. If a future requirement needs "Admin force-refund the client" or "Admin split the payment," the financial logic already exists and only needs a new endpoint + a call site.

### Dispute Status Values

| Status | Meaning |
|---|---|
| `OPEN` | Just created, awaiting admin review |
| `UNDER_REVIEW` | Admin has requested more evidence |
| `RESOLVED` | Admin wrote a resolution note (observation, not a verdict); dispute record closed, milestone unlocked for Client/Expert to settle directly |
| `CLOSED` | Opener closed the dispute themselves (no admin decision needed) |

---

## Final Expected System State

Happy path final state:

| Entity | Final Status |
|---|---|
| `JobPosts.Status` | `COMPLETED` (only if the project has a `JobId`) |
| `Projects.Status` | `COMPLETED` |
| `Milestones.Status` | `RELEASED` |
| `Deliverables.Status` | `SUBMITTED` — stays as-is, never becomes `APPROVED` |
| `Payments` | 2 rows per milestone (deposit + remaining), both `Status = RELEASED` |
| `Reviews` | 2 reviews created |
| Expert wallet | Received 90% of milestone.Amount at default rates (30% deposit + 60% net remaining) |
| Platform system wallet | Received 10% of milestone.Amount as commission |
| Expert profile | Completed project count increased |

---

## Acceptance Criteria

```text
1. Expert can submit deliverable.
2. Client can approve deliverable — this releases the remaining installment (minus
   platform commission) but does NOT change Deliverable.Status.
3. Payment (remaining installment) is released only after approval, in the same
   transaction as the approval. The deposit installment was already released earlier,
   at Fund time — "payment" here is not a single release, it's the second of two.
4. Milestone changes to RELEASED after the remaining installment is released.
5. Project changes to COMPLETED automatically once all milestones are RELEASED/REFUNDED
   — there is no manual "complete project" action.
6. Client can review Expert.
7. Expert can review Client.
8. Review rating must be between 1 and 5.
9. User cannot review themselves.
10. Same reviewer cannot review the same reviewee twice for the same project.
11. If Client requests revision, no money moves (deposit already paid stays paid; nothing
    was "HELD" to keep held).
12. If Client/Expert opens dispute, Milestone and Project become DISPUTED, but Payment
    status is NOT changed (stays RELEASED — there is no FROZEN transition in the code).
13. Admin resolving a dispute only records a ResolutionNote and unlocks milestone/project
    — it does not itself release, refund, or split payment. (`Treasury.RefundMilestoneAsync`
    / `SplitMilestoneFundsAsync` exist but have no caller.)
```

---

## Important Negative Test Cases

| Test Case | Expected Result |
|---|---|
| Release payment before deliverable submission | Should fail — Approve requires Milestone.Status = SUBMITTED |
| Review before project completed | Should not be allowed |
| Rating = 0 or 6 | Should fail |
| ReviewerId = RevieweeId | Should fail |
| Duplicate review for same project/reviewer/reviewee | Should fail |
| Client requests revision | No wallet balance changes; Milestone → REVISION_REQUESTED |
| Client opens dispute | Payment status unchanged (stays RELEASED); Milestone/Project become `DISPUTED` |
| Non-admin resolves dispute | Should fail — `AdminPolicy` required |
| Resolve dispute expecting auto payment release/refund/split | Should NOT happen — only `ResolutionNote` is recorded, `Treasury` is never called |
| Fund milestone when Milestone.Status != CREATED | Should fail (also guards a fund/fund race via optimistic-concurrency on Milestone.Status) |
| Approve milestone while there is any active (`OPEN`/`UNDER_REVIEW`) dispute on it | Should fail |
| Non-owner Client accepts proposal | Should fail |
| Expert submits proposal to non-OPEN job | Should fail |
| Client funds milestone with insufficient balance for the deposit installment | Should fail |
| Client approves milestone with insufficient balance for the remaining installment | Should fail |
| Expert submits deliverable to project they do not own | Should fail |
| Expert tries to create/rename/delete a milestone step titled "Created"/"Funded"/"Completed" | Should fail — reserved system titles |

---

# 5. Full E2E Demo Script

Use this scenario for presentation and testing:

```text
1. Login as Client.
2. Create job:
   "Tôi muốn chatbot AI cho shop bán mỹ phẩm."
3. System refines requirement:
   - Better title
   - Clear description
   - Required skills
   - Budget
   - Timeline
   - Suggested milestones
4. Client publishes job.
5. System recommends experts.
6. Login as Expert.
7. Expert views OPEN job.
8. Expert submits proposal.
9. Login as Client.
10. Client accepts proposal.
11. System creates project.
12. Client confirms milestone.
13. Client tops up wallet (VNPay or demo deposit) if needed.
14. Client funds milestone → 30% deposit (default rate) transfers Client wallet → Expert
    wallet immediately, Payment row created RELEASED, Milestone → IN_PROGRESS.
15. Expert submits deliverable → Milestone → SUBMITTED (Deliverable.Status also SUBMITTED
    and stays that way — it never advances to APPROVED).
16. Client approves deliverable → remaining 70% released, minus 10% platform commission
    (second Payment row created RELEASED, commission WalletTransaction to platform
    system wallet).
17. Milestone becomes RELEASED.
18. Project becomes COMPLETED automatically (no manual "finish" action) once every
    milestone on it is settled.
19. Client reviews Expert.
20. Expert reviews Client.
```

Optional dispute demo:

```text
Expert submits deliverable
→ Client opens dispute
→ Milestone/Project become DISPUTED (Payment status is untouched — stays RELEASED,
  the deposit installment is already in Expert's wallet)
→ Admin reviews evidence, writes resolution note (PUT /disputes/{id}/resolve)
→ Dispute becomes RESOLVED, Milestone unlocks to SUBMITTED (deliverable was submitted)
  or IN_PROGRESS (if not)
→ No money moved by the resolve step itself — Client/Expert continue via the normal
  approve/request-revision actions once unlocked
```

---

# 6. Status Summary

## Job Status

```text
DRAFT → OPEN → IN_PROGRESS → COMPLETED
```

Optional:

```text
DRAFT / OPEN → CANCELLED / CLOSED
```

## Proposal Status

```text
SUBMITTED → SHORTLISTED
SUBMITTED / SHORTLISTED → ACCEPTED
SUBMITTED / SHORTLISTED → REJECTED
SUBMITTED → WITHDRAWN
```

## Project Status

```text
PENDING_PAYMENT → ACTIVE → IN_REVIEW → COMPLETED
```

Optional:

```text
ACTIVE / IN_REVIEW → DISPUTED
DISPUTED → ACTIVE / IN_REVIEW / COMPLETED / CANCELLED
```

## Milestone Status

Enum declares `CREATED, FUNDED, IN_PROGRESS, SUBMITTED, REVISION_REQUESTED, APPROVED, DISPUTED, COMPLETED, RELEASED, REFUNDED` — but only the path below is ever actually assigned by current code. `FUNDED`, `APPROVED`, `COMPLETED` are dead enum values, never set anywhere.

```text
CREATED → IN_PROGRESS → SUBMITTED → RELEASED
```

Revision path:

```text
SUBMITTED → REVISION_REQUESTED → SUBMITTED
```

Dispute path:

```text
IN_PROGRESS / SUBMITTED → DISPUTED
DISPUTED → SUBMITTED (if deliverable already submitted) / IN_PROGRESS (otherwise),
           via dispute resolve or close
RELEASED / REFUNDED only via a subsequent normal approve action — never automatic,
and REFUNDED specifically requires Treasury.RefundMilestoneAsync, which currently
has no caller anywhere in the codebase
```

## Payment Status

Enum declares `PENDING, HELD, RELEASED, REFUNDED, FROZEN, FAILED, PARTIALLY_RELEASED` — but the actual `Treasury` pipeline only ever creates rows directly as `RELEASED`. `HELD`, `FROZEN`, `PARTIALLY_RELEASED` are dead enum values in the current code path.

```text
NULL → RELEASED   (created RELEASED immediately, both for the deposit installment at
                    Fund time and the remaining installment at Approve time — two
                    separate Payment rows per milestone in the happy path)
```

Dispute path:

```text
No transition — Payment status is untouched while a dispute is OPEN/UNDER_REVIEW/RESOLVED/CLOSED.
REFUNDED is only reachable if Treasury.RefundMilestoneAsync is wired up in the future.
```

## Deliverable Status

Enum declares `SUBMITTED, APPROVED, REVISION_REQUESTED, REJECTED` — but only `SUBMITTED` is ever assigned. No code path advances a Deliverable past `SUBMITTED`, even when its milestone is approved, revised, or disputed.

```text
SUBMITTED   (terminal in practice — a new revision creates a NEW Deliverable row with
             RevisionNumber + 1, also starting at SUBMITTED, rather than mutating the old one)
```

## Dispute Status

```text
OPEN → UNDER_REVIEW → RESOLVED
OPEN → UNDER_REVIEW → CLOSED
OPEN → RESOLVED
OPEN → CLOSED
```

## Review Status

```text
Review is created only after project completion.
```

---

# 7. Main Database Mapping

| Business Part | Main Tables |
|---|---|
| User and roles | `Users`, `ClientProfiles`, `ExpertProfiles` |
| Skills and categories | `Categories`, `Skills`, `ExpertSkills`, `JobSkills` |
| Job creation | `JobPosts`, `JobSkills`, `JobPostMilestones`, `AIJobSuggestions` |
| Expert matching | `RecommendationResults`, `ExpertProfiles`, `ExpertSkills` |
| Expert verification | `ExpertVerifications` |
| Proposal (Path A) | `Proposals`, `ProposalMilestones` |
| Service catalog (Path B) | `Services`, `ServicePackages`, `ServiceFaqs` |
| Service request/offer (Path B) | `ServiceRequests`, `ServiceOffers`, `ServiceOfferMilestones` |
| Project creation | `Projects`, `Milestones` |
| Milestone tracking | `Milestones`, `MilestoneSteps` |
| Escrow/payment | `Wallets`, `Payments`, `WalletTransactions` |
| Deliverable | `Deliverables`, `Milestones` |
| Messaging | `Conversations`, `Messages` |
| Dispute | `Disputes`, `DisputeEvidences` |
| Review | `Reviews` |
| Notifications | `Notifications` |

---

# 8. Suggested API Mapping

## Flow 1 — Create Job & Match Expert

```text
POST /ai/job-assistant
POST /jobs
PUT /jobs/{id}
POST /jobs/{id}/publish
POST /jobs/{id}/recommendations/generate
GET /jobs/{id}/recommendations
GET /profiles/expert/{id}
```

## Flow 2 — Project Creation

### Path A — Proposal

```text
GET /jobs
GET /jobs/{id}
POST /jobs/{id}/proposals
GET /jobs/{id}/proposals
PUT /proposals/{id}/shortlist
PUT /proposals/{id}/reject
PUT /proposals/{id}/accept
GET /projects/{id}
```

### Path B — Service

```text
POST /services
PUT /services/{id}
POST /services/{id}/publish
POST /services/{id}/unpublish
GET /services
GET /services/mine
GET /services/{id}
POST /services/{id}/requests
GET /services/{id}/requests
GET /experts/me/service-requests
GET /clients/me/service-requests
GET /service-requests/{id}
POST /service-requests/{id}/accept
POST /service-requests/{id}/decline
POST /service-requests/{id}/offers
POST /service-offers/{id}/accept
GET /service-requests/{id}/offer
GET /projects/{id}
```

## Flow 3 — Milestone, Escrow & Deliverable

```text
GET /projects/{id}
POST /projects/{id}/milestones
POST /wallet/vnpay/deposit
PUT /milestones/{id}/fund
POST /milestones/{id}/deliverables
GET /milestones/{id}/deliverables
PUT /milestones/{id}/approve
PUT /milestones/{id}/request-revision
POST /milestones/{id}/dispute
```

## Flow 4 — Completion, Payment, and Review

```text
POST /reviews
GET /disputes
GET /disputes/{id}
PUT /disputes/{id}/resolve
PUT /disputes/{id}/close
```

Note:

```text
Payment release is normally triggered as part of PUT /milestones/{id}/approve — there is no separate manual release endpoint.
Resolving a dispute (PUT /disputes/{id}/resolve) only records an admin note — it never moves payment by itself.
```

Full request/response shapes for every endpoint above: see [`API_BY_FLOW.md`](./API_BY_FLOW.md).

---

# 9. Implementation Rules for AI Coding Agent

## Rule 1: AI is internal System behavior

Do not create an `AI` actor in use case diagrams or main flows.

Use:

```text
System helps clarify requirement.
System calculates recommendation.
```

Do not use:

```text
AI creates job.
AI chooses expert.
AI approves project.
```

## Rule 2: Accepting a proposal or a service offer must create a project

Path A — When Client accepts a proposal:

```text
Selected proposal → ACCEPTED
Other proposals → REJECTED
Job → IN_PROGRESS
Project → PENDING_PAYMENT
Milestones → CREATED
```

Path B — When Client accepts a service offer:

```text
Selected offer → ACCEPTED
Project → PENDING_PAYMENT
Milestones → CREATED
(no Job/Proposal involved)
```

## Rule 3: Payment must be safe

```text
The remaining installment cannot be released before the milestone reaches SUBMITTED
(i.e. a deliverable was submitted) — enforced by Treasury.PayRemainingAsync.
During revision, no Payment is created or changed — the deposit installment already
released stays released, nothing is "held".
During dispute, Payment status is NOT changed (there is no FROZEN transition in the
current code) — only Milestone/Project are locked from further money-moving actions.
Resolving a dispute does not by itself release/refund/split payment — that's a
separate follow-up action, and the Treasury methods that WOULD do a refund/split
(RefundMilestoneAsync, SplitMilestoneFundsAsync) currently have no caller at all.
```

## Rule 4: Project completion depends on milestones

```text
Project becomes COMPLETED only when all milestones are RELEASED.
```

## Rule 5: Review depends on completed project

```text
Client and Expert can review only after Project is COMPLETED.
```

## Rule 6: Review constraints

```text
Rating must be 1–5.
ReviewerId cannot equal RevieweeId.
Same reviewer cannot review same reviewee twice in the same project.
```

---

# 10. What Is Not Main Flow

These features can exist, but they are not part of the 4 final main flows:

```text
1. Advanced Analytics
2. Direct wallet transfer (Client → Expert, outside escrow)
3. Real-time chat
4. Complex notification system
5. Expert skill/certificate verification (KYC-adjacent)
```

> Note: Service Publishing was previously listed here — it has been promoted to Main Flow 2, Path B, since the full Service → Request → Offer → Project path is implemented in code.

They can be treated as secondary flows or future work.
