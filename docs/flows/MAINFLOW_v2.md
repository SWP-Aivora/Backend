# Aivora — Main Flow

> Purpose: This document is the canonical **main business flow** for Aivora.
> It is written for AI coding agents, backend developers, frontend developers, testers, and report writers.
> **v2** — supersedes `MAINFLOW_v1.md` (removed). Rewritten to match current code: dispute resolution no longer auto-moves money (issue #94), project renamed AITasker → Aivora, table names corrected.

---

## 0. Scope Lock

The core system flow is:

```text
Client đăng nhu cầu
→ Expert nhận job
→ Project được thực hiện
→ Thanh toán / review / dispute
```

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
| 2 | **Proposal & Project Creation** | Expert, Client, System | Expert submits a proposal, Client accepts one proposal, System creates a project |
| 3 | **Milestone, Escrow & Deliverable** | Client, Expert, System | Client and Expert confirm milestones, Client funds escrow, Expert submits deliverable, Client reviews result |
| 4 | **Completion, Payment, and Review** | Client, Expert, System, Admin | System releases payment, completes project, handles dispute if needed, and allows both sides to review |

Do not split these into 10–15 separate main flows.
Do not use an old 6-flow version as the main version.

---

## 2. End-to-End Summary

```text
1. Client creates a job and the System helps refine the requirement, suggest skills, budget, timeline, milestones, and recommend suitable experts.

2. Expert views the job, submits a proposal, and Client reviews proposals to select the most suitable expert. The System then creates a project from the accepted proposal.

3. Client and Expert confirm milestones. Client funds each milestone using escrow. Expert completes the work and submits deliverables. Client reviews the deliverable and either approves, requests revision, or opens a dispute.

4. If a dispute occurs, Admin only observes and documents — the platform does not decide the outcome. Admin marks the dispute resolved for the record, which unlocks the milestone; Client and Expert then settle it themselves through the normal milestone approve/revision flow. After all milestones are completed, the project is marked as completed and both Client and Expert can review each other.
```

One-line description:

```text
Aivora has four main business flows: job creation and expert matching, proposal and project creation, milestone-based delivery with escrow, and dispute review with project completion and peer review.
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

# MAIN FLOW 2: Proposal & Project Creation

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

# MAIN FLOW 3: Milestone, Escrow & Deliverable

## Actor chính

**Client**, **Expert**

## Internal system capability

**Milestone tracking, escrow, deliverable management**

## Goal

Client and Expert confirm project milestones.
Client funds a milestone using escrow.
Expert completes the work and submits deliverable, optionally tracked through granular **milestone steps**.
Client reviews the deliverable.

## Preconditions

```text
1. Project exists.
2. Project is linked to one accepted proposal.
3. Project has Client and Expert.
4. Client has wallet with sufficient balance (deposited via VNPay, or credited via demo deposit in dev/test).
5. Expert has wallet.
6. Milestone plan exists or can be created from proposal/job suggestion.
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

5. Client confirms milestone plan.

6. System creates milestones with status CREATED.

7. Project moves to PENDING_PAYMENT.

8. Client selects the first milestone to fund. Client tops up their wallet first if needed
   (VNPay redirect flow: POST /wallet/vnpay/deposit → pay on VNPay → IPN callback credits wallet;
   or a demo credit via POST /wallet/deposit-demo in dev/test).

9. Client clicks Fund Milestone.

10. System checks Client wallet balance.

11. If balance is enough:
    - System subtracts money from Client available balance
    - System holds money in escrow
    - Payment status becomes HELD
    - Milestone status becomes FUNDED
    - Project status becomes ACTIVE

12. Expert receives notification that milestone is funded.

13. Expert starts working, optionally tracking granular progress via milestone steps
    (PENDING → IN_PROGRESS → COMPLETED / SKIPPED / BLOCKED per step).

14. When finished, Expert submits deliverable:
    - Description
    - Demo URL
    - GitHub/source code link
    - File/document link
    - Setup instruction
    - Note to Client

15. System saves deliverable.

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

2. System marks deliverable as APPROVED.

3. System marks milestone as APPROVED.

4. System releases money from escrow to Expert.

5. Payment status becomes RELEASED.

6. Milestone status becomes RELEASED.

7. Expert earning increases.

8. If there are remaining milestones, Client continues funding the next milestone
   (Project status returns to ACTIVE).

9. If all milestones are RELEASED, Project becomes COMPLETED and Job becomes COMPLETED.
```

## Case B: Request Revision

```text
1. Client selects Request Revision.

2. Client enters revision reason.

3. System stores revision request.

4. Deliverable status becomes REVISION_REQUESTED.

5. Milestone status becomes REVISION_REQUESTED.

6. Project status returns to ACTIVE.

7. Payment remains HELD.

8. Expert fixes the deliverable.

9. Expert submits again.

10. Milestone returns to SUBMITTED.

11. Client reviews again.
```

## Case C: Open Dispute

```text
1. Client or Expert selects Open Dispute (from the milestone, or directly via POST /disputes).

2. User enters dispute reason and evidence if available.

3. System creates dispute (status = OPEN).

4. Milestone status becomes DISPUTED.

5. Payment status becomes FROZEN.

6. Project status becomes DISPUTED.

7. Admin receives notification to review the dispute.
```

## Status Transition

```text
Project:
CREATED → PENDING_PAYMENT → ACTIVE → IN_REVIEW → COMPLETED
or
ACTIVE / IN_REVIEW → DISPUTED
DISPUTED → ACTIVE / IN_REVIEW / COMPLETED / CANCELLED

Milestone:
CREATED → FUNDED → IN_PROGRESS → SUBMITTED → APPROVED → RELEASED

Milestone revision path:
SUBMITTED → REVISION_REQUESTED → SUBMITTED

Milestone dispute path:
SUBMITTED → DISPUTED
DISPUTED → RELEASED / REFUNDED / REVISION_REQUESTED (via a follow-up milestone action, not automatic)

Payment:
NULL → PENDING → HELD → RELEASED

Payment dispute path:
HELD → FROZEN → (stays FROZEN until resolved through a normal milestone action; dispute resolution itself does not move money)
```

## Main Tables

| Table | Purpose |
|---|---|
| `Projects` | Stores project lifecycle |
| `Milestones` | Stores project milestone status |
| `MilestoneSteps` | Stores granular per-milestone task tracking |
| `Payments` | Stores escrow/payment state |
| `Wallets` | Stores user wallet balance |
| `WalletTransactions` | Stores wallet movement logs |
| `Deliverables` | Stores expert submission |
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

When work is completed and approved, the System releases payment, marks milestone/project as completed, and allows both sides to review each other.

If a dispute occurs, Admin reviews the evidence and writes a resolution note for the record, then unlocks the milestone/project so Client and Expert can continue on their own. **The platform does not decide who is right or move any money** — Client (fund the milestone further, approve it) and Expert (resubmit, negotiate) resolve the disagreement themselves through the normal milestone actions once unlocked.

---

## 4A. Happy Path: Approved Deliverable → Released Payment → Completed Project → Reviews

### Preconditions

```text
1. Job status = IN_PROGRESS.
2. Project status = ACTIVE or IN_REVIEW.
3. Milestone status = SUBMITTED.
4. Deliverable status = SUBMITTED.
5. Payment status = HELD.
6. Client wallet has already funded the milestone.
7. Expert wallet can receive released payment.
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

Expected database result:

| Table | Field | Expected Value |
|---|---|---|
| `Deliverables` | `Status` | `APPROVED` |
| `Deliverables` | `ReviewedAt` | Not null |
| `Milestones` | `Status` | `APPROVED` |
| `Milestones` | `ApprovedAt` | Not null |

### Step 4.4 — System releases payment

Approving a deliverable and releasing its payment happen in the **same atomic transaction** (`PUT /milestones/{id}/approve`) — there is no separate release-payment endpoint.

Expected database result:

| Table | Field | Expected Value |
|---|---|---|
| `Payments` | `Status` | `RELEASED` |
| `Payments` | `ReleasedAt` | Not null |
| `Milestones` | `Status` | `RELEASED` |
| `Milestones` | `PaidAt` | Not null |
| `WalletTransactions` | `Type` | `PAYMENT_RELEASE` |
| `WalletTransactions` | `Direction` | `CREDIT` for Expert, `DEBIT` for Client |

Example wallet result:

```text
Client initial available balance = 2000
Milestone amount = 900
Expert initial available balance = 0
```

After payment release:

| Wallet | Available Balance | Held Balance | Total Earned |
|---|---:|---:|---:|
| Client | `1100` | `0` | unchanged |
| Expert | `900` | `0` | `900` |

### Step 4.5 — System completes project

System checks:

```text
If all milestones are RELEASED, mark project as COMPLETED.
```

Expected database result:

| Table | Field | Expected Value |
|---|---|---|
| `Projects` | `Status` | `COMPLETED` |
| `Projects` | `CompletedAt` | Not null |
| `JobPosts` | `Status` | `COMPLETED` |

System emits `JobStatusUpdated` (status=completed) to the Client over SignalR.

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
| `JobPosts.Status` | `COMPLETED` |
| `Projects.Status` | `COMPLETED` |
| `Milestones.Status` | `RELEASED` |
| `Deliverables.Status` | `APPROVED` |
| `Payments.Status` | `RELEASED` |
| `Reviews` | 2 reviews created |
| Expert wallet | Received payment |
| Expert profile | Completed project count increased |

---

## Acceptance Criteria

```text
1. Expert can submit deliverable.
2. Client can approve deliverable.
3. Payment is released only after approval, in the same transaction as the approval.
4. Milestone changes to RELEASED after payment release.
5. Project changes to COMPLETED after all milestones are released.
6. Client can review Expert.
7. Expert can review Client.
8. Review rating must be between 1 and 5.
9. User cannot review themselves.
10. Same reviewer cannot review the same reviewee twice for the same project.
11. If Client requests revision, payment must remain HELD.
12. If Client/Expert opens dispute, payment must become FROZEN and project must become DISPUTED.
13. Admin resolving a dispute only records a ResolutionNote — it does not itself release, refund, or split payment.
```

---

## Important Negative Test Cases

| Test Case | Expected Result |
|---|---|
| Release payment before deliverable approval | Should fail — no standalone release endpoint exists |
| Review before project completed | Should not be allowed |
| Rating = 0 or 6 | Should fail |
| ReviewerId = RevieweeId | Should fail |
| Duplicate review for same project/reviewer/reviewee | Should fail |
| Client requests revision | Payment should stay `HELD` |
| Client opens dispute | Payment should become `FROZEN`, project should become `DISPUTED` |
| Non-admin resolves dispute | Should fail — `AdminPolicy` required |
| Resolve dispute expecting auto payment release | Should NOT happen — only `ResolutionNote` is recorded |
| Non-owner Client accepts proposal | Should fail |
| Expert submits proposal to non-OPEN job | Should fail |
| Client funds milestone with insufficient balance | Should fail |
| Expert submits deliverable to project they do not own | Should fail |

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
14. Client funds milestone.
15. Payment is held in escrow.
16. Expert submits deliverable.
17. Client approves deliverable.
18. System releases payment (same transaction as approval).
19. Milestone becomes RELEASED.
20. Project becomes COMPLETED.
21. Client reviews Expert.
22. Expert reviews Client.
```

Optional dispute demo:

```text
Expert submits deliverable
→ Client opens dispute
→ Payment becomes FROZEN
→ Admin reviews evidence, writes resolution note
→ Dispute becomes RESOLVED (payment still FROZEN — needs a follow-up milestone action to move)
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

```text
CREATED → FUNDED → IN_PROGRESS → SUBMITTED → APPROVED → RELEASED
```

Revision path:

```text
SUBMITTED → REVISION_REQUESTED → SUBMITTED
```

Dispute path:

```text
SUBMITTED → DISPUTED
DISPUTED → RELEASED / REFUNDED / REVISION_REQUESTED (follow-up action, not automatic)
```

## Payment Status

```text
PENDING → HELD → RELEASED
```

Dispute/refund path:

```text
HELD → FROZEN → RELEASED / REFUNDED / PARTIALLY_RELEASED (via follow-up milestone action)
```

## Deliverable Status

```text
SUBMITTED → APPROVED
SUBMITTED → REVISION_REQUESTED → SUBMITTED
SUBMITTED → REJECTED
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
| Proposal | `Proposals`, `ProposalMilestones` |
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

## Flow 2 — Proposal & Project Creation

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

## Rule 2: Accept proposal must create project

When Client accepts a proposal:

```text
Selected proposal → ACCEPTED
Other proposals → REJECTED
Job → IN_PROGRESS
Project → PENDING_PAYMENT
Milestones → CREATED
```

## Rule 3: Payment must be safe

```text
Payment cannot be RELEASED before deliverable is approved.
Payment must stay HELD during revision.
Payment must become FROZEN during dispute.
Resolving a dispute does not by itself release/refund payment — that's a separate follow-up action.
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
1. Service Publishing (AI Service Generator for Experts)
2. Advanced Analytics
3. Direct wallet transfer (Client → Expert, outside escrow)
4. Real-time chat
5. Complex notification system
6. Expert skill/certificate verification (KYC-adjacent)
```

They can be treated as secondary flows or future work.
