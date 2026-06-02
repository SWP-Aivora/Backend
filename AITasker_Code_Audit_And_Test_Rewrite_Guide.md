# AITasker Code Audit & Test Rewrite Guide

> Mục tiêu của file này: dùng làm **tài liệu chỉ đạo cho AI/code agent** để đọc lại source code hiện tại, double-check logic nghiệp vụ, sửa sai state transition, và viết lại test cho project **AITasker**.
>
> Hãy coi đây là **source-of-truth ở mức nghiệp vụ**. Khi code hiện tại khác tài liệu này, agent phải ghi rõ khác ở đâu, vì sao khác, và chỉ sửa nếu sửa đó làm hệ thống đúng hơn với MVP flow.

---

## 0. Executive Summary

AITasker không nên được hiểu đơn giản là “Upwork/Fiverr cho AI”. Điểm khác biệt quan trọng của project là:

> AITasker là marketplace chuyên cho dịch vụ AI automation, tập trung vào việc giúp client không chuyên kỹ thuật mô tả đúng nhu cầu, gợi ý expert phù hợp, và quản lý delivery theo milestone + escrow giả lập để giảm rủi ro thất bại.

Core business flow cần được bảo vệ trong code và test:

```text
AI requirement
→ job post
→ expert recommendation
→ proposal
→ accept proposal
→ project created
→ milestone created/funded
→ escrow held
→ deliverable submitted
→ client approval / revision / dispute
→ payment released / refunded / frozen
→ review
```

Mọi service, controller, repository, entity, DTO và test phải xoay quanh flow trên. Không được để code chỉ là CRUD rời rạc mà thiếu invariant nghiệp vụ.

---

## 1. Scope cần kiểm soát

### 1.1 Must-have MVP

Agent phải ưu tiên kiểm tra và test các phần sau:

1. Authentication + role authorization: `CLIENT`, `EXPERT`, `ADMIN`.
2. Client job posting.
3. AI Job Assistant / AI Job Suggestion.
4. Expert profile + skills.
5. Expert recommendation.
6. Proposal submission, shortlist, reject, withdraw, accept.
7. Project creation after accepted proposal.
8. Milestone creation and funding.
9. Simulated escrow payment.
10. Deliverable submission.
11. Deliverable approval / revision request / dispute.
12. Payment release / refund / freeze.
13. Review and rating.
14. Admin dispute handling.

### 1.2 Should-have, nhưng không được làm hỏng MVP

Các phần sau có thể có, nhưng không được làm core flow phức tạp quá mức:

- Basic messaging.
- Service publishing.
- Admin dashboard.
- Basic notification.
- Transaction history.

### 1.3 Không nên build/test sâu trong MVP

Các phần sau nếu có thì chỉ để future work hoặc mock/demo:

- Real payment gateway.
- Real legal escrow.
- Real KYC.
- Full real-time chat phức tạp.
- Complex ML training.
- Fully automated dispute decision.
- Withdrawal thật.

---

## 2. Domain Model Source of Truth

Schema MVP hiện tại có các nhóm entity chính:

### 2.1 Identity & Profile

- `Users`
- `ClientProfiles`
- `ExpertProfiles`
- `Wallets`

Invariant cần kiểm:

- Mỗi `User.Email` là unique.
- `User.Role` chỉ có: `CLIENT`, `EXPERT`, `ADMIN`.
- Mỗi user chỉ có một wallet.
- Client action phải do user role `CLIENT` thực hiện.
- Expert action phải do user role `EXPERT` thực hiện.
- Admin action phải do user role `ADMIN` thực hiện.
- Expert profile chỉ thuộc về user có role `EXPERT`.
- Client profile chỉ thuộc về user có role `CLIENT`.

### 2.2 Category & Skill

- `Categories`
- `Skills`
- `ExpertSkills`
- `JobSkills`

Invariant cần kiểm:

- Một expert không được có duplicate skill.
- Một job không được có duplicate skill.
- Skill matching phải dựa trên `JobSkills` và `ExpertSkills`.
- `ExpertSkills.Level` chỉ nhận: `BEGINNER`, `INTERMEDIATE`, `ADVANCED`, `EXPERT`.

### 2.3 Job & AI Suggestion

- `JobPosts`
- `AIJobSuggestions`

Invariant cần kiểm:

- Job thuộc về một client.
- Job mới tạo mặc định là `DRAFT`.
- Job chỉ nhận proposal khi status là `OPEN`.
- `OriginalDescription` là input gốc của client.
- `EnhancedDescription` là nội dung đã được AI/client chỉnh sửa.
- AI suggestion không được auto-publish job nếu client chưa xác nhận.
- AI suggestion output phải được validate trước khi lưu.

### 2.4 Recommendation

- `RecommendationResults`

Invariant cần kiểm:

- Mỗi cặp `(JobId, ExpertId)` chỉ có một recommendation result tại một thời điểm hoặc một batch hiện tại.
- `TotalScore` nằm trong khoảng `0..100`.
- Recommendation phải có `Explanation`, không chỉ có score.
- Recommendation không được recommend user không phải expert.
- Recommendation không được recommend expert bị suspended/deleted.

### 2.5 Proposal

- `Proposals`
- `ProposalMilestones`

Invariant cần kiểm:

- Chỉ expert mới được submit proposal.
- Expert không được submit proposal cho job của chính mình nếu có logic self-action.
- Mỗi expert chỉ được submit một proposal cho một job.
- Job phải ở status `OPEN` mới được submit proposal.
- Proposal mới tạo là `SUBMITTED`.
- Chỉ proposal `SUBMITTED` hoặc `SHORTLISTED` mới được accept.
- Proposal đã `WITHDRAWN` không được accept/reject/shortlist.
- Proposal đã `REJECTED` không được accept.
- Proposal đã `ACCEPTED` không được withdraw/reject lại.

### 2.6 Project & Milestone

- `Projects`
- `Milestones`
- `Deliverables`

Invariant cần kiểm:

- Accept proposal phải tạo đúng một project.
- Một job chỉ được có một project.
- Một accepted proposal chỉ được có một project.
- Project phải link đúng `JobId`, `AcceptedProposalId`, `ClientId`, `ExpertId`.
- Project mới sau accept nên là `PENDING_PAYMENT` nếu milestone chưa funded.
- Milestone phải có `Amount >= 0`.
- Milestone nên có `AcceptanceCriteria` để review deliverable.
- Expert chỉ submit deliverable cho milestone thuộc project của mình.
- Client chỉ review deliverable của project của mình.

### 2.7 Wallet, Payment, Escrow

- `Wallets`
- `Payments`
- `WalletTransactions`

Invariant cần kiểm:

- Client phải có đủ `AvailableBalance` mới fund milestone được.
- Funding milestone phải trừ client available balance.
- Funding milestone phải tạo payment status `HELD`.
- Funding milestone phải tạo wallet transaction `ESCROW_HOLD`.
- Không được fund một milestone đã funded/paid/refunded/disputed.
- Không được release payment nếu payment chưa `HELD`.
- Không được release payment hai lần.
- Không được refund payment hai lần.
- Dispute phải freeze payment: `HELD → FROZEN`.
- Approve deliverable phải release payment: `HELD → RELEASED`.
- Refund phải chuyển payment: `HELD/FROZEN → REFUNDED`.
- Expert earning chỉ tăng khi payment released.

### 2.8 Conversation & Message

- `Conversations`
- `Messages`

Invariant cần kiểm:

- Conversation phải gắn với đúng client và expert.
- User chỉ đọc/gửi message nếu là participant của conversation hoặc admin nếu có quyền.
- Message phải có content hoặc attachment.

### 2.9 Review

- `Reviews`

Invariant cần kiểm:

- Chỉ review sau khi project completed hoặc milestone/payment đủ điều kiện.
- Không được tự review chính mình.
- Một reviewer chỉ review một reviewee một lần trong một project.
- Rating nằm trong `1..5`.
- Review phải update rating aggregate của expert/client nếu system có aggregate.

### 2.10 Dispute

- `Disputes`
- `DisputeEvidence`

Invariant cần kiểm:

- Chỉ client hoặc expert trong project mới mở dispute.
- Dispute phải gắn với project, milestone, payment.
- Mở dispute phải freeze payment.
- Admin mới được resolve dispute.
- Resolve dispute phải cập nhật đồng bộ: dispute status, payment status, milestone status, wallet balance/transaction.

---

## 3. State Machine Chuẩn

### 3.1 JobPost Status

```text
DRAFT → OPEN → IN_PROGRESS → COMPLETED
DRAFT → CANCELLED
OPEN → CANCELLED
OPEN → CLOSED
IN_PROGRESS → COMPLETED
IN_PROGRESS → CANCELLED / DISPUTED nếu có mở rộng
```

Check code:

- Không cho submit proposal khi job không phải `OPEN`.
- Accept proposal phải chuyển job `OPEN → IN_PROGRESS`.
- Job completed khi project completed.
- Không cho publish job nếu thiếu title/description/budget/status hợp lệ.

### 3.2 Proposal Status

```text
SUBMITTED → SHORTLISTED
SUBMITTED → REJECTED
SUBMITTED → WITHDRAWN
SUBMITTED → ACCEPTED
SHORTLISTED → REJECTED
SHORTLISTED → WITHDRAWN
SHORTLISTED → ACCEPTED
ACCEPTED = terminal
REJECTED = terminal
WITHDRAWN = terminal
```

Check code:

- Accept proposal phải là atomic transaction.
- Proposal được chọn: `ACCEPTED`.
- Sibling proposals còn đang `SUBMITTED` hoặc `SHORTLISTED`: `REJECTED`.
- Sibling proposals đã `WITHDRAWN`: giữ nguyên.
- Sibling proposals đã `REJECTED`: giữ nguyên.
- Sau accept, không proposal nào khác của job được accept.
- Sau accept, job không còn nhận proposal mới.

### 3.3 Project Status

```text
PENDING_PAYMENT → ACTIVE
ACTIVE → IN_REVIEW
IN_REVIEW → ACTIVE           // nếu request revision
IN_REVIEW → COMPLETED        // nếu tất cả milestone paid
ACTIVE / IN_REVIEW → DISPUTED
DISPUTED → ACTIVE            // nếu admin request revision
DISPUTED → COMPLETED         // nếu admin resolve final milestone
PENDING_PAYMENT / ACTIVE → CANCELLED
```

Check code:

- Project không được `ACTIVE` nếu chưa có milestone funded hoặc payment held.
- Project `COMPLETED` khi tất cả milestones đã `PAID` hoặc đã được resolve hợp lệ.
- Project `DISPUTED` khi có milestone/payment đang disputed/frozen.

### 3.4 Milestone Status

```text
CREATED → FUNDED
FUNDED → IN_PROGRESS
FUNDED / IN_PROGRESS → SUBMITTED
SUBMITTED → APPROVED
SUBMITTED → REVISION_REQUESTED
SUBMITTED → DISPUTED
REVISION_REQUESTED → SUBMITTED
APPROVED → PAID
DISPUTED → PAID
DISPUTED → REFUNDED
DISPUTED → REVISION_REQUESTED
```

Check code:

- Expert chỉ submit deliverable khi milestone `FUNDED`, `IN_PROGRESS`, hoặc `REVISION_REQUESTED`.
- Client chỉ approve/request revision/open dispute khi milestone `SUBMITTED`.
- `PAID` là terminal đối với milestone.
- `REFUNDED` là terminal đối với milestone nếu project không tiếp tục.

### 3.5 Payment Status

```text
PENDING → HELD
HELD → RELEASED
HELD → FROZEN
HELD → REFUNDED
FROZEN → RELEASED
FROZEN → REFUNDED
FROZEN → PARTIALLY_RELEASED
FAILED = terminal
RELEASED = terminal
REFUNDED = terminal
PARTIALLY_RELEASED = terminal hoặc tùy policy
```

Check code:

- Không release/refund nếu status terminal.
- Không tạo nhiều payment cho một milestone.
- `Payments.MilestoneId` unique phải được phản ánh trong code.
- Mọi payment mutation phải có wallet transaction tương ứng.

### 3.6 Deliverable Status

```text
SUBMITTED → APPROVED
SUBMITTED → REVISION_REQUESTED
SUBMITTED → REJECTED
REVISION_REQUESTED → SUBMITTED // bằng deliverable revision mới hoặc update revision number
```

Check code:

- Revision number phải tăng khi expert submit lại sau revision request.
- Chỉ latest deliverable mới được review.
- Deliverable approved phải đồng bộ milestone/payment.

### 3.7 Dispute Status

```text
OPEN → UNDER_REVIEW → RESOLVED
OPEN → CLOSED
UNDER_REVIEW → CLOSED
```

Resolution types:

```text
RELEASE_TO_EXPERT
REFUND_TO_CLIENT
SPLIT_PAYMENT
REQUEST_REVISION
```

Check code:

- `RELEASE_TO_EXPERT`: payment released, milestone paid/approved, expert wallet updated.
- `REFUND_TO_CLIENT`: payment refunded, milestone refunded, client wallet updated.
- `SPLIT_PAYMENT`: partial release/refund logic phải rõ, không để tiền biến mất.
- `REQUEST_REVISION`: payment có thể giữ frozen hoặc quay về held tùy policy, milestone revision requested.

---

## 4. Core Flow cần double-check trong code

### 4.1 AI-assisted Job Posting

Expected flow:

```text
Client creates draft job
→ Client calls AI Job Assistant
→ System stores AIJobSuggestion
→ Client accepts/edits suggestion
→ Job enhanced fields updated
→ Client publishes job
→ Job status = OPEN
```

Code audit checklist:

- [ ] Endpoint/service tạo job có kiểm role client không?
- [ ] Draft job có status `DRAFT` không?
- [ ] AI suggestion có lưu raw input và output không?
- [ ] AI output lỗi format có fallback không?
- [ ] Client phải confirm trước khi publish không?
- [ ] Publish job có validate required fields không?
- [ ] Publish job có set `PublishedAt` không?
- [ ] Job status transition có được kiểm tra trong domain/service không?

Tests cần có:

- Create draft job success.
- Client publishes valid draft job success.
- Publish job fails when user is expert/admin.
- Publish job fails when missing title/description.
- AI suggestion stored successfully.
- AI suggestion failure does not corrupt job.
- Accept AI suggestion updates enhanced description/skills/milestones.

---

### 4.2 Expert Recommendation

Expected flow:

```text
Job OPEN
→ Load active experts
→ Compute matching score
→ Store RecommendationResults
→ Return ranked experts with explanation
```

Code audit checklist:

- [ ] Recommendation only runs for existing job.
- [ ] Recommendation excludes suspended/deleted users.
- [ ] Recommendation excludes non-expert users.
- [ ] Score is normalized `0..100`.
- [ ] Explanation is generated and returned.
- [ ] Re-running recommendation does not create duplicate `(JobId, ExpertId)` rows.
- [ ] Skill score, rating score, budget score are deterministic enough for tests.

Suggested scoring contract:

```text
TotalScore =
  SkillScore * 0.35
+ PortfolioScore * 0.20
+ RatingScore * 0.15
+ BudgetScore * 0.10
+ AvailabilityScore * 0.10
+ CompletionScore * 0.10
```

Tests cần có:

- Expert with more matching skills ranks higher.
- Expert outside budget ranks lower.
- Suspended expert is excluded.
- Non-expert user is excluded.
- Recommendation result contains explanation.
- Duplicate recommendation result is not created on rerun.

---

### 4.3 Submit Proposal

Expected flow:

```text
Expert views OPEN job
→ Expert submits proposal
→ Proposal status = SUBMITTED
→ Optional proposal milestones saved
```

Code audit checklist:

- [ ] Only expert can submit proposal.
- [ ] Job must be `OPEN`.
- [ ] Expert cannot submit duplicate proposal for same job.
- [ ] Budget must be `>= 0`.
- [ ] Proposed milestones amount must be `>= 0`.
- [ ] Proposal status is `SUBMITTED`.
- [ ] `SubmittedAt` and `UpdatedAt` set correctly.

Tests cần có:

- Expert submits proposal success.
- Client cannot submit proposal.
- Admin cannot submit proposal.
- Proposal rejected if job is draft/in progress/completed/cancelled.
- Duplicate proposal rejected.
- Negative budget rejected.
- Proposal milestones saved in order.

---

### 4.4 Shortlist / Reject / Withdraw Proposal

Expected flow:

```text
Client can shortlist/reject proposal on their own job.
Expert can withdraw own proposal.
```

Code audit checklist:

- [ ] Client can only manage proposals belonging to their own job.
- [ ] Expert can only withdraw their own proposal.
- [ ] Shortlist only from `SUBMITTED`.
- [ ] Reject from `SUBMITTED` or `SHORTLISTED`.
- [ ] Withdraw from `SUBMITTED` or `SHORTLISTED`.
- [ ] Terminal states cannot be changed.
- [ ] `UpdatedAt` changes on status update.
- [ ] `WithdrawnAt` set when withdrawn.

Tests cần có:

- Client shortlists submitted proposal.
- Client rejects submitted/shortlisted proposal.
- Expert withdraws own proposal.
- Expert cannot withdraw another expert's proposal.
- Non-owner client cannot reject proposal.
- Cannot change accepted/rejected/withdrawn proposal.

---

### 4.5 Accept Proposal & Create Project

Expected flow:

```text
Client accepts proposal
→ chosen proposal = ACCEPTED
→ sibling submitted/shortlisted proposals = REJECTED
→ job = IN_PROGRESS
→ project created
→ milestones copied from proposal milestones or AI/job suggested milestones
→ transaction committed atomically
```

This is one of the most important flows. It must be transaction-safe.

Code audit checklist:

- [ ] Accept endpoint/service checks current user is job owner client.
- [ ] Proposal belongs to an `OPEN` job.
- [ ] Proposal status is `SUBMITTED` or `SHORTLISTED`.
- [ ] Transaction begins before status changes.
- [ ] Selected proposal set to `ACCEPTED`.
- [ ] Other active proposals set to `REJECTED`.
- [ ] Job status set to `IN_PROGRESS`.
- [ ] Project created exactly once.
- [ ] Project uses correct `JobId`, `AcceptedProposalId`, `ClientId`, `ExpertId`.
- [ ] Project title/description/budget copied consistently.
- [ ] Project status is `PENDING_PAYMENT`.
- [ ] Proposal milestones copied to project milestones if available.
- [ ] If no proposal milestones exist, system has fallback milestone strategy or requires client to create milestones later.
- [ ] If project creation fails, proposal/job statuses roll back.
- [ ] Idempotency: accepting same proposal twice does not create duplicate project.

Tests cần có:

- Accept proposal success creates project.
- Accept proposal rejects sibling proposals.
- Withdrawn sibling remains withdrawn.
- Rejected sibling remains rejected.
- Job moves to `IN_PROGRESS`.
- Project has correct client/expert/proposal/job IDs.
- Project status is `PENDING_PAYMENT`.
- Milestones copied from proposal milestones.
- Cannot accept proposal if current user is not job owner.
- Cannot accept proposal for non-open job.
- Cannot accept rejected/withdrawn proposal.
- Cannot accept two proposals for one job.
- Transaction rollback test: simulated failure after proposal status update leaves no partial updates.

---

### 4.6 Milestone Funding / Escrow Hold

Expected flow:

```text
Client funds milestone
→ client available balance decreases
→ payment created/updated to HELD
→ wallet transaction ESCROW_HOLD created
→ milestone status = FUNDED
→ project status = ACTIVE
```

Code audit checklist:

- [ ] Only project client can fund milestone.
- [ ] Milestone belongs to client's project.
- [ ] Milestone status must be `CREATED`.
- [ ] Client wallet exists.
- [ ] Client available balance >= milestone amount.
- [ ] Payment created with payer=client, payee=expert.
- [ ] Payment status becomes `HELD`.
- [ ] Client wallet available balance decreases.
- [ ] Wallet transaction created with type `ESCROW_HOLD`, direction `DEBIT`.
- [ ] Milestone status becomes `FUNDED`.
- [ ] Milestone `FundedAt` set.
- [ ] Project status becomes `ACTIVE`.
- [ ] Entire operation transaction-safe.

Tests cần có:

- Fund milestone success.
- Cannot fund without sufficient balance.
- Cannot fund milestone twice.
- Non-owner client cannot fund.
- Expert cannot fund as client.
- Payment created correctly.
- Wallet transaction created correctly.
- Project moves to active.
- Transaction rollback on payment/wallet failure.

---

### 4.7 Expert Submit Deliverable

Expected flow:

```text
Expert submits deliverable for funded/in-progress/revision milestone
→ deliverable saved
→ milestone status = SUBMITTED
→ project status = IN_REVIEW
```

Code audit checklist:

- [ ] Only assigned project expert can submit deliverable.
- [ ] Milestone belongs to expert's project.
- [ ] Milestone status allows submission.
- [ ] Deliverable has description or file/demo/source URL.
- [ ] Revision number increments correctly.
- [ ] Milestone `SubmittedAt` set.
- [ ] Project status becomes `IN_REVIEW`.

Tests cần có:

- Expert submits deliverable success.
- Expert cannot submit for unfunded milestone.
- Different expert cannot submit deliverable.
- Client cannot submit deliverable.
- Empty deliverable rejected.
- Revision submission increments revision number.

---

### 4.8 Client Approve Deliverable / Release Payment

Expected flow:

```text
Client approves submitted deliverable
→ deliverable = APPROVED
→ milestone = APPROVED/PAID
→ payment HELD → RELEASED
→ expert wallet available/earned increases
→ wallet transaction PAYMENT_RELEASE created
→ if all milestones paid, project = COMPLETED
```

Code audit checklist:

- [ ] Only project client can approve.
- [ ] Milestone status must be `SUBMITTED`.
- [ ] Payment status must be `HELD`.
- [ ] Latest deliverable approved.
- [ ] Payment release is idempotent.
- [ ] Expert wallet updated.
- [ ] Wallet transaction created.
- [ ] Milestone `ApprovedAt` and `PaidAt` set.
- [ ] Project completed only if all milestones terminal paid/resolved.
- [ ] Operation transaction-safe.

Tests cần có:

- Approve deliverable success releases payment.
- Cannot approve if no submitted deliverable.
- Cannot approve if payment not held.
- Cannot approve twice.
- Non-owner client cannot approve.
- Expert cannot approve own deliverable.
- Project completed when final milestone paid.
- Project remains active/review if other milestones unpaid.

---

### 4.9 Request Revision

Expected flow:

```text
Client requests revision
→ deliverable = REVISION_REQUESTED
→ milestone = REVISION_REQUESTED
→ payment remains HELD
→ expert can submit revision
```

Code audit checklist:

- [ ] Only project client can request revision.
- [ ] Milestone must be `SUBMITTED`.
- [ ] Revision reason required.
- [ ] Payment remains `HELD`.
- [ ] Expert can submit new deliverable after revision request.

Tests cần có:

- Request revision success.
- Revision does not release payment.
- Revision does not refund payment.
- Expert can resubmit after revision.
- Cannot request revision after paid/refunded/disputed terminal states.

---

### 4.10 Open Dispute / Freeze Payment

Expected flow:

```text
Client or expert opens dispute
→ dispute created
→ payment HELD → FROZEN
→ milestone = DISPUTED
→ project = DISPUTED
```

Code audit checklist:

- [ ] Only project participants can open dispute.
- [ ] Milestone/payment must belong to project.
- [ ] Payment status must be `HELD`.
- [ ] Dispute created with reason and description.
- [ ] Payment `FrozenAt` set.
- [ ] Milestone status `DISPUTED`.
- [ ] Project status `DISPUTED`.
- [ ] Duplicate open dispute for same milestone/payment blocked.

Tests cần có:

- Client opens dispute success.
- Expert opens dispute success if allowed by requirements.
- Non-participant cannot open dispute.
- Duplicate dispute blocked.
- Payment frozen.
- Milestone/project moved to disputed.

---

### 4.11 Admin Resolve Dispute

Expected flow:

```text
Admin reviews dispute
→ choose resolution
→ update payment/milestone/project/wallet/dispute atomically
```

Resolution handling:

#### RELEASE_TO_EXPERT

```text
Payment: FROZEN → RELEASED
Milestone: DISPUTED → PAID
Expert wallet: +amount
WalletTransaction: PAYMENT_RELEASE / CREDIT
Dispute: RESOLVED
```

#### REFUND_TO_CLIENT

```text
Payment: FROZEN → REFUNDED
Milestone: DISPUTED → REFUNDED
Client wallet: +amount
WalletTransaction: REFUND / CREDIT
Dispute: RESOLVED
```

#### REQUEST_REVISION

```text
Payment: FROZEN → HELD hoặc giữ FROZEN theo policy, nhưng phải nhất quán
Milestone: DISPUTED → REVISION_REQUESTED
Dispute: RESOLVED hoặc CLOSED
```

#### SPLIT_PAYMENT

```text
Payment: FROZEN → PARTIALLY_RELEASED
Expert wallet: +expertShare
Client wallet: +clientRefund
WalletTransactions: 2 records
Milestone: DISPUTED → PAID/REFUNDED/PARTIALLY_RELEASED policy rõ ràng
Dispute: RESOLVED
```

Code audit checklist:

- [ ] Only admin can resolve dispute.
- [ ] Dispute must be `OPEN` or `UNDER_REVIEW`.
- [ ] Resolution type required.
- [ ] Resolution note stored.
- [ ] Wallet/payment changes are transaction-safe.
- [ ] Money conservation: expert credit + client refund = frozen amount for split.
- [ ] Cannot resolve dispute twice.

Tests cần có:

- Admin release to expert success.
- Admin refund to client success.
- Admin request revision success.
- Admin split payment success if implemented.
- Non-admin cannot resolve.
- Cannot resolve already resolved dispute.
- Money conservation test for split payment.
- Transaction rollback test.

---

### 4.12 Review & Rating

Expected flow:

```text
Project completed
→ client reviews expert
→ expert reviews client
→ average rating updated
```

Code audit checklist:

- [ ] Only project participants can review.
- [ ] Reviewer and reviewee must belong to same project.
- [ ] Reviewer cannot review self.
- [ ] Rating 1..5.
- [ ] Duplicate review blocked.
- [ ] Review only allowed after project completion, unless policy says after milestone completion.
- [ ] Rating aggregate updated consistently.

Tests cần có:

- Client reviews expert success.
- Expert reviews client success.
- Non-participant cannot review.
- Cannot self-review.
- Duplicate review rejected.
- Invalid rating rejected.
- Review before project completion rejected.
- Expert rating average updated.

---

## 5. API Contract Audit Checklist

Agent phải kiểm tra API theo các nhóm sau. Nếu API hiện tại khác tên endpoint thì vẫn kiểm theo chức năng tương đương.

### 5.1 Auth / Profile

```text
POST /auth/register
POST /auth/login
GET /me
PUT /clients/me/profile
PUT /experts/me/profile
GET /experts
GET /experts/{id}
```

Check:

- [ ] JWT/auth middleware hoạt động.
- [ ] Role claim đúng.
- [ ] Không trả `PasswordHash` ra response.
- [ ] Update profile không cho sửa user khác.

### 5.2 Job

```text
POST /jobs
GET /jobs
GET /jobs/{id}
PUT /jobs/{id}
POST /jobs/{id}/publish
POST /ai/job-assistant
```

Check:

- [ ] Public listing chỉ trả job `OPEN` nếu không phải owner/admin.
- [ ] Client xem được draft của mình.
- [ ] Expert không sửa job của client.
- [ ] Admin có thể xem/manage nếu có requirement.

### 5.3 Recommendation

```text
POST /jobs/{id}/recommendations/run
GET /jobs/{id}/recommendations
```

Check:

- [ ] Chỉ client owner hoặc admin được xem recommendation chi tiết.
- [ ] Kết quả sorted giảm dần theo score.
- [ ] Response có reason/explanation.

### 5.4 Proposal

```text
POST /jobs/{id}/proposals
GET /jobs/{id}/proposals
GET /proposals/{id}
PUT /proposals/{id}/shortlist
PUT /proposals/{id}/reject
PUT /proposals/{id}/withdraw
PUT /proposals/{id}/accept
```

Check:

- [ ] Proposal list của job chỉ owner client/admin thấy toàn bộ.
- [ ] Expert chỉ thấy proposal của mình nếu endpoint cá nhân.
- [ ] Accept proposal atomic.

### 5.5 Project / Milestone / Payment

```text
GET /projects
GET /projects/{id}
POST /projects/{id}/milestones
PUT /milestones/{id}/fund
POST /milestones/{id}/deliverables
PUT /milestones/{id}/approve
PUT /milestones/{id}/request-revision
POST /milestones/{id}/dispute
GET /payments/history
POST /wallet/deposit-demo
```

Check:

- [ ] Project listing filter theo role.
- [ ] Client chỉ xem project của mình.
- [ ] Expert chỉ xem project mình được hire.
- [ ] Admin xem được tất cả nếu có requirement.
- [ ] Wallet/payment endpoints không cho user mutate ví người khác.

### 5.6 Admin

```text
GET /admin/users
PUT /admin/users/{id}/suspend
GET /admin/disputes
PUT /admin/disputes/{id}/resolve
```

Check:

- [ ] Tất cả endpoint admin yêu cầu role admin.
- [ ] Admin action có audit log nếu system có.
- [ ] Suspend user không phá dữ liệu lịch sử.

---

## 6. Database / EF Core / ORM Audit Checklist

Nếu project dùng .NET + EF Core, kiểm tra các điểm sau. Nếu dùng framework khác thì map tương đương.

### 6.1 Entity Mapping

- [ ] Tất cả decimal money fields có precision rõ: `decimal(12,2)` hoặc `decimal(18,2)`.
- [ ] Email unique index.
- [ ] `UserId` unique ở `Wallets`, `ClientProfiles`, `ExpertProfiles`.
- [ ] Unique `(JobId, ExpertId)` ở `Proposals`.
- [ ] Unique `(JobId, ExpertId)` ở `RecommendationResults`.
- [ ] Unique `JobId` và `AcceptedProposalId` ở `Projects`.
- [ ] Unique `MilestoneId` ở `Payments`.
- [ ] Unique `(ProjectId, ReviewerId, RevieweeId)` ở `Reviews`.
- [ ] Check constraints hoặc domain validation cho statuses.

### 6.2 Transaction Boundaries

Các operation sau bắt buộc phải chạy trong transaction:

- Accept proposal + reject siblings + create project + create milestones.
- Fund milestone + update wallet + create payment + create wallet transaction.
- Approve deliverable + release payment + update wallet + update milestone/project.
- Open dispute + freeze payment + update milestone/project.
- Resolve dispute + update payment/wallet/milestone/project/dispute.

### 6.3 Concurrency / Idempotency

- [ ] Accept proposal không tạo duplicate project khi request double-click.
- [ ] Fund milestone không hold tiền hai lần.
- [ ] Approve deliverable không release tiền hai lần.
- [ ] Resolve dispute không release/refund hai lần.
- [ ] Có unique constraints hỗ trợ chống duplicate.
- [ ] Có retry hoặc error handling khi conflict.

### 6.4 Soft Delete / Status

- [ ] User deleted/suspended không được login/use system.
- [ ] Suspended expert không được submit proposal.
- [ ] Suspended expert không được recommend.
- [ ] Historical project/proposal vẫn giữ được thông tin.

---

## 7. Authorization Matrix

| Action | Client | Expert | Admin |
|---|---:|---:|---:|
| Create job | Own only | No | Optional |
| Publish job | Own only | No | Optional |
| Run recommendation | Own job | No | Yes |
| View recommended experts | Own job | No | Yes |
| Submit proposal | No | Yes, for OPEN job | No |
| Shortlist proposal | Own job | No | Optional |
| Reject proposal | Own job | No | Optional |
| Withdraw proposal | No | Own proposal | Optional |
| Accept proposal | Own job | No | Optional |
| View project | Own project | Assigned project | Yes |
| Create/fund milestone | Own project | No | Optional |
| Submit deliverable | No | Assigned project | No |
| Approve deliverable | Own project | No | Optional |
| Request revision | Own project | No | Optional |
| Open dispute | Own project | Assigned project | Yes |
| Resolve dispute | No | No | Yes |
| Create review | Project participant | Project participant | No |
| Suspend user | No | No | Yes |

Agent phải kiểm từng endpoint/service theo matrix này.

---

## 8. Test Rewrite Strategy

### 8.1 Mục tiêu viết lại test

Test không nên chỉ kiểm “endpoint trả 200”. Test phải bảo vệ invariant nghiệp vụ:

- State transition đúng.
- Role authorization đúng.
- Transaction rollback đúng.
- Money movement đúng.
- Duplicate action bị chặn.
- Terminal states không bị mutate.

### 8.2 Test Layers

#### Unit Tests

Dùng cho:

- Scoring algorithm.
- Status transition validation.
- Domain helper/calculator.
- DTO validator.
- Permission policy nhỏ.

Không dùng unit test để giả lập toàn bộ database quá mức nếu logic cần transaction thật.

#### Integration Tests

Dùng cho:

- Service + repository + database.
- Transaction boundaries.
- EF Core constraints.
- Wallet/payment consistency.
- Accept proposal atomic flow.

Nên dùng test database gần production nhất có thể. Nếu production là SQL Server, ưu tiên Testcontainers SQL Server hoặc localdb. Nếu không kịp, SQLite/InMemory chỉ dùng cho logic đơn giản và phải cẩn thận vì constraint khác SQL Server.

#### API / End-to-End Tests

Dùng cho:

- Auth + role claim.
- Full HTTP flow.
- Response DTO.
- Status code.
- Error format.

### 8.3 Test Naming Convention

Dùng format rõ:

```text
MethodName_StateUnderTest_ExpectedBehavior
```

Ví dụ:

```text
AcceptProposal_WhenProposalIsSubmitted_CreatesProjectAndRejectsSiblingProposals
FundMilestone_WhenClientHasInsufficientBalance_ReturnsValidationError
ApproveDeliverable_WhenPaymentAlreadyReleased_DoesNotReleaseAgain
```

### 8.4 Arrange / Act / Assert chuẩn

Mỗi test nên có pattern:

```text
Arrange:
- Seed client, expert, job, proposal, wallet.

Act:
- Call service/API.

Assert:
- Response/status correct.
- Database state correct.
- Related records correct.
- No unintended mutation.
```

### 8.5 Test Data Builder nên tạo

Agent nên tạo hoặc refactor test builders:

```text
UserBuilder.Client()
UserBuilder.Expert()
UserBuilder.Admin()
JobBuilder.Open(clientId)
JobBuilder.Draft(clientId)
ProposalBuilder.Submitted(jobId, expertId)
ProjectBuilder.PendingPayment(clientId, expertId, proposalId)
MilestoneBuilder.Created(projectId, amount)
PaymentBuilder.Held(projectId, milestoneId, payerId, payeeId, amount)
WalletBuilder.WithBalance(userId, amount)
```

Mục tiêu: test đọc như business story, không bị noise bởi setup dài.

---

## 9. Minimum Required Test Suite

### 9.1 Auth & Role

- [ ] Register client creates user/client profile/wallet.
- [ ] Register expert creates user/expert profile/wallet.
- [ ] Login returns token with role.
- [ ] Suspended user cannot login.
- [ ] User cannot access admin endpoint without admin role.

### 9.2 Job & AI Suggestion

- [ ] Client creates draft job.
- [ ] Expert cannot create job.
- [ ] Client publishes job.
- [ ] Publish invalid job fails.
- [ ] AI assistant stores suggestion.
- [ ] AI assistant failure handled safely.
- [ ] Accept AI suggestion updates job enhanced description.

### 9.3 Recommendation

- [ ] Run recommendation for open job.
- [ ] Expert with matching skills ranks higher.
- [ ] Suspended expert excluded.
- [ ] Non-expert excluded.
- [ ] Score range 0..100.
- [ ] Explanation present.
- [ ] Rerun does not duplicate records.

### 9.4 Proposal

- [ ] Expert submits proposal to open job.
- [ ] Cannot submit proposal to draft job.
- [ ] Cannot submit duplicate proposal.
- [ ] Client cannot submit proposal.
- [ ] Expert withdraws own proposal.
- [ ] Client shortlists proposal.
- [ ] Client rejects proposal.
- [ ] Non-owner client cannot manage proposal.
- [ ] Terminal proposal cannot change state.

### 9.5 Accept Proposal

- [ ] Accept submitted proposal creates project.
- [ ] Accept shortlisted proposal creates project.
- [ ] Accept rejects sibling submitted/shortlisted proposals.
- [ ] Withdrawn sibling not changed.
- [ ] Job becomes in progress.
- [ ] Project has correct relations.
- [ ] Proposal milestones copied to milestones.
- [ ] Cannot accept proposal for non-open job.
- [ ] Cannot accept rejected proposal.
- [ ] Cannot accept withdrawn proposal.
- [ ] Cannot accept two proposals for same job.
- [ ] Rollback if project creation fails.

### 9.6 Milestone Funding

- [ ] Client funds milestone successfully.
- [ ] Client balance decreases.
- [ ] Payment becomes held.
- [ ] Wallet transaction created.
- [ ] Milestone becomes funded.
- [ ] Project becomes active.
- [ ] Insufficient balance fails.
- [ ] Expert cannot fund milestone.
- [ ] Non-owner client cannot fund milestone.
- [ ] Cannot fund milestone twice.
- [ ] Rollback if wallet transaction fails.

### 9.7 Deliverable

- [ ] Expert submits deliverable successfully.
- [ ] Milestone becomes submitted.
- [ ] Project becomes in review.
- [ ] Expert cannot submit to another expert's project.
- [ ] Client cannot submit deliverable.
- [ ] Empty deliverable rejected.
- [ ] Revision submission increments revision number.

### 9.8 Approval / Revision

- [ ] Client approves deliverable.
- [ ] Payment released.
- [ ] Expert wallet credited.
- [ ] Wallet transaction created.
- [ ] Milestone paid.
- [ ] Final milestone completes project.
- [ ] Non-owner client cannot approve.
- [ ] Expert cannot approve.
- [ ] Cannot approve twice.
- [ ] Request revision keeps payment held.
- [ ] Expert can resubmit after revision request.

### 9.9 Dispute

- [ ] Client opens dispute.
- [ ] Expert opens dispute if allowed.
- [ ] Payment frozen.
- [ ] Milestone/project disputed.
- [ ] Duplicate open dispute blocked.
- [ ] Non-participant cannot open dispute.
- [ ] Admin release to expert.
- [ ] Admin refund to client.
- [ ] Admin request revision.
- [ ] Admin split payment if implemented.
- [ ] Non-admin cannot resolve.
- [ ] Resolved dispute cannot be resolved again.

### 9.10 Review

- [ ] Client reviews expert after completed project.
- [ ] Expert reviews client after completed project.
- [ ] Duplicate review blocked.
- [ ] Self-review blocked.
- [ ] Invalid rating blocked.
- [ ] Non-participant blocked.
- [ ] Rating aggregate updated.

---

## 10. High-Risk Bugs Agent Must Look For

### 10.1 Project duplicated after accept

Symptoms:

- Accept endpoint called twice creates two projects.
- Two proposals accepted for one job.
- Job stays open after accept.

Fix:

- Use transaction.
- Check job status.
- Add unique constraints.
- Re-read proposal/job inside transaction.

### 10.2 Money released twice

Symptoms:

- Expert wallet credited multiple times.
- Payment status already released but approval still credits wallet.

Fix:

- Check payment status before release.
- Use terminal status guard.
- Add idempotency check.

### 10.3 Sibling proposals not rejected

Symptoms:

- Multiple active proposals remain after client accepts one.
- UI still shows pending proposals for in-progress job.

Fix:

- Reject only `SUBMITTED` and `SHORTLISTED` siblings.
- Do not mutate `WITHDRAWN` or already `REJECTED`.

### 10.4 Authorization bypass

Symptoms:

- Another client can accept/reject proposal of someone else's job.
- Expert can approve own deliverable.
- Non-admin can resolve dispute.

Fix:

- Centralize owner checks.
- Integration tests with multiple users.

### 10.5 State transition too permissive

Symptoms:

- Proposal rejected after accepted.
- Milestone approved before deliverable submitted.
- Dispute opened after payment released.

Fix:

- Implement transition validators.
- Unit test every allowed/forbidden transition.

### 10.6 AI output corrupts domain data

Symptoms:

- AI invalid JSON crashes request.
- AI suggested negative budget saved.
- AI auto-publishes unsafe job.

Fix:

- Validate AI output.
- Store suggestion separately.
- Require client confirmation.

### 10.7 Test uses InMemory DB and misses constraints

Symptoms:

- Tests pass but SQL Server fails on unique/FK/check constraints.

Fix:

- Prefer integration tests with real SQL Server/Testcontainers.
- At minimum, add tests for uniqueness and FK behavior.

---

## 11. Error Handling Contract

Agent should make errors consistent. Suggested categories:

| Case | Suggested Status | Example |
|---|---:|---|
| Not authenticated | 401 | Missing/invalid token |
| Authenticated but wrong role | 403 | Expert tries to create job |
| Resource not found | 404 | Proposal not found |
| State transition invalid | 409 | Accept rejected proposal |
| Validation error | 400 | Negative budget |
| Duplicate resource | 409 | Duplicate proposal |
| Insufficient balance | 400 or 409 | Wallet balance too low |
| Unexpected server error | 500 | Unhandled exception |

Tests should assert not only status code but also meaningful error message/code.

---

## 12. Suggested Internal Services

Agent should check whether services are too CRUD-like. Recommended service boundaries:

```text
AuthService
UserProfileService
JobService
AIJobAssistantService
RecommendationService
ProposalService
ProjectService
MilestoneService
PaymentService
WalletService
DeliverableService
DisputeService
ReviewService
MessageService
AdminService
```

Important orchestration rule:

- `ProposalService.AcceptProposalAsync` may orchestrate project creation for MVP, but must keep transaction boundary clear.
- `PaymentService` should own money mutation logic.
- `WalletService` should not allow arbitrary balance mutation from controllers.
- Controllers should be thin: validate request → call service → return response.

---

## 13. Suggested DTO Response Rules

### 13.1 Never expose

- `PasswordHash`
- Internal secrets/API keys
- Full AI raw prompt if it contains private user data and endpoint is public
- Other users' wallet balances unless admin/owner

### 13.2 Proposal response should include

- Proposal id
- Job id
- Expert public summary
- Cover letter
- Proposed budget
- Proposed timeline
- Proposed milestones
- Status
- SubmittedAt/UpdatedAt

### 13.3 Project detail response should include

- Project id
- Job summary
- Client summary
- Expert summary
- Accepted proposal summary
- Milestones
- Payment status per milestone
- Current project status

### 13.4 Recommendation response should include

- Expert summary
- Total score
- Score breakdown
- Explanation

---

## 14. Suggested Test Data Scenarios

### Scenario A: Happy Path Demo

```text
1. Client has wallet balance 2000.
2. Expert has chatbot/RAG skills.
3. Client creates job: AI chatbot for beauty shop.
4. AI assistant improves job.
5. Client publishes job.
6. Recommendation ranks expert high.
7. Expert submits proposal with 2 milestones.
8. Client accepts proposal.
9. Project created.
10. Client funds milestone 1 amount 500.
11. Expert submits deliverable.
12. Client approves.
13. Payment released.
14. Project remains active if milestone 2 unpaid.
15. Client funds milestone 2.
16. Expert submits deliverable.
17. Client approves.
18. Project completed.
19. Client reviews expert.
```

Expected asserts:

- Final client balance = initial - total project amount.
- Final expert balance/earned = total project amount.
- All payments released.
- All milestones paid.
- Project completed.
- Review created.

### Scenario B: Competing Proposals

```text
1. Job OPEN.
2. Expert A submits proposal.
3. Expert B submits proposal.
4. Expert C submits proposal and withdraws.
5. Client accepts Expert A.
```

Expected asserts:

- Proposal A = ACCEPTED.
- Proposal B = REJECTED.
- Proposal C = WITHDRAWN.
- Job = IN_PROGRESS.
- One project only.

### Scenario C: Revision

```text
1. Milestone funded.
2. Expert submits deliverable.
3. Client requests revision.
4. Expert submits revision.
5. Client approves.
```

Expected asserts:

- Payment remains held during revision.
- Revision number increments.
- Payment released only after approval.

### Scenario D: Dispute Refund

```text
1. Milestone funded.
2. Expert submits deliverable.
3. Client opens dispute.
4. Admin refunds client.
```

Expected asserts:

- Payment frozen after dispute.
- Payment refunded after admin resolution.
- Client balance restored.
- Expert not credited.
- Milestone refunded.
- Dispute resolved.

### Scenario E: Dispute Release

```text
1. Milestone funded.
2. Expert submits deliverable.
3. Client opens dispute.
4. Admin releases to expert.
```

Expected asserts:

- Payment frozen after dispute.
- Payment released after admin resolution.
- Expert credited.
- Client not refunded.
- Milestone paid.
- Dispute resolved.

---

## 15. Concrete Agent Task List

Agent should perform this in order:

### Phase 1: Inventory

- [ ] List all modules/services/controllers/entities/tests.
- [ ] Identify implemented flows vs missing flows.
- [ ] Identify status enums/constants and compare with this file.
- [ ] Identify all money mutation code.
- [ ] Identify all transaction boundaries.
- [ ] Identify all authorization checks.

### Phase 2: Code Audit

- [ ] Audit job flow.
- [ ] Audit recommendation flow.
- [ ] Audit proposal flow.
- [ ] Audit accept proposal/project creation flow.
- [ ] Audit milestone/payment flow.
- [ ] Audit deliverable/revision flow.
- [ ] Audit dispute flow.
- [ ] Audit review flow.
- [ ] Audit admin authorization.

For every issue found, agent must report:

```text
Issue:
Impact:
Location:
Expected behavior:
Actual behavior:
Fix plan:
Test to add:
```

### Phase 3: Fix Critical Bugs First

Priority order:

1. Authorization bugs.
2. Money/payment bugs.
3. Transaction/duplicate project bugs.
4. Invalid state transition bugs.
5. Data consistency bugs.
6. DTO/response issues.
7. Non-critical refactor.

### Phase 4: Rewrite Tests

- [ ] Delete or rewrite tests that only check superficial success.
- [ ] Add test builders/fixtures.
- [ ] Add integration tests for transactional flows.
- [ ] Add negative tests for every role/state violation.
- [ ] Add idempotency/duplicate-action tests.
- [ ] Add money conservation tests.

### Phase 5: Final Verification

Agent must run:

```bash
# .NET example
 dotnet restore
 dotnet build
 dotnet test
```

If project uses Node/Java/etc., run equivalent commands:

```bash
npm install
npm run build
npm test
```

or

```bash
mvn test
```

Final report must include:

```text
- Files changed
- Bugs fixed
- Tests added/rewritten
- Test result summary
- Remaining risks
- Any behavior intentionally left unchanged
```

---

## 16. Ready-to-Paste Prompt for AI Code Agent

Copy this into the coding agent:

```text
Read `AITasker_Code_Audit_And_Test_Rewrite_Guide.md` first and treat it as the business source-of-truth for the AITasker MVP.

Your task:
1. Inspect the current codebase and identify all implemented modules, services, controllers, entities, DTOs, repositories, migrations, and tests.
2. Compare the code against the required MVP flow:
   AI requirement → job → recommendation → proposal → project → milestone → escrow → deliverable → approval/revision/dispute → review.
3. Double-check all role authorization, state transitions, transaction boundaries, idempotency rules, and wallet/payment consistency.
4. Fix critical bugs first, especially:
   - accepting proposals,
   - creating projects,
   - rejecting sibling proposals,
   - funding milestones,
   - releasing/refunding/frozen payments,
   - dispute resolution,
   - invalid role access,
   - duplicate actions.
5. Rewrite or add tests so they validate business invariants, not only status codes.
6. Use clear test names and builders/fixtures where useful.
7. Run the full test suite and provide a final report.

Do not expand scope into real payment gateway, real KYC, complex ML, or real-time chat unless already implemented and necessary. Keep the MVP stable and testable.

For every bug found, report:
- Issue
- Impact
- Location
- Expected behavior
- Actual behavior
- Fix plan
- Test added

Final output must include:
- Summary of fixes
- Files changed
- Tests added/rewritten
- Test results
- Remaining risks
```

---

## 17. Final Acceptance Criteria

Project is considered stable enough when all are true:

- [ ] Client can create and publish AI-assisted job.
- [ ] Recommendation returns ranked experts with explanation.
- [ ] Expert can submit proposal only to open job.
- [ ] Client can accept one proposal.
- [ ] Accept proposal creates exactly one project.
- [ ] Sibling active proposals are rejected.
- [ ] Job becomes in progress.
- [ ] Client can fund milestone using demo wallet.
- [ ] Funding milestone holds escrow and updates wallet/payment/transaction.
- [ ] Expert can submit deliverable.
- [ ] Client can approve deliverable and release payment.
- [ ] Client can request revision without releasing payment.
- [ ] Client/expert can open dispute and freeze payment.
- [ ] Admin can resolve dispute correctly.
- [ ] Review works after completion.
- [ ] Role authorization is enforced.
- [ ] Terminal states cannot be mutated.
- [ ] Duplicate actions do not corrupt data.
- [ ] All critical flows have integration tests.
- [ ] Full test suite passes.

---

## 18. Short Version for Reviewers

The code is correct only if it preserves this business invariant:

> AITasker must safely move a client from vague AI requirement to a structured job, match suitable experts, accept exactly one proposal, create a project, manage milestone escrow, allow deliverable review, and release/refund/freeze money without role leaks, duplicate actions, or invalid state transitions.

If a test does not protect one of these invariants, it is probably not valuable enough.
