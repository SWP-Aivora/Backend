# AITasker API by Flow - Compatibility Direct Transfer

> Source of truth: `MainFlows-new.md`.
>
> This document keeps the current API shape as much as possible and changes the business meaning from escrow/wallet custody to simulated direct-transfer tracking.

## Global Rules

- Base path: `/api/v1`
- Auth: `Authorization: Bearer <accessToken>`
- Response wrapper: `{ success, message, data, traceId }`
- Existing technical names such as wallet, payment, transaction, held, and released may remain in code/API during MVP.
- User-facing behavior must not describe project payment as legal wallet custody, escrow, fund freeze, refund, or platform payout.
- `Wallets` means simulated demo balance only.
- `Payments` means milestone direct-transfer tracking record.
- `WalletTransactions` means simulated transfer/event ledger.

## Flow 1: Create Job & Match Expert

Purpose: Client creates a job, System helps refine requirements, and System recommends experts.

| Method | Endpoint | Auth | Purpose |
|---|---|---|---|
| `POST` | `/ai/job-assistant` | Client | Generate job suggestion from raw requirement. |
| `GET` | `/ai/job-assistant/{suggestionId}` | Client owner | View suggestion detail. |
| `PATCH` | `/ai/job-assistant/{suggestionId}` | Client owner | Edit suggestion fields. |
| `POST` | `/ai/job-assistant/{suggestionId}/refine` | Client owner | Refine suggestion through AI. |
| `POST` | `/ai/job-assistant/{suggestionId}/accept` | Client owner | Create job draft from accepted suggestion. |
| `POST` | `/ai/job-assistant/{suggestionId}/reject` | Client owner | Reject suggestion. |
| `POST` | `/jobs` | Client | Create job draft manually. |
| `PUT` | `/jobs/{jobId}` | Client owner | Update draft job. |
| `POST` | `/jobs/{jobId}/publish` | Client owner | Publish job. |
| `GET` | `/jobs` | Public | List jobs, usually `OPEN` marketplace jobs. |
| `GET` | `/jobs/{jobId}` | Public for open jobs | View job detail. |
| `POST` | `/jobs/{jobId}/cancel` | Client owner | Cancel draft/open job. |
| `DELETE` | `/jobs/{jobId}` | Client owner | Delete draft job. |
| `POST` | `/jobs/{jobId}/recommendations/generate` | Client owner | Generate expert recommendations. |
| `GET` | `/jobs/{jobId}/recommendations` | Client owner | View expert recommendations. |

```text
Job: DRAFT -> OPEN -> IN_PROGRESS -> COMPLETED
Job alternative: DRAFT / OPEN -> CANCELLED / CLOSED
AIJobSuggestion: GENERATED -> ACCEPTED / REJECTED / FAILED
```

## Flow 2: Proposal, Agreement Snapshot & Project Creation

Purpose: Expert submits a proposal, Client accepts one proposal, and System creates a project/reference agreement snapshot.

| Method | Endpoint | Auth | Purpose |
|---|---|---|---|
| `POST` | `/jobs/{jobId}/proposals` | Expert | Submit proposal to an open job. |
| `GET` | `/jobs/{jobId}/proposals` | Client owner | View proposals for owned job. |
| `GET` | `/proposals/{proposalId}` | Participant | View proposal detail. |
| `GET` | `/proposals/me` | Expert | View expert proposals. |
| `PUT` | `/proposals/{proposalId}/shortlist` | Client owner | Shortlist proposal. |
| `PUT` | `/proposals/{proposalId}/reject` | Client owner | Reject proposal. |
| `PUT` | `/proposals/{proposalId}/withdraw` | Expert owner | Withdraw proposal. |
| `PUT` | `/proposals/{proposalId}/accept` | Client owner | Accept proposal and create project. |
| `GET` | `/projects/{projectId}` | Project participant | View created project. |

Accept proposal transaction:

1. Validate job is `OPEN`.
2. Validate caller owns the job.
3. Validate proposal is `SUBMITTED` or `SHORTLISTED`.
4. Set selected proposal to `ACCEPTED`.
5. Reject sibling proposals.
6. Set job to `IN_PROGRESS`.
7. Create project with status `PENDING_PAYMENT`.
8. Create initial milestones with status `CREATED`.

```text
Proposal: SUBMITTED -> SHORTLISTED -> ACCEPTED / REJECTED
Proposal alternative: SUBMITTED / SHORTLISTED -> WITHDRAWN
Job: OPEN -> IN_PROGRESS
Project: PENDING_PAYMENT
Milestone: CREATED
```

## Flow 3: Project Management, Simulated Direct Transfer, Deliverable, Dispute & Review

Purpose: Client and Expert manage milestones using the current payment/transaction APIs, but the business meaning is direct-transfer tracking rather than escrow.

### Payment Compatibility Rules

- Keep `Wallets`, `Payments`, and `WalletTransactions` for minimum code change.
- Keep `/milestones/{milestoneId}/fund` as the main payment-start endpoint for now.
- `/milestones/{milestoneId}/fund` means "client initiated a simulated direct transfer and the platform recorded the event".
- `PaymentStatus.HELD` means waiting for expert receipt confirmation, not escrow-held funds.
- `PaymentStatus.RELEASED` means receipt/payment record completed or deliverable payment completed, not platform release of money.
- `PaymentStatus.FROZEN`, `REFUNDED`, and `PARTIALLY_RELEASED` are legacy escrow states and should not be used in the new main flow.
- Wallet balance changes, if present, are demo/simulation only.

### Project & Milestone Endpoints

| Method | Endpoint | Auth | Purpose |
|---|---|---|---|
| `GET` | `/projects` | Client/Expert | List user projects. |
| `GET` | `/projects/{projectId}` | Participant | View project with milestones. |
| `PUT` | `/projects/{projectId}/cancel` | Client owner | Cancel project before payment/work starts. |
| `POST` | `/projects/{projectId}/milestones` | Client owner | Create milestone. |
| `GET` | `/milestones/{milestoneId}` | Participant | View milestone detail. |
| `PUT` | `/milestones/{milestoneId}` | Client owner | Update milestone while editable. |
| `PUT` | `/milestones/{milestoneId}/fund` | Client owner | Record simulated direct-transfer initiation for the milestone. |

`PUT /milestones/{milestoneId}/fund` expected effects:

1. Create or update `Payments` record for the milestone.
2. Set payment status to `HELD` as a legacy technical value.
3. Set milestone status to `FUNDED`.
4. Set project status to `ACTIVE` if needed.
5. Create `WalletTransactions` event as simulated transfer evidence.
6. Do not describe this as escrow hold, real wallet debit, or platform custody in user-facing copy.

### Optional Demo Balance APIs

| Method | Endpoint | Auth | Purpose |
|---|---|---|---|
| `GET` | `/wallet/me` | Authenticated | View simulated demo balance. |
| `POST` | `/wallet/deposit-demo` | Authenticated | Add demo balance for testing only. |
| `GET` | `/payments/history` | Authenticated | View simulated transfer/event ledger. |

These APIs are allowed for MVP demos, but docs/UI must call them simulated demo balance, not a legal wallet.

### Deliverable Endpoints

| Method | Endpoint | Auth | Purpose |
|---|---|---|---|
| `POST` | `/milestones/{milestoneId}/deliverables` | Expert owner | Submit deliverable after simulated transfer is recorded. |
| `GET` | `/milestones/{milestoneId}/deliverables` | Participant | List milestone deliverables. |
| `PUT` | `/milestones/{milestoneId}/approve` | Client owner | Approve deliverable and complete milestone. |
| `PUT` | `/milestones/{milestoneId}/request-revision` | Client owner | Request deliverable revision. |

Deliverable submission preconditions:

- Expert is assigned to the project.
- Milestone status is `FUNDED`, `IN_PROGRESS`, or `REVISION_REQUESTED`.
- Payment status is `HELD` or `RELEASED` depending on how the current service records receipt/completion.

Approve deliverable effects:

1. Latest deliverable becomes `APPROVED`.
2. Milestone becomes `PAID`.
3. Payment may become `RELEASED` as a legacy technical value.
4. Project becomes `COMPLETED` only when every milestone is completed.
5. No user-facing wording should say the platform releases or pays out real funds.

### Dispute Endpoints

| Method | Endpoint | Auth | Purpose |
|---|---|---|---|
| `POST` | `/milestones/{milestoneId}/dispute` | Participant | Flag milestone/project as disputed. |
| `POST` | `/disputes` | Participant | Open dispute directly. |
| `GET` | `/disputes` | Participant/Admin | List visible disputes. |
| `GET` | `/disputes/{disputeId}` | Participant/Admin | View dispute detail. |
| `POST` | `/disputes/{disputeId}/evidence` | Participant/Admin | Add evidence. |

Dispute effects:

1. Create dispute record with status `OPEN`.
2. Set milestone status to `DISPUTED`.
3. Set project status to `DISPUTED`.
4. Keep payment/transaction records as evidence.
5. Do not present dispute as platform fund freeze or refund.

Admin dispute money-resolution endpoints from the old escrow model are legacy/out-of-main-flow. If they remain in code, hide them from the main client/expert flow and document them as demo-only or deprecated.

### Review Endpoints

| Method | Endpoint | Auth | Purpose |
|---|---|---|---|
| `POST` | `/reviews` | Project participant | Create review after project completion. |
| `GET` | `/users/{userId}/reviews` | Public | View user reviews. |

Review preconditions:

- Project status is `COMPLETED`.
- Reviewer is the project client or expert.
- Reviewee is the other project participant.
- Rating is between 1 and 5.
- Same reviewer cannot review the same reviewee twice for the same project.

### Status Mapping

```text
Project:
PENDING_PAYMENT -> ACTIVE -> IN_REVIEW -> COMPLETED
ACTIVE / IN_REVIEW -> DISPUTED

Milestone:
CREATED -> FUNDED -> IN_PROGRESS -> SUBMITTED -> PAID
SUBMITTED -> REVISION_REQUESTED -> SUBMITTED
FUNDED / IN_PROGRESS / SUBMITTED / REVISION_REQUESTED -> DISPUTED

Milestone status meaning:
FUNDED = simulated direct transfer recorded / receipt ready, not escrow funded
PAID = deliverable approved and milestone completed, not platform payout

Payment:
PENDING -> HELD -> RELEASED
PENDING / HELD -> FAILED

Payment status meaning:
PENDING = transfer record created
HELD = simulated direct transfer initiated / waiting receipt confirmation
RELEASED = receipt/payment completed as a legacy technical value
FROZEN / REFUNDED / PARTIALLY_RELEASED = legacy escrow states, not used in main flow

Deliverable:
SUBMITTED -> APPROVED
SUBMITTED -> REVISION_REQUESTED -> SUBMITTED
SUBMITTED -> REJECTED

Dispute:
OPEN -> UNDER_REVIEW -> RESOLVED / CLOSED
```

## Supporting APIs

### Auth & Profile

| Method | Endpoint | Auth | Purpose |
|---|---|---|---|
| `POST` | `/auth/register` | Public | Register user. |
| `POST` | `/auth/login` | Public | Login. |
| `POST` | `/auth/refresh-token` | Public | Refresh token. |
| `GET` | `/auth/me` | Authenticated | Current user. |
| `PUT` | `/profiles/client` | Client | Update client profile. |
| `PUT` | `/profiles/expert` | Expert | Update expert profile. |
| `GET` | `/profiles/expert/{expertId}` | Public | View expert profile. |
| `GET` | `/profiles/experts/featured` | Public | Featured experts. |
| `PUT` | `/users/me` | Authenticated | Update own user information. |

### Skills, Categories, Messaging, Media, Notifications, Admin

These APIs remain supporting features and are not changed by the direct-transfer compatibility flow:

- Categories and skills CRUD/listing.
- Expert skill management.
- Conversation and message APIs.
- SignalR chat hub.
- Media upload/delete APIs.
- Notification read/list APIs.
- Admin stats and user suspension APIs.
- AI service generator for expert service publishing.

## API Request/Response Verification

This table audits all documented endpoints against the actual codebase implementation:

| Flow / Component | Method | Documented Endpoint | Actual Route in Code | Auth Policy | Status | Details / Mismatch / Planned Fix |
|---|---|---|---|---|---|---|
| **Flow 1** | `POST` | `/ai/job-assistant` | `/api/v1/ai/job-assistant` | `ClientPolicy` | **TRUE** | Generates job suggestion. Injects `AIJobAssistantService`. |
| **Flow 1** | `GET` | `/ai/job-assistant/{suggestionId}` | `/api/v1/ai/job-assistant/{id}` | `ClientPolicy` | **TRUE** | Retrieves AI suggestion. |
| **Flow 1** | `PATCH` | `/ai/job-assistant/{suggestionId}` | `/api/v1/ai/job-assistant/{id}` | `ClientPolicy` | **TRUE** | Patches fields in AI suggestion. |
| **Flow 1** | `POST` | `/ai/job-assistant/{suggestionId}/refine` | `/api/v1/ai/job-assistant/{id}/refine` | `ClientPolicy` | **TRUE** | Refines AI suggestion using new feedback. |
| **Flow 1** | `POST` | `/ai/job-assistant/{suggestionId}/accept` | `/api/v1/ai/job-assistant/{id}/accept` | `ClientPolicy` | **TRUE** | Accepts suggestion, creates a draft job post. |
| **Flow 1** | `POST` | `/ai/job-assistant/{suggestionId}/reject` | `/api/v1/ai/job-assistant/{id}/reject` | `ClientPolicy` | **TRUE** | Rejects suggestion. |
| **Flow 1** | `POST` | `/jobs` | `/api/v1/jobs` | `ClientPolicy` | **TRUE** | Creates a job post in `DRAFT` status. |
| **Flow 1** | `PUT` | `/jobs/{jobId}` | `/api/v1/jobs/{id}` | `ClientPolicy` | **TRUE** | Updates draft job post. |
| **Flow 1** | `POST` | `/jobs/{jobId}/publish` | `/api/v1/jobs/{id}/publish` | `ClientPolicy` | **TRUE** | Publishes job post to `OPEN` status. |
| **Flow 1** | `GET` | `/jobs` | `/api/v1/jobs` | `AllowAnonymous` | **TRUE** | Lists job posts with optional filter `categoryId`. |
| **Flow 1** | `GET` | `/jobs/{jobId}` | `/api/v1/jobs/{id}` | `AllowAnonymous` | **TRUE** | Retrieves job details. |
| **Flow 1** | `POST` | `/jobs/{jobId}/cancel` | `/api/v1/jobs/{id}/cancel` | `ClientPolicy` | **TRUE** | Cancels job post. Accepts optional reason in body. |
| **Flow 1** | `DELETE` | `/jobs/{jobId}` | `/api/v1/jobs/{id}` | `ClientPolicy` | **TRUE** | Deletes a draft job post. |
| **Flow 1** | `POST` | `/jobs/{jobId}/recommendations/generate` | `/api/v1/jobs/{id}/recommendations/generate` | `ClientPolicy` | **TRUE** | Generates recommended experts. |
| **Flow 1** | `GET` | `/jobs/{jobId}/recommendations` | `/api/v1/jobs/{id}/recommendations` | `ClientPolicy` | **TRUE** | Lists generated expert recommendations. |
| **Flow 2** | `POST` | `/jobs/{jobId}/proposals` | `/api/v1/jobs/{id}/proposals` | `ExpertPolicy` | **TRUE** | Submits proposal with custom budget, timeline, and milestones. |
| **Flow 2** | `GET` | `/jobs/{jobId}/proposals` | `/api/v1/jobs/{id}/proposals` | `ClientPolicy` | **TRUE** | Lists proposals for the client's owned job. |
| **Flow 2** | `GET` | `/proposals/{proposalId}` | `/api/v1/proposals/{id}` | `Authorize` (authenticated) | **TRUE** | Retrieves proposal details. |
| **Flow 2** | `GET` | `/experts/me/proposals` | `/api/v1/proposals/me` | `ExpertPolicy` | **PARTIAL** | Mapped path mismatch. Documented as `/experts/me/proposals`, actual endpoint is `/api/v1/proposals/me`. |
| **Flow 2** | `PUT` | `/proposals/{proposalId}/shortlist` | `/api/v1/proposals/{id}/shortlist` | `ClientPolicy` | **TRUE** | Shortlists a proposal. |
| **Flow 2** | `PUT` | `/proposals/{proposalId}/reject` | `/api/v1/proposals/{id}/reject` | `ClientPolicy` | **TRUE** | Rejects a proposal. |
| **Flow 2** | `PUT` | `/proposals/{proposalId}/withdraw` | `/api/v1/proposals/{id}/withdraw` | `ExpertPolicy` | **TRUE** | Withdraws proposal. |
| **Flow 2** | `PUT` | `/proposals/{proposalId}/accept` | `/api/v1/proposals/{id}/accept` | `ClientPolicy` | **TRUE** | Accepts proposal and initializes project. |
| **Flow 2** | `GET` | `/projects/{projectId}` | `/api/v1/projects/{id}` | `Authorize` (authenticated) | **TRUE** | Retrieves project details. |
| **Flow 3** | `GET` | `/projects` | `/api/v1/projects` | `Authorize` (authenticated) | **TRUE** | Lists user projects with optional `status` filter. |
| **Flow 3** | `GET` | `/projects/{projectId}` | `/api/v1/projects/{id}` | `Authorize` (authenticated) | **TRUE** | View project with milestones. |
| **Flow 3** | `PUT` | `/projects/{projectId}/cancel` | `/api/v1/projects/{id}/cancel` | `ClientPolicy` | **TRUE** | Cancels project. |
| **Flow 3** | `POST` | `/projects/{projectId}/milestones` | `/api/v1/projects/{id}/milestones` | `ClientPolicy` | **TRUE** | Creates a milestone. |
| **Flow 3** | `GET` | `/milestones/{milestoneId}` | `/api/v1/milestones/{id}` | `Authorize` (authenticated) | **TRUE** | Retrieves milestone details. |
| **Flow 3** | `PUT` | `/milestones/{milestoneId}` | `/api/v1/milestones/{id}` | `ClientPolicy` | **TRUE** | Updates milestone while in editable state. |
| **Flow 3** | `PUT` | `/milestones/{milestoneId}/fund` | `/api/v1/milestones/{id}/fund` | `ClientPolicy` | **TRUE** | Records simulated direct-transfer. Updates payment to `HELD` (technical status). |
| **Flow 3** | `GET` | `/wallet/me` | `/api/v1/wallet/me` | `Authorize` (authenticated) | **TRUE** | Retrieves simulated demo wallet balance. |
| **Flow 3** | `POST` | `/wallet/deposit-demo` | `/api/v1/wallet/deposit-demo` | `ClientPolicy` | **TRUE** | Deposits demo funds. |
| **Flow 3** | `GET` | `/wallet/transactions` | `/api/v1/payments/history` | `Authorize` (authenticated) | **PARTIAL** | Mapped path mismatch. Documented as `/wallet/transactions`, actual endpoint is `/api/v1/payments/history`. |
| **Flow 3** | `POST` | `/milestones/{milestoneId}/deliverables` | `/api/v1/milestones/{id}/deliverables` | `ExpertPolicy` | **TRUE** | Submits deliverable links/notes. |
| **Flow 3** | `GET` | `/milestones/{milestoneId}/deliverables` | `/api/v1/milestones/{id}/deliverables` | `Authorize` (authenticated) | **TRUE** | Lists deliverables for milestone. |
| **Flow 3** | `PUT` | `/milestones/{milestoneId}/approve` | `/api/v1/milestones/{id}/approve` | `ClientPolicy` | **TRUE** | Approves deliverable and completes milestone (releases technical payment). |
| **Flow 3** | `PUT` | `/milestones/{milestoneId}/request-revision` | `/api/v1/milestones/{id}/request-revision` | `ClientPolicy` | **TRUE** | Requests revision on latest deliverable. |
| **Flow 3** | `POST` | `/milestones/{milestoneId}/dispute` | `/api/v1/milestones/{id}/dispute` | `Authorize` (authenticated) | **TRUE** | Flags milestone/project as disputed. |
| **Flow 3** | `POST` | `/disputes` | `/api/v1/disputes` | `Authorize` (authenticated) | **TRUE** | Opens dispute directly. |
| **Flow 3** | `GET` | `/disputes` | `/api/v1/disputes` | `Authorize` (authenticated) | **TRUE** | Lists user disputes (or all disputes for Admins). |
| **Flow 3** | `GET` | `/disputes/{disputeId}` | `/api/v1/disputes/{id}` | `Authorize` (authenticated) | **TRUE** | View dispute detail. |
| **Flow 3** | `POST` | `/disputes/{disputeId}/evidence` | `/api/v1/disputes/{id}/evidence` | `Authorize` (authenticated) | **TRUE** | Adds text/file evidence to open dispute. |
| **Flow 3** | `PUT` | `/disputes/{disputeId}/resolve` | `/api/v1/disputes/{id}/resolve` | `AdminPolicy` | **TRUE** | Admin dispute money-resolution (demarcated as legacy/demo-only). |
| **Flow 3** | `POST` | `/reviews` | `/api/v1/reviews` | `Authorize` (authenticated) | **TRUE** | Creates rating & review post-completion. |
| **Flow 3** | `GET` | `/users/{userId}/reviews` | `/api/v1/users/{userId}/reviews` | `AllowAnonymous` | **TRUE** | Retrieves reviews for user (public). |
| **Admin** | `GET` | `/admin/stats` | `/api/v1/admin/stats` | `AdminPolicy` | **TRUE** | Retrieves dashboard stats. |
| **Admin** | `GET` | `/admin/users` | `/api/v1/admin/users` | `AdminPolicy` | **TRUE** | Lists users with search/pagination. |
| **Admin** | `PUT` | `/admin/users/{id}/suspend` | `/api/v1/admin/users/{id}/suspend` | `AdminPolicy` | **TRUE** | Suspends user. |
| **Admin** | `PUT` | `/admin/users/{id}/unsuspend` | `/api/v1/admin/users/{id}/unsuspend` | `AdminPolicy` | **TRUE** | Unsuspends user. |

## Legacy Concepts

| Legacy technical concept | New business meaning / decision |
|---|---|
| `Wallets` | Simulated demo balance only. |
| `WalletTransactions` | Simulated transfer/event ledger. |
| `Payments` | Milestone direct-transfer tracking record. |
| `/milestones/{id}/fund` | Record simulated direct-transfer initiation. |
| `Wallet.HeldBalance` | Legacy technical field; do not present as legally held funds. |
| `PaymentStatus.HELD` | Transfer initiated / waiting receipt confirmation. |
| `PaymentStatus.RELEASED` | Receipt/payment completed as legacy technical value. |
| `PaymentStatus.FROZEN` | Legacy escrow state, not main flow. |
| `PaymentStatus.REFUNDED` | Legacy escrow state, not main flow. |
| `WalletTransactionType.ESCROW_HOLD` | Legacy technical event name for simulated transfer initiation. |
| `WalletTransactionType.PAYMENT_RELEASE` | Legacy technical event name for receipt/completion event. |
| Admin release/refund/split | Legacy/out-of-main-flow, not normal client/expert journey. |

## Negative Test Cases

| Test Case | Expected Result |
|---|---|
| Expert submits deliverable before payment/transfer record exists | Should fail. |
| Client approves deliverable before deliverable submission | Should fail. |
| Dispute opens after simulated direct transfer | Milestone/project become `DISPUTED`; UI must not say real funds are frozen. |
| Client requests revision | Payment/transaction record remains unchanged. |
| Review before project completed | Should fail. |
| Rating = 0 or 6 | Should fail. |
| ReviewerId = RevieweeId | Should fail. |
| Duplicate review for same project/reviewer/reviewee | Should fail. |
| Non-owner client accepts proposal | Should fail. |
| Expert submits proposal to non-open job | Should fail. |

## Future Optional Refactor

After MVP, the team may rename technical models/endpoints from wallet/escrow language to direct-transfer language. This is optional and should not block the current minimum-change implementation.

## References

- `MainFlows-new.md` - current business-flow source of truth.
- `db.sql` - compatibility schema with legacy technical names.
- `README.md` - flow-doc status and legacy notes.
