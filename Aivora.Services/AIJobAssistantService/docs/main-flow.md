Core cá»§a há»‡ thá»‘ng lÃ : **Client Ä‘Äƒng nhu cáº§u â†’ Expert nháº­n job â†’ Project Ä‘Æ°á»£c thá»±c hiá»‡n â†’ Thanh toÃ¡n/review/dispute**. AI chá»‰ náº±m bÃªn trong System Ä‘á»ƒ há»— trá»£ requirement vÃ  matching, khÃ´ng pháº£i actor riÃªng.

---

# MAIN FLOW 1: Create Job & Match Expert

## Actor chÃ­nh

**Client**

## Actor phá»¥

**Expert**

## Má»¥c tiÃªu

Client táº¡o bÃ i Ä‘Äƒng thuÃª expert AI. System há»— trá»£ lÃ m rÃµ requirement, gá»£i Ã½ skill/budget/milestone vÃ  recommend expert phÃ¹ há»£p.

## Main flow

```text
1. Client Ä‘Äƒng nháº­p vÃ o há»‡ thá»‘ng.

2. Client chá»n Create Job.

3. Client nháº­p nhu cáº§u ban Ä‘áº§u.
   VÃ­ dá»¥:
   "TÃ´i muá»‘n lÃ m chatbot AI cho shop má»¹ pháº©m."

4. Client nháº­p thÃªm thÃ´ng tin cÆ¡ báº£n:
   - LÄ©nh vá»±c kinh doanh
   - Má»¥c tiÃªu mong muá»‘n
   - Budget dá»± kiáº¿n
   - Timeline dá»± kiáº¿n
   - Dá»¯ liá»‡u hiá»‡n cÃ³
   - NgÃ´n ngá»¯ cáº§n há»— trá»£

5. System há»— trá»£ lÃ m rÃµ requirement:
   - Viáº¿t láº¡i job description rÃµ rÃ ng hÆ¡n
   - Gá»£i Ã½ required skills
   - Gá»£i Ã½ budget há»£p lÃ½
   - Gá»£i Ã½ timeline
   - Gá»£i Ã½ milestone
   - ÄÆ°a ra cÃ¢u há»i cáº§n lÃ m rÃµ thÃªm

6. Client xem láº¡i ná»™i dung Ä‘Æ°á»£c system gá»£i Ã½.

7. Client chá»‰nh sá»­a náº¿u cáº§n.

8. Client publish job.

9. System lÆ°u job vá»›i status OPEN.

10. System tÃ­nh toÃ¡n expert phÃ¹ há»£p dá»±a trÃªn:
    - Skill match
    - Portfolio
    - Rating
    - Budget fit
    - Availability
    - Completed projects

11. System hiá»ƒn thá»‹ danh sÃ¡ch recommended experts cho Client.

12. Expert cÃ³ thá»ƒ xem job Ä‘ang OPEN trÃªn marketplace.
```

## Status chÃ­nh

```text
Job: NULL â†’ DRAFT / OPEN
```

## Ã nghÄ©a

Flow nÃ y gá»™p 3 pháº§n trÆ°á»›c Ä‘Ã³:

```text
Create Job
+ AI-assisted Requirement
+ Expert Recommendation
```

ÄÃ¢y lÃ  flow Ä‘áº§u vÃ o cá»§a há»‡ thá»‘ng. Náº¿u khÃ´ng cÃ³ job thÃ¬ cÃ¡c flow sau khÃ´ng cháº¡y Ä‘Æ°á»£c.

---

# MAIN FLOW 2: Proposal & Project Creation

## Actor chÃ­nh

**Expert**, **Client**

## Má»¥c tiÃªu

Expert gá»­i proposal cho job. Client xem proposal, chá»n expert phÃ¹ há»£p vÃ  system táº¡o project.

## Main flow

```text
1. Expert Ä‘Äƒng nháº­p vÃ o há»‡ thá»‘ng.

2. Expert vÃ o trang Find Jobs.

3. Expert xem danh sÃ¡ch job Ä‘ang OPEN.

4. Expert chá»n má»™t job phÃ¹ há»£p.

5. System hiá»ƒn thá»‹ chi tiáº¿t job:
   - Title
   - Description
   - Required skills
   - Budget
   - Timeline
   - Suggested milestones
   - Client information

6. Expert chá»n Submit Proposal.

7. Expert nháº­p proposal:
   - Cover letter
   - Proposed budget
   - Proposed timeline
   - Proposed milestones
   - Portfolio/demo link náº¿u cÃ³

8. Expert submit proposal.

9. System kiá»ƒm tra proposal há»£p lá»‡.

10. System lÆ°u proposal vá»›i status SUBMITTED.

11. Client nháº­n notification cÃ³ proposal má»›i.

12. Client vÃ o job cá»§a mÃ¬nh Ä‘á»ƒ xem danh sÃ¡ch proposal.

13. Client so sÃ¡nh proposal theo:
    - Match score
    - Rating cá»§a expert
    - Proposed budget
    - Timeline
    - Portfolio
    - Ná»™i dung proposal

14. Client cÃ³ thá»ƒ:
    - Reject proposal
    - Shortlist proposal
    - Message expert
    - Accept proposal

15. Client chá»n Accept Proposal.

16. System kiá»ƒm tra:
    - Job cÃ²n OPEN
    - Proposal há»£p lá»‡
    - Expert cÃ²n active
    - Client lÃ  chá»§ job

17. System cáº­p nháº­t proposal Ä‘Æ°á»£c chá»n thÃ nh ACCEPTED.

18. System cáº­p nháº­t cÃ¡c proposal cÃ²n láº¡i thÃ nh REJECTED.

19. System cáº­p nháº­t Job thÃ nh IN_PROGRESS.

20. System táº¡o Project tá»«:
    - Job
    - Accepted proposal
    - Client
    - Expert
    - Budget
    - Timeline
    - Proposed milestones

21. Client vÃ  Expert Ä‘Æ°á»£c chuyá»ƒn sang giai Ä‘oáº¡n quáº£n lÃ½ project.
```

## Status chÃ­nh

```text
Proposal: NULL â†’ SUBMITTED â†’ ACCEPTED / REJECTED

Job: OPEN â†’ IN_PROGRESS

Project: NULL â†’ CREATED
```

## Ã nghÄ©a

Flow nÃ y gá»™p 2 pháº§n:

```text
Submit Proposal
+ Client Accept Proposal
+ Create Project
```

ÄÃ¢y lÃ  Ä‘iá»ƒm chuyá»ƒn tá»« **marketplace phase** sang **project execution phase**.

TrÆ°á»›c flow nÃ y, há»‡ thá»‘ng chá»‰ lÃ  nÆ¡i Ä‘Äƒng job vÃ  tÃ¬m expert.
Sau flow nÃ y, há»‡ thá»‘ng báº¯t Ä‘áº§u quáº£n lÃ½ project tháº­t.

---

# MAIN FLOW 3: Milestone, Escrow & Deliverable

## Actor chÃ­nh

**Client**, **Expert**

## Má»¥c tiÃªu

Client vÃ  Expert xÃ¡c nháº­n milestone. Client fund tiá»n vÃ o escrow giáº£ láº­p. Expert lÃ m viá»‡c vÃ  submit deliverable. Client review káº¿t quáº£.

## Main flow

```text
1. Sau khi project Ä‘Æ°á»£c táº¡o, Client má»Ÿ Project Detail.

2. System hiá»ƒn thá»‹ milestone plan ban Ä‘áº§u.
   Milestone cÃ³ thá»ƒ láº¥y tá»«:
   - Milestone do system gá»£i Ã½ khi táº¡o job
   - Milestone do Expert Ä‘á» xuáº¥t trong proposal
   - Milestone do Client chá»‰nh sá»­a láº¡i

3. Client vÃ  Expert xem milestone plan.

4. Má»—i milestone gá»“m:
   - Title
   - Description
   - Amount
   - Due date
   - Acceptance criteria
   - Deliverable type

5. Client xÃ¡c nháº­n milestone plan.

6. System táº¡o cÃ¡c milestone vá»›i status CREATED.

7. Project chuyá»ƒn sang PENDING_PAYMENT.

8. Client chá»n milestone Ä‘áº§u tiÃªn Ä‘á»ƒ fund.

9. Client báº¥m Fund Milestone.

10. System kiá»ƒm tra balance cá»§a Client.

11. Náº¿u Ä‘á»§ tiá»n:
    - System trá»« tiá»n khá»i Client balance
    - System giá»¯ tiá»n trong escrow
    - Payment chuyá»ƒn sang HELD
    - Milestone chuyá»ƒn sang FUNDED
    - Project chuyá»ƒn sang ACTIVE

12. Expert nháº­n thÃ´ng bÃ¡o milestone Ä‘Ã£ Ä‘Æ°á»£c fund.

13. Expert báº¯t Ä‘áº§u thá»±c hiá»‡n cÃ´ng viá»‡c.

14. Khi hoÃ n thÃ nh, Expert submit deliverable:
    - Description
    - Demo URL
    - GitHub link
    - File/document link
    - Setup instruction
    - Note cho Client

15. System lÆ°u deliverable.

16. Milestone chuyá»ƒn sang SUBMITTED.

17. Client nháº­n thÃ´ng bÃ¡o cÃ³ deliverable cáº§n review.

18. Client má»Ÿ deliverable vÃ  so sÃ¡nh vá»›i acceptance criteria.

19. Client chá»n má»™t trong ba hÆ°á»›ng:
    A. Approve
    B. Request Revision
    C. Open Dispute
```

## Case A: Approve

```text
1. Client chá»n Approve.

2. System release tiá»n tá»« escrow cho Expert.

3. Payment chuyá»ƒn sang RELEASED.

4. Milestone chuyá»ƒn sang PAID.

5. Expert earning tÄƒng.

6. Náº¿u cÃ²n milestone khÃ¡c, Client tiáº¿p tá»¥c fund milestone tiáº¿p theo.

7. Náº¿u táº¥t cáº£ milestone Ä‘Ã£ PAID, Project chuyá»ƒn sang COMPLETED.
```

## Case B: Request Revision

```text
1. Client chá»n Request Revision.

2. Client nháº­p lÃ½ do cáº§n sá»­a.

3. System lÆ°u revision request.

4. Milestone chuyá»ƒn sang REVISION_REQUESTED.

5. Payment váº«n giá»¯ HELD.

6. Expert sá»­a deliverable.

7. Expert submit láº¡i.

8. Milestone quay láº¡i SUBMITTED.

9. Client review láº¡i.
```

## Case C: Open Dispute

```text
1. Client hoáº·c Expert chá»n Open Dispute.

2. User nháº­p lÃ½ do tranh cháº¥p vÃ  báº±ng chá»©ng náº¿u cÃ³.

3. System táº¡o dispute.

4. Milestone chuyá»ƒn sang DISPUTED.

5. Payment chuyá»ƒn sang FROZEN.

6. Admin Ä‘Æ°á»£c thÃ´ng bÃ¡o Ä‘á»ƒ xá»­ lÃ½.
```

## Status chÃ­nh

```text
Project: CREATED â†’ PENDING_PAYMENT â†’ ACTIVE â†’ COMPLETED

Milestone:
CREATED â†’ FUNDED â†’ SUBMITTED â†’ PAID
hoáº·c
SUBMITTED â†’ REVISION_REQUESTED â†’ SUBMITTED
hoáº·c
SUBMITTED â†’ DISPUTED

Payment:
NULL â†’ HELD â†’ RELEASED
hoáº·c
HELD â†’ FROZEN
```

## Ã nghÄ©a

Flow nÃ y lÃ  pháº§n nghiá»‡p vá»¥ quan trá»ng nháº¥t cá»§a project.

NÃ³ gá»™p:

```text
Confirm Milestones
+ Fund Escrow
+ Submit Deliverable
+ Review Deliverable
+ Approve / Revision / Dispute
```

ÄÃ¢y lÃ  flow chá»©ng minh AITasker khÃ´ng chá»‰ lÃ  nÆ¡i Ä‘Äƒng job, mÃ  cÃ²n quáº£n lÃ½ toÃ n bá»™ quÃ¡ trÃ¬nh lÃ m viá»‡c vÃ  thanh toÃ¡n.

---

# MAIN FLOW 4: Completion, Payment, and Review
Below is **1 end-to-end business flow test** for your requirement. I wrote it so the whole team can use it, but I made **section 4: Completion, Payment, and Review** more detailed because that is your part.

---

# E2E Business Flow Test: AI Tasker Project Completion

## Scenario

A client wants to hire an AI expert to build a chatbot for a beauty shop website. The client creates a job, an expert applies, the project is managed through milestones, then the work is submitted, approved, paid, and reviewed.

---

## Actors

| Role                        | Person in team | System actor             |
| --------------------------- | -------------: | ------------------------ |
| Client Create Job           |          Khang | Client                   |
| Expert Applies              |           QAnh | Expert                   |
| Project Management          |           Khoa | Client + Expert          |
| Completion, Payment, Review |           QuÃ¢n | Client + Expert + System |

---

# 1. Client Create Job â€” Khang

## Test objective

Verify that the client can create and publish a job.

## Test steps

1. Client logs in.
2. Client creates a job post:

   * Title: `Build AI Chatbot for Beauty Shop`
   * Description: `Need chatbot to answer product questions and recommend skincare products`
   * Budget type: `FIXED`
   * Budget min: `800`
   * Budget max: `1000`
   * Currency: Coin`
   * Skills: `OpenAI API`, `Chatbot`, `React`
3. Client publishes the job.

## Expected result

| Table             | Expected data                 |
| ----------------- | ----------------------------- |
| `JobPosts`        | New job created               |
| `JobPosts.Status` | `OPEN`                        |
| `JobSkills`       | Required skills linked to job |

---

# 2. Expert Applies for Job â€” QAnh

## Test objective

Verify that an expert can submit a proposal.

## Test steps

1. Expert views open jobs.
2. Expert opens `Build AI Chatbot for Beauty Shop`.
3. Expert submits proposal:

   * Proposed budget: `900 Coin`
   * Timeline: `14 days`
   * Cover letter: `I can build this chatbot using OpenAI API and React.`
4. Client accepts the proposal.

## Expected result

| Table              | Expected data        |
| ------------------ | -------------------- |
| `Proposals`        | New proposal created |
| `Proposals.Status` | `ACCEPTED`           |
| `Projects`         | New project created  |
| `Projects.Status`  | `PENDING_PAYMENT`    |

---

# 3. Project Management â€” Khoa

## Test objective

Verify that the accepted proposal becomes an active project with funded milestone.

## Test steps

1. System creates project from accepted proposal.
2. System creates milestone:

   * Title: `Chatbot MVP Delivery`
   * Amount: `900 USD`
   * Acceptance criteria: `Chatbot can answer FAQ, recommend products, and provide demo URL`
3. Client funds milestone.
4. System holds payment in escrow.
5. Project becomes active.

## Expected result

| Table               | Expected data                                              |
| ------------------- | ---------------------------------------------------------- |
| `Projects.Status`   | `ACTIVE`                                                   |
| `Milestones.Status` | `IN_PROGRESS` or `FUNDED`                                  |
| `Payments.Status`   | `HELD`                                                     |
| `Wallets`           | Client available balance decreases, held balance increases |

---

# 4. Completion, Payment, and Review â€” QuÃ¢n

## Main test objective

Verify that when the expert completes the work, the client can approve it, the system releases payment, the project becomes completed, and both sides can review each other.

---

## Preconditions

Before your part starts, the system should already have:

| Item          | Required status                |
| ------------- | ------------------------------ |
| Job           | `IN_PROGRESS`                  |
| Project       | `ACTIVE`                       |
| Milestone     | `IN_PROGRESS` or `FUNDED`      |
| Payment       | `HELD`                         |
| Client wallet | Has enough funded/held balance |
| Expert wallet | Can receive released payment   |

---

## Step 4.1 â€” Expert submits deliverable

### Action

Expert submits final work for milestone.

Example deliverable:

```text
Demo URL: https://demo.beauty-chatbot.com
Source Code URL: https://github.com/demo/beauty-chatbot
Note: Chatbot MVP completed with FAQ, product recommendation, and admin prompt config.
```

### Expected database result

| Table          | Field            | Expected value |
| -------------- | ---------------- | -------------- |
| `Deliverables` | `Status`         | `SUBMITTED`    |
| `Deliverables` | `RevisionNumber` | `1`            |
| `Milestones`   | `Status`         | `SUBMITTED`    |
| `Milestones`   | `SubmittedAt`    | Not null       |
| `Projects`     | `Status`         | `IN_REVIEW`    |

---

## Step 4.2 â€” Client reviews submitted work

### Action

Client opens the submitted deliverable and checks:

```text
1. Chatbot answers beauty product questions.
2. Chatbot can recommend skincare products.
3. Demo URL works.
4. Source code is provided.
5. Work matches acceptance criteria.
```

### Expected result

Client sees the deliverable details and can choose:

```text
Approve
Request Revision
Open Dispute
```

For this happy path test, client chooses **Approve**.

---

## Step 4.3 â€” Client approves deliverable

### Action

Client clicks **Approve Deliverable**.

### Expected database result

| Table          | Field        | Expected value |
| -------------- | ------------ | -------------- |
| `Deliverables` | `Status`     | `APPROVED`     |
| `Deliverables` | `ReviewedAt` | Not null       |
| `Milestones`   | `Status`     | `APPROVED`     |
| `Milestones`   | `ApprovedAt` | Not null       |

---

## Step 4.4 â€” System releases payment

### Action

After deliverable is approved, system releases escrow payment to expert.

### Expected database result

| Table                | Field        | Expected value      |
| -------------------- | ------------ | ------------------- |
| `Payments`           | `Status`     | `RELEASED`          |
| `Payments`           | `ReleasedAt` | Not null            |
| `Milestones`         | `Status`     | `PAID`              |
| `Milestones`         | `PaidAt`     | Not null            |
| `WalletTransactions` | `Type`       | `PAYMENT_RELEASE`   |
| `WalletTransactions` | `Direction`  | `CREDIT` for expert |

### Wallet expected result

Assume:

```text
Client initial available balance = 2000 USD
Project amount = 900 USD
Expert initial available balance = 0 USD
```

After payment release:

| Wallet | Available balance | Held balance | Total earned |
| ------ | ----------------: | -----------: | -----------: |
| Client |            `1100` |          `0` |    unchanged |
| Expert |             `900` |          `0` |        `900` |

---

## Step 4.5 â€” System completes project

### Action

System checks that all milestones are paid.

If all milestones are paid, project is marked as completed.

### Expected database result

| Table      | Field         | Expected value |
| ---------- | ------------- | -------------- |
| `Projects` | `Status`      | `COMPLETED`    |
| `Projects` | `CompletedAt` | Not null       |
| `JobPosts` | `Status`      | `COMPLETED`    |

---

## Step 4.6 â€” Client reviews expert

### Action

Client leaves review for expert.

Example review:

```text
Rating: 5
Comment: Expert delivered a working chatbot on time with good quality.
Communication rating: 5
Quality rating: 5
Deadline rating: 5
```

### Expected database result

| Table     | Field            | Expected value |
| --------- | ---------------- | -------------- |
| `Reviews` | `ReviewerId`     | Client user id |
| `Reviews` | `RevieweeId`     | Expert user id |
| `Reviews` | `Rating`         | `5`            |
| `Reviews` | `QualityRating`  | `5`            |
| `Reviews` | `DeadlineRating` | `5`            |

---

## Step 4.7 â€” Expert reviews client

### Action

Expert leaves review for client.

Example review:

```text
Rating: 5
Comment: Client provided clear requirements and fast feedback.
Requirement clarity rating: 5
Communication rating: 5
```

### Expected database result

| Table     | Field                      | Expected value |
| --------- | -------------------------- | -------------- |
| `Reviews` | `ReviewerId`               | Expert user id |
| `Reviews` | `RevieweeId`               | Client user id |
| `Reviews` | `Rating`                   | `5`            |
| `Reviews` | `RequirementClarityRating` | `5`            |

---

# Final Expected System State

| Entity                | Final status                      |
| --------------------- | --------------------------------- |
| `JobPosts.Status`     | `COMPLETED`                       |
| `Projects.Status`     | `COMPLETED`                       |
| `Milestones.Status`   | `PAID`                            |
| `Deliverables.Status` | `APPROVED`                        |
| `Payments.Status`     | `RELEASED`                        |
| `Reviews`             | 2 reviews created                 |
| Expert wallet         | Received payment                  |
| Expert profile        | Completed project count increased |

---

# Acceptance Criteria for QuÃ¢nâ€™s Part

Your part is passed if all of these are true:

```text
1. Expert can submit deliverable.
2. Client can approve deliverable.
3. Payment is released only after approval.
4. Milestone changes to PAID after payment release.
5. Project changes to COMPLETED after all milestones are paid.
6. Client can review expert.
7. Expert can review client.
8. Review rating must be between 1 and 5.
9. User cannot review themselves.
10. Same reviewer cannot review the same reviewee twice for the same project.
```

---

# Important Negative Test Cases for Your Part

| Test case                                           | Expected result                                                  |
| --------------------------------------------------- | ---------------------------------------------------------------- |
| Release payment before deliverable approval         | Should fail                                                      |
| Review before project completed                     | Should not be allowed                                            |
| Rating = 0 or 6                                     | Should fail                                                      |
| ReviewerId = RevieweeId                             | Should fail                                                      |
| Duplicate review for same project/reviewer/reviewee | Should fail                                                      |
| Client requests revision                            | Payment should stay `HELD`                                       |
| Client opens dispute                                | Payment should become `FROZEN`, project should become `DISPUTED` |

---

# Simple End-to-End Flow Summary

```text
Client creates job
â†’ Expert submits proposal
â†’ Client accepts proposal
â†’ Project is created
â†’ Client funds milestone
â†’ Payment is held in escrow
â†’ Expert submits deliverable
â†’ Client approves deliverable
â†’ System releases payment
â†’ Milestone becomes PAID
â†’ Project becomes COMPLETED
â†’ Client reviews expert
â†’ Expert reviews client
```

This is a clean happy-path business flow you can present as the full E2E test, with your section covering the most important final state transitions: **submitted â†’ approved â†’ released â†’ paid â†’ completed â†’ reviewed**.


# Báº£n 4 main flow cuá»‘i cÃ¹ng nÃªn dÃ¹ng

| STT | Main Flow                                   | Actor chÃ­nh           | Má»¥c Ä‘Ã­ch                                                           |
| --: | ------------------------------------------- | --------------------- | ------------------------------------------------------------------ |
|   1 | **Create Job & Match Expert**               | Client                | Client táº¡o job, system há»— trá»£ lÃ m rÃµ yÃªu cáº§u vÃ  gá»£i Ã½ expert       |
|   2 | **Proposal & Project Creation**             | Expert, Client        | Expert gá»­i proposal, Client chá»n expert, system táº¡o project        |
|   3 | **Milestone, Escrow & Deliverable**         | Client, Expert        | Chia milestone, fund escrow, expert ná»™p deliverable, client review |
|   4 | **Completion, Payment, and Review**         | Client + Expert +System | Admin xá»­ lÃ½ tranh cháº¥p, project hoÃ n thÃ nh, hai bÃªn review         |

---

# Báº£n flow tá»•ng há»£p ngáº¯n gá»n

```text
1. Client creates a job and the system helps refine the requirement, suggest skills, budget, timeline, milestones, and recommend suitable experts.

2. Expert views the job, submits a proposal, and Client reviews proposals to select the most suitable expert. The system then creates a project from the accepted proposal.

3. Client and Expert confirm milestones. Client funds each milestone using simulated escrow. Expert completes the work and submits deliverables. Client reviews the deliverable and either approves, requests revision, or opens a dispute.

4. If a dispute occurs, Admin reviews evidence and resolves payment. After all milestones are completed, the project is marked as completed and both Client and Expert can review each other.
```

# CÃ¢u chá»‘t cho tÃ i liá»‡u

```text
AITasker has four main business flows: job creation and expert matching, proposal and project creation, milestone-based delivery with simulated escrow, and dispute resolution with project completion and review.
```

CÃ¡ch gá»™p nÃ y lÃ  há»£p lÃ½ nháº¥t: **khÃ´ng quÃ¡ vá»¥n nhÆ° 10â€“15 flow**, nhÆ°ng cÅ©ng **khÃ´ng quÃ¡ ngáº¯n Ä‘áº¿n má»©c máº¥t nghiá»‡p vá»¥ chÃ­nh**.
