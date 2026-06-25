# AITasker — Main Flow

> Purpose: This document is the canonical **main business flow** for AITasker.  
> It is written for AI coding agents, backend developers, frontend developers, testers, and report writers.

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

AITasker should be analyzed as a marketplace + project delivery system, not as an “AI actor” system.

---

## 1. Final Main Flow List

AITasker has exactly **4 main business flows**:

| No. | Main Flow | Main Actors | Purpose |
|---:|---|---|---|
| 1 | **Create Job & Match Expert** | Client, System, Expert | Client creates a job, System refines requirements and recommends suitable experts |
| 2 | **Proposal & Project Creation** | Expert, Client, System | Expert submits a proposal, Client accepts one proposal, System creates a project |
| 3 | **Milestone, Escrow & Deliverable** | Client, Expert, System | Client and Expert confirm milestones, Client funds escrow, Expert submits deliverable, Client reviews result |
| 4 | **Completion, Payment, and Review** | Client, Expert, System, Admin | System releases payment, completes project, handles dispute if needed, and allows both sides to review |

Do not split these into 10–15 separate main flows.  
Do not use the old 6-flow version as the main version.

---

## 2. End-to-End Summary

```text
1. Client creates a job and the System helps refine the requirement, suggest skills, budget, timeline, milestones, and recommend suitable experts.

2. Expert views the job, submits a proposal, and Client reviews proposals to select the most suitable expert. The System then creates a project from the accepted proposal.

3. Client and Expert confirm milestones. Client funds each milestone using escrow. Expert completes the work and submits deliverables. Client reviews the deliverable and either approves, requests revision, or opens a dispute.

4. If a dispute occurs, Admin reviews evidence and resolves payment. After all milestones are completed, the project is marked as completed and both Client and Expert can review each other.
```

One-line description:

```text
AITasker has four main business flows: job creation and expert matching, proposal and project creation, milestone-based delivery with escrow, and dispute resolution with project completion and review.
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
Proposal: NULL → SUBMITTED → ACCEPTED / REJECTED

Job: OPEN → IN_PROGRESS

Project: NULL → CREATED
```

Recommended implementation status:

```text
Project: NULL → PENDING_PAYMENT
```

## Main Tables

| Table | Purpose |
|---|---|
| `JobPosts` | Source job data |
| `Proposals` | Stores expert proposal |
| `ProposalMilestones` | Stores milestone plan proposed by expert |
| `Projects` | Created after proposal acceptance |
| `Milestones` | May be generated from proposal milestones |
| `Conversations` | Optional conversation between Client and Expert |
| `Messages` | Optional message history |

## Transaction Rule

Accepting a proposal must be atomic.

```text
Accept proposal transaction:
1. Validate job/proposal/client/expert.
2. Set selected proposal = ACCEPTED.
3. Set sibling proposals = REJECTED.
4. Set job = IN_PROGRESS.
5. Create project.
6. Create initial milestone plan if applicable.
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

Before this flow, AITasker is only a place to post jobs and find experts.  
After this flow, AITasker starts managing real project delivery.

---

# MAIN FLOW 3: Milestone, Escrow & Deliverable

## Actor chính

**Client**, **Expert**

## Internal system capability

**Milestone tracking, escrow, deliverable management**

## Goal

Client and Expert confirm project milestones.  
Client funds a milestone using escrow.  
Expert completes the work and submits deliverable.  
Client reviews the deliverable.

## Preconditions

```text
1. Project exists.
2. Project is linked to one accepted proposal.
3. Project has Client and Expert.
4. Client has wallet with sufficient balance (deposited via VNPay Sandbox).
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
   - Deliverable type

5. Client confirms milestone plan.

6. System creates milestones with status CREATED.

7. Project moves to PENDING_PAYMENT.

8. Client selects the first milestone to fund.

9. Client clicks Fund Milestone.

10. System checks Client wallet balance.

11. If balance is enough:
    - System subtracts money from Client available balance
    - System holds money in escrow
    - Payment status becomes HELD
    - Milestone status becomes FUNDED
    - Project status becomes ACTIVE

12. Expert receives notification that milestone is funded.

13. Expert starts working.

14. When finished, Expert submits deliverable:
    - Description
    - Demo URL
    - GitHub/source code link
    - File/document link
    - Setup instruction
    - Note to Client

15. System saves deliverable.

16. Milestone status becomes SUBMITTED.

17. Project status may become IN_REVIEW.

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

8. If there are remaining milestones, Client continues funding the next milestone.

9. If all milestones are RELEASED, Project becomes COMPLETED.
```

## Case B: Request Revision

```text
1. Client selects Request Revision.

2. Client enters revision reason.

3. System stores revision request.

4. Deliverable status becomes REVISION_REQUESTED.

5. Milestone status becomes REVISION_REQUESTED.

6. Payment remains HELD.

7. Expert fixes the deliverable.

8. Expert submits again.

9. Milestone returns to SUBMITTED.

10. Client reviews again.
```

## Case C: Open Dispute

```text
1. Client or Expert selects Open Dispute.

2. User enters dispute reason and evidence if available.

3. System creates dispute.

4. Milestone status becomes DISPUTED.

5. Payment status becomes FROZEN.

6. Project status becomes DISPUTED.

7. Admin receives notification to resolve the dispute.
```

## Status Transition

```text
Project:
CREATED → PENDING_PAYMENT → ACTIVE → IN_REVIEW → COMPLETED
or
ACTIVE / IN_REVIEW → DISPUTED

Milestone:
CREATED → FUNDED → SUBMITTED → APPROVED → RELEASED

Milestone revision path:
SUBMITTED → REVISION_REQUESTED → SUBMITTED

Milestone dispute path:
SUBMITTED → DISPUTED

Payment:
NULL → PENDING → HELD → RELEASED

Payment dispute path:
HELD → FROZEN
```

## Main Tables

| Table | Purpose |
|---|---|
| `Projects` | Stores project lifecycle |
| `Milestones` | Stores project milestone status |
| `Payments` | Stores escrow/payment state |
| `Wallets` | Stores user wallet balance |
| `WalletTransactions` | Stores wallet movement logs |
| `Deliverables` | Stores expert submission |
| `Disputes` | Created only if dispute is opened |
| `DisputeEvidence` | Stores dispute evidence if available |

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

This flow proves that AITasker is not only a job board.  
It manages project delivery, milestone validation, and payment safety.

---

# MAIN FLOW 4: Completion, Payment, and Review

## Actor chính

**Client**, **Expert**, **System**

## Actor phụ

**Admin**  
Admin participates only when a dispute occurs.

## Goal

When work is completed and approved, the System releases payment, marks milestone/project as completed, and allows both sides to review each other.

If a dispute occurs, Admin reviews evidence and resolves payment before the project can continue or close.

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

After deliverable approval, System releases escrow payment to Expert.

Expected database result:

| Table | Field | Expected Value |
|---|---|---|
| `Payments` | `Status` | `RELEASED` |
| `Payments` | `ReleasedAt` | Not null |
| `Milestones` | `Status` | `RELEASED` |
| `Milestones` | `PaidAt` | Not null |
| `WalletTransactions` | `Type` | `PAYMENT_RELEASE` |
| `WalletTransactions` | `Direction` | `CREDIT` for Expert |

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

## 4B. Dispute Path: Dispute → Admin Resolution → Payment Decision

### Trigger

```text
Client or Expert opens a dispute from a submitted milestone/deliverable.
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

3. System creates Dispute.

4. System updates:
   - Milestone status = DISPUTED
   - Payment status = FROZEN
   - Project status = DISPUTED

5. Admin opens dispute detail.

6. Admin reviews:
   - Job description
   - Proposal
   - Milestone acceptance criteria
   - Deliverable
   - Message history if available
   - Payment status
   - Evidence from both sides

7. Admin chooses resolution:
   A. RELEASE_TO_EXPERT
   B. REFUND_TO_CLIENT
   C. SPLIT_PAYMENT
   D. REQUEST_REVISION

8. System updates dispute, payment, milestone, and project status based on Admin decision.

9. Client and Expert receive result notification.
```

### Resolution Outcomes

| Admin Decision | Payment Result | Milestone Result | Project Result |
|---|---|---|---|
| `RELEASE_TO_EXPERT` | `RELEASED` | `RELEASED` | Continue or `COMPLETED` if all milestones released |
| `REFUND_TO_CLIENT` | `REFUNDED` | `REFUNDED` | Continue, cancel, or close depending on business rule |
| `SPLIT_PAYMENT` | `PARTIALLY_RELEASED` | Resolved according to split rule | Continue or close |
| `REQUEST_REVISION` | Remains `HELD` | `REVISION_REQUESTED` | Back to active/review cycle |

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
3. Payment is released only after approval.
4. Milestone changes to RELEASED after payment release.
5. Project changes to COMPLETED after all milestones are released.
6. Client can review Expert.
7. Expert can review Client.
8. Review rating must be between 1 and 5.
9. User cannot review themselves.
10. Same reviewer cannot review the same reviewee twice for the same project.
11. If Client requests revision, payment must remain HELD.
12. If Client/Expert opens dispute, payment must become FROZEN and project must become DISPUTED.
13. Admin can resolve dispute with release, refund, split payment, or revision request.
```

---

## Important Negative Test Cases

| Test Case | Expected Result |
|---|---|
| Release payment before deliverable approval | Should fail |
| Review before project completed | Should not be allowed |
| Rating = 0 or 6 | Should fail |
| ReviewerId = RevieweeId | Should fail |
| Duplicate review for same project/reviewer/reviewee | Should fail |
| Client requests revision | Payment should stay `HELD` |
| Client opens dispute | Payment should become `FROZEN`, project should become `DISPUTED` |
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
13. Client funds milestone.
14. Payment is held in escrow.
15. Expert submits deliverable.
16. Client approves deliverable.
17. System releases payment.
18. Milestone becomes RELEASED.
19. Project becomes COMPLETED.
20. Client reviews Expert.
21. Expert reviews Client.
```

Optional dispute demo:

```text
Expert submits deliverable
→ Client opens dispute
→ Payment becomes FROZEN
→ Admin reviews evidence
→ Admin resolves dispute
```

---

# 6. Status Summary

## Job Status

```text
DRAFT → OPEN → IN_PROGRESS → COMPLETED
```

Optional:

```text
OPEN → CANCELLED / CLOSED
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
CREATED → FUNDED → SUBMITTED → APPROVED → RELEASED
```

Revision path:

```text
SUBMITTED → REVISION_REQUESTED → SUBMITTED
```

Dispute path:

```text
SUBMITTED → DISPUTED
DISPUTED → RELEASED / REFUNDED / REVISION_REQUESTED
```

## Payment Status

```text
PENDING → HELD → RELEASED
```

Dispute/refund path:

```text
HELD → FROZEN → RELEASED / REFUNDED / PARTIALLY_RELEASED
```

## Deliverable Status

```text
SUBMITTED → APPROVED
SUBMITTED → REVISION_REQUESTED → SUBMITTED
SUBMITTED → REJECTED
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
| Job creation | `JobPosts`, `JobSkills`, `AIJobSuggestions` |
| Expert matching | `RecommendationResults`, `ExpertProfiles`, `ExpertSkills` |
| Proposal | `Proposals`, `ProposalMilestones` |
| Project creation | `Projects`, `Milestones` |
| Escrow/payment | `Wallets`, `Payments`, `WalletTransactions` |
| Deliverable | `Deliverables`, `Milestones` |
| Messaging | `Conversations`, `Messages` |
| Dispute | `Disputes`, `DisputeEvidence` |
| Review | `Reviews` |

---

# 8. Suggested API Mapping

## Flow 1 — Create Job & Match Expert

```text
POST /jobs
POST /ai/job-assistant
PUT /jobs/{id}
POST /jobs/{id}/publish
GET /api/v1/jobs/{id}/recommendations
GET /experts/{id}
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
PUT /milestones/{id}/fund
POST /milestones/{id}/deliverables
GET /milestones/{id}/deliverables
PUT /api/v1/milestones/{id}/approve
PUT /api/v1/milestones/{id}/request-revision
POST /milestones/{id}/dispute
```

## Flow 4 — Completion, Payment, and Review

```text
POST /api/v1/reviews
GET /api/v1/disputes
GET /api/v1/disputes/{id}
PUT /api/v1/disputes/{id}/resolve
```

Note:

```text
Payment release should normally be triggered by deliverable approval,
not manually called by Client.
```

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
1. Service Publishing
2. AI Service Generator
3. Advanced Analytics
4. Withdraw real money
5. Real-time chat
6. Complex notification system
7. KYC / identity verification
```

They can be treated as secondary flows or future work.
