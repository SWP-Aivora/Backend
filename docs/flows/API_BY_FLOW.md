# AITasker API by Flow - Compatibility Direct Transfer

> Source of truth: `MainFlows-new.md`.
>
<<<<<<< HEAD
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
| `GET` | `/experts/me/proposals` | Expert | View expert proposals. |
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
| `GET` | `/wallet/transactions` | Authenticated | View simulated transfer/event ledger. |

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
=======
> Source of truth: `MainFlows-new.md`.
>
> This document keeps the current API shape as much as possible and changes the business meaning from escrow/wallet custody to simulated direct-transfer tracking.
>
>## Global Rules
>
>- Base path: `/api/v1`
>- Auth: `Authorization: Bearer <accessToken>`
>- Response wrapper: `{ success, message, data, traceId }`
>- Existing technical names such as wallet, payment, transaction, held, and released may remain in code/API during MVP.
>- User-facing behavior must not describe project payment as legal wallet custody, escrow, fund freeze, refund, or platform payout.
>- `Wallets` means simulated demo balance only.
>- `Payments` means milestone direct-transfer tracking record.
>- `WalletTransactions` means simulated transfer/event ledger.

---

# FLOW 1: Create Job & Match Expert

> **Mục tiêu:** Client tạo job, System hỗ trợ làm rõ requirement bằng AI, tính toán expert phù hợp.
> **Actors:** Client (chính), Expert (phụ — xem job).
> **Status:** `Job: NULL → DRAFT → OPEN`
> **Tables:** `JobPosts`, `JobSkills`, `AIJobSuggestions`, `RecommendationResults`, `ExpertProfiles`, `ExpertSkills`, `Skills`, `Categories`

## 1.1. Gọi AI Job Assistant (Generate Suggestion)

```
POST /api/v1/ai/job-assistant
```

**Auth:** `ClientPolicy`.  **Rate Limit:** `AI` (20 req/min).

**Request body (minimal — chỉ cần `rawInput`):**
```json
{
  "rawInput": "I need a chatbot for my e-commerce store to handle customer support 24/7"
}
```

**Request body (đầy đủ):**
```json
{
  "rawInput": "Build a deep learning recommendation engine for our streaming platform",
  "businessDomain": "Media & Entertainment",
  "expectedOutcome": "Increase user engagement by 30% through personalized recommendations",
  "budgetType": "FIXED",
  "currency": "AICOIN",
  "budgetMin": 5000,
  "budgetMax": 15000,
  "timelineDays": 45,
  "experienceLevel": "ADVANCED"
}
```

**Field Reference (`GenerateSuggestionRequest`):**

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `rawInput` | string | **Yes** | >= 5 chars, trimmed |
| `businessDomain` | string? | No | Max 255 chars |
| `expectedOutcome` | string? | No | Max 1000 chars |
| `budgetType` | string? | No | `"FIXED"` \| `"HOURLY"` |
| `currency` | string? | No | Normalized: `"VND"`, `"USD"`, `"AICOIN"` |
| `budgetMin` | decimal? | No | — |
| `budgetMax` | decimal? | No | — |
| `timelineDays` | int? | No | — |
| `experienceLevel` | string? | No | `"BEGINNER"` \| `"INTERMEDIATE"` \| `"ADVANCED"` \| `"EXPERT"` |

**Status:** `201 Created` (thành công), `400` (validation), `401` (thiếu token), `403` (sai role).

**Side effects:** Tạo `AIJobSuggestions` (Status = `GENERATED`). Gọi AI Assistant Module.

**Response `201`:**
```json
{
  "success": true,
  "message": "AI job suggestion generated",
  "data": {
    "id": "7b1764ed-abeb-4684-83bb-6c1c02a13a48",
    "jobId": null,
    "clientId": "c87a3d46-243f-4fcc-b8c8-8b5a14815f32",
    "rawInput": "Build a deep learning recommendation engine for our streaming platform",
    "suggestedTitle": "AI Enhanced: Build a deep learning recommendation engine...",
    "suggestedDescription": "This is an AI enhanced description for: ...",
    "businessDomain": "Media & Entertainment",
    "expectedOutcome": "Increase user engagement by 30%...",
    "budgetType": "FIXED",
    "currency": "AICOIN",
    "suggestedBudgetMin": 5000,
    "suggestedBudgetMax": 15000,
    "suggestedTimelineDays": 45,
    "experienceLevel": "ADVANCED",
    "suggestedSkills": ["AI Chatbot", "Prompt Engineering"],
    "suggestedMilestones": [
      {
        "title": "Requirements Analysis",
        "description": "Analyze requirements...",
        "amount": 3000,
        "dueDays": 15,
        "acceptanceCriteria": null
      }
    ],
    "clarifyingQuestions": [
      "What streaming platforms do you currently use?",
      "Do you have existing user data for training?"
    ],
    "clarifyingAnswers": [],
    "riskWarnings": [
      "Budget may be low for real-time recommendation latency requirements",
      "Timeline is aggressive for deep learning model development"
    ],
    "aiModel": "Aivora-Mock",
    "status": "GENERATED",
    "rejectionReason": null,
    "createdAt": "2026-06-10T07:41:53Z"
  }
}
```

> **Error responses:** `400` — `"RawInput must be at least 5 characters long."` khi rawInput < 5 chars.
> `401` khi thiếu token hoặc token không hợp lệ.

---

## 1.2. Xem chi tiết gợi ý AI

```
GET /api/v1/ai/job-assistant/{suggestionId}
```

**Auth:** `ClientPolicy`, phải là chủ suggestion.

**Status:** `200` (thành công), `404` (không tìm thấy).

**Response `404`:**
```json
{
  "success": false,
  "message": "AI Suggestion not found.",
  "errors": { "code": "not_found" }
}
```

---

## 1.3. Chỉnh sửa gợi ý AI (Partial Update)

```
PATCH /api/v1/ai/job-assistant/{suggestionId}
```

**Auth:** `ClientPolicy`. Chỉ cập nhật các field được gửi lên (partial update).

**Field Reference (`PatchSuggestionRequest`):**

| Field | Type | Ghi chú |
|-------|------|---------|
| `suggestedTitle` | string? | Max 255 |
| `suggestedDescription` | string? | — |
| `businessDomain` | string? | Max 255 |
| `expectedOutcome` | string? | Max 1000 |
| `budgetType` | string? | `"FIXED"` \| `"HOURLY"` |
| `currency` | string? | — |
| `suggestedBudgetMin` | decimal? | — |
| `suggestedBudgetMax` | decimal? | — |
| `suggestedTimelineDays` | int? | — |
| `experienceLevel` | string? | `"BEGINNER"` \| `"INTERMEDIATE"` \| `"ADVANCED"` \| `"EXPERT"` |
| `suggestedSkills` | List\<string\>? | Array string |
| `suggestedMilestones` | List\<SuggestedMilestone\>? | Array object |
| `clarifyingAnswers` | List\<string\>? | Array string |

**Request body (cập nhật budget + skills + title):**
```json
{
  "suggestedTitle": "Custom AI Recommendation Engine for Streaming",
  "experienceLevel": "EXPERT",
  "budgetType": "HOURLY",
  "suggestedBudgetMin": 7000,
  "suggestedBudgetMax": 12000,
  "suggestedSkills": ["Python", "TensorFlow", "Recommendation Systems", "Kubernetes"],
  "suggestedTimelineDays": 60
}
```

> **Lưu ý:** Các enum (`experienceLevel`, `budgetType`) chấp nhận giá trị chuỗi (VD: `"EXPERT"`, `"HOURLY"`) nhờ `[JsonConverter(typeof(JsonStringEnumConverter))]`.

**Status:** `200` (thành công), `400` (suggestion đã xử lý).

---

## 1.4. Refine gợi ý AI

```
POST /api/v1/ai/job-assistant/{suggestionId}/refine
```

**Auth:** `ClientPolicy`. Gửi message để AI cải thiện suggestion.

**Request body (`RefineSuggestionRequest`):**
```json
{
  "message": "Increase budget to 20000 and add PyTorch to the skills"
}
```

**Validation:** `message` >= 3 chars.

**Response `200`:**
```json
{
  "success": true,
  "message": "AI suggestion refined",
  "data": {
    "suggestion": {
      "suggestedBudgetMin": 20000,
      "suggestedBudgetMax": 20000,
      "suggestedSkills": ["Python", "NLP", "TensorFlow", "FastAPI", "PyTorch"]
    },
    "aiResponse": "I updated the suggested budget.",
    "changedFields": ["suggestedBudgetMin", "suggestedBudgetMax"]
  }
}
```

> **Mock provider behavior:** `message` chứa "budget" → cập nhật budget.  Chứa "add skill: X" → thêm skill.  Chứa "question N: answer" → trả lời clarifying question.  Chứa "should"/"why"/"explain" → trả lời advisory.

---

## 1.5. Tạo job draft từ gợi ý AI (Accept)

```
POST /api/v1/ai/job-assistant/{suggestionId}/accept
```

**Auth:** `ClientPolicy`.

**Request body (`AcceptSuggestionRequest`):**
```json
{
  "categoryId": "681b2016-dc4d-40a8-a727-ec1b26b3e5e2",
  "selectedSkillIds": []
}
```

| Field | Type | Required | Ghi chú |
|-------|------|----------|---------|
| `categoryId` | Guid? | **Yes** | Category phải tồn tại |
| `selectedSkillIds` | List\<Guid\>? | No | IDs của các kỹ năng được chọn |

**Side effects:**
- `AIJobSuggestions.Status = ACCEPTED`.
- Tạo `JobPosts` (Status = `DRAFT`).
- Tạo `JobSkills` + `JobPostMilestones`.

**Status:** `201 Created` (thành công), `400` (thiếu categoryId / suggestion đã xử lý), `500` (lỗi DB).

**Response `201`:**
```json
{
  "success": true,
  "message": "Job draft created from AI suggestion",
  "data": {
    "job": {
      "id": "51bfa2f4-1484-4319-9d75-da2177fbc4e7",
      "title": "Production NLP Sentiment Analysis Pipeline",
      "status": "DRAFT"
    }
  }
}
```

---

## 1.6. Từ chối gợi ý AI

```
POST /api/v1/ai/job-assistant/{suggestionId}/reject
```

**Auth:** `ClientPolicy`.

**Request body (`RejectSuggestionRequest`):**
```json
{
  "reason": "The budget is too high for our current stage. We will create a simpler job."
}
```

**Validation:** `reason` >= 3 chars và <= 500 chars.

**Side effects:** `AIJobSuggestions.Status = REJECTED`, `AIJobSuggestions.RejectionReason = reason`.

**Status:** `200` (thành công), `400` (reason quá ngắn/dài hoặc suggestion đã xử lý).

**Response `200`:**
```json
{
  "success": true,
  "message": "AI suggestion rejected",
  "data": {
    "status": "REJECTED",
    "rejectionReason": "The budget is too high for our current stage..."
  }
}
```

---

## 1.7. AI Service Generator (Expert only)

```
POST /api/v1/ai/service-generator
```

**Auth:** `ExpertPolicy`.  **Rate Limit:** `AI` (20 req/min).

Tạo mô tả dịch vụ + 3 gói (Basic/Standard/Premium) cho Expert dựa trên input.

**Request body (`GenerateServiceDescriptionRequest`):**

```json
{
  "rawInput": "I am a senior AI engineer with 8 years of experience building production ML pipelines and custom LLM solutions for enterprise clients across finance and healthcare.",
  "skills": ["Python", "Machine Learning", "Deep Learning", "NLP", "TensorFlow", "PyTorch"],
  "priceFrom": 1500,
  "deliveryDays": 30,
  "tone": "professional",
  "targetClient": "enterprise",
  "language": "en"
}
```

**Field Reference:**

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `rawInput` | string | **Yes** | 20–4000 chars |
| `skills` | List\<string\> | **Yes** | 1–20 items |
| `priceFrom` | decimal | **Yes** | 1–100000 |
| `deliveryDays` | int | **Yes** | 1–365 |
| `tone` | string | No (default: `"professional"`) | `"professional"` \| `"friendly"` \| `"premium"` \| `"technical"` |
| `targetClient` | string | No (default: `"startup"`) | `"startup"` \| `"sme"` \| `"enterprise"` \| `"individual"` |
| `language` | string | No (default: `"vi"`) | `"vi"` \| `"en"` |

**Status:** `201 Created` (thành công), `400` (validation), `403` (Client gọi endpoint này).

**Response `201`:**
```json
{
  "success": true,
  "message": "Service description generated",
  "data": {
    "suggestedTitle": "Professional Service: Build Python Solutions",
    "suggestedDescription": "I deliver high-quality services for Python, Machine Learning...",
    "packages": [
      {
        "name": "Basic",
        "title": "Basic Package",
        "price": 1500,
        "deliveryDays": 15,
        "description": "Core setup and first delivery.",
        "features": ["Basic Python setup", "Setup documentation", "3 days support"]
      },
      {
        "name": "Standard",
        "title": "Standard Full Solution",
        "price": 3000,
        "deliveryDays": 30,
        "description": "Complete delivery for common production needs.",
        "features": ["Implementation with skills", "Testing", "API and database integration", "7 days support"]
      },
      {
        "name": "Premium",
        "title": "Premium Enterprise Solution",
        "price": 6000,
        "deliveryDays": 45,
        "description": "Advanced delivery with scalability and extended support.",
        "features": ["All Standard features", "Scalable architecture", "30 days warranty", "Handoff call"]
      }
    ],
    "faqs": [
      {
        "question": "What do I need to prepare to get started?",
        "answer": "Please prepare your requirements, examples, preferred stack, and any constraints."
      },
      {
        "question": "Is support included after delivery?",
        "answer": "Yes, support is included based on the selected package tier."
      }
    ]
  }
}
```

> **Gói tiers:** Luôn trả về đúng 3 gói: Basic, Standard, Premium. Giá: Basic = `priceFrom`, Standard = 2x, Premium = 4x.

---

## 1.8. AI Service — Test Results (2026-06-10)

Toan bộ 7 endpoint AI service da đuợc test thực tế với database PostgreSQL thật và Mock AI provider. Kết quả:

| # | Endpoint | Method | Status Code | Kết quả |
|---|----------|--------|-------------|---------|
| 1 | `/ai/job-assistant` | POST | 201 | Generate suggestion — trả về title, description, skills, milestones, clarifying questions, risk warnings |
| 2 | `/ai/job-assistant/{id}` | GET | 200 | Get suggestion — trả về toan bộ data |
| 3 | `/ai/job-assistant/{id}` | PATCH | 200 | Partial update — chấp nhận string enum (`EXPERT`, `HOURLY`) |
| 4 | `/ai/job-assistant/{id}/refine` | POST | 200 | AI refinement — trả về `aiResponse` + `changedFields` |
| 5 | `/ai/job-assistant/{id}/accept` | POST | 201 | Accept → tạo Job + JobPostMilestones trong DB |
| 6 | `/ai/job-assistant/{id}/reject` | POST | 200 | Reject — cập nhật status + rejectionReason |
| 7 | `/ai/service-generator` | POST | 201 | Generate service description — 3 tiers (Basic/Standard/Premium) + FAQs |

**Error cases đa verify:**

| Test case | Expected | Actual |
|-----------|----------|--------|
| Thiếu auth token | 401 | 401 |
| `rawInput` < 5 chars | 400 + message | `"RawInput must be at least 5 characters long."` |
| Suggestion ID khong tồn tại | 404 | `"AI Suggestion not found."` |
| Suggestion đa xử lý (reject lại) | 400 | `"Suggestion is already processed."` |
| Client gọi `/ai/service-generator` | 403 | 403 Forbidden |
| `reason` < 3 chars khi reject | 400 | Validation error |
| Thiếu `categoryId` khi accept | 400 | `"CategoryId is required..."` |

**Bugs đa fix trong quá trinh test:**

| Bug | Root Cause | Fix |
|-----|-----------|-----|
| `JobPostMilestones` table khong tồn tại | Thiếu `IEntityTypeConfiguration<JobPostMilestone>` + migration | Tạo `JobPostMilestoneConfiguration.cs` + migration `AddJobPostMilestonesTable` |
| PATCH `experienceLevel` / `budgetType` bị reject | `System.Text.Json` khong parse string->enum mặc định | Them `[JsonConverter(typeof(JsonStringEnumConverter))]` vao `SkillLevel` va `BudgetType` enums |
| AI luon dung Mock provider | Chưa set `AIProvider__ApiKey` env var | Set `AIProvider__Provider=Gemini` + `AIProvider__ApiKey=<key>` để bật real AI |

## 1.7. Tạo job draft thủ công

```
POST /api/v1/jobs
```

**Auth:** `ClientPolicy`.

**Request body:**
```json
{
  "title": "Xây dựng Chatbot AI",
  "originalDescription": "Tôi muốn chatbot AI cho shop bán mỹ phẩm.",
  "enhancedDescription": "Mô tả chi tiết...",
  "budgetMin": 800,
  "budgetMax": 1500,
  "timelineDays": 21,
  "categoryId": "guid",
  "skillIds": ["guid-1", "guid-2"]
}
```

**Validation:**
- `originalDescription` bắt buộc.
- `title` bắt buộc khi publish (có thể trống khi tạo draft).
- `budgetMin ≤ BudgetMax` nếu cả hai đều có.

## 1.8. Cập nhật job draft

```
PUT /api/v1/jobs/{jobId}
```

**Auth:** `ClientPolicy`, chủ job. Chỉ cho phép khi Status = `DRAFT`.

## 1.9. Publish job

```
POST /api/v1/jobs/{jobId}/publish
```

**Auth:** `ClientPolicy`, chủ job.

**Validation:** Job phải có `title` và `enhancedDescription` (hoặc `originalDescription`).

**Status transition:** `DRAFT → OPEN`.

**Side effects:**
- `JobPosts.Status = OPEN`.
- `JobPosts.PublishedAt = UTC now`.

## 1.10. Xem danh sách job (public)

```
GET /api/v1/jobs?status=OPEN&categoryId=&skillId=&pageIndex=1&pageSize=20
```

**Auth:** không bắt buộc.

## 1.11. Xem chi tiết job

```
GET /api/v1/jobs/{jobId}
```

**Auth:** không bắt buộc (public cho OPEN job).

## 1.12. Hủy job

```
POST /api/v1/jobs/{jobId}/cancel
```

**Auth:** `ClientPolicy`, chủ job.

**Status transition:** `DRAFT / OPEN → CANCELLED`.

## 1.13. Xóa job

```
DELETE /api/v1/jobs/{jobId}
```

**Auth:** `ClientPolicy`, chủ job. Chỉ khi Status = `DRAFT`.

## 1.14. Tính toán expert recommendations

```
POST /api/v1/jobs/{jobId}/recommendations/generate
```

**Auth:** `ClientPolicy`, chủ job.

**Preconditions:** `JobPosts.Status = OPEN`.

**Side effects:**
- Load job skills, budget, category, description.
- Load active experts.
- Tính `TotalScore = 0.35*SkillScore + 0.20*PortfolioScore + 0.15*RatingScore + 0.10*BudgetScore + 0.10*AvailabilityScore + 0.10*CompletionScore`.
- Lưu vào `RecommendationResults`.

## 1.15. Xem expert recommendations cho job

```
GET /api/v1/jobs/{jobId}/recommendations
```

**Auth:** `ClientPolicy`, chủ job.

**Response:**
```json
{
  "success": true,
  "data": {
    "jobId": "guid",
    "generatedAt": "2026-06-10T10:00:00Z",
    "recommendations": [
      {
        "expertId": "guid",
        "totalScore": 87.50,
        "explanation": "Matches 5/6 required skills, has RAG chatbot experience, rating 4.8/5, and budget fits the client's range.",
        "expert": {
          "fullName": "Trần Văn B",
          "headline": "AI/ML Engineer",
          "ratingAvg": 4.8,
          "completedProjects": 12,
          "hourlyRate": 45.00
        }
      }
    ]
  }
}
```

## Flow 1 — API Summary

| # | Method | Endpoint | Auth | Tables |
|---|--------|----------|------|--------|
| 1 | POST | `/ai/job-assistant` | Client | `AIJobSuggestions` |
| 2 | GET | `/ai/job-assistant/{id}` | Client | `AIJobSuggestions` |
| 3 | PATCH | `/ai/job-assistant/{id}` | Client | `AIJobSuggestions` |
| 4 | POST | `/ai/job-assistant/{id}/refine` | Client | `AIJobSuggestions` |
| 5 | POST | `/ai/job-assistant/{id}/accept` | Client | `AIJobSuggestions`, `JobPosts`, `JobSkills` |
| 6 | POST | `/ai/job-assistant/{id}/reject` | Client | `AIJobSuggestions` |
| 7 | POST | `/ai/service-generator` | Expert | `AIJobSuggestions` |
| 8 | POST | `/jobs` | Client | `JobPosts`, `JobSkills`, `JobPostMilestones` |
| 9 | GET | `/jobs` | — | `JobPosts` |
| 10 | GET | `/jobs/{id}` | — | `JobPosts`, `JobSkills` |
| 11 | PUT | `/jobs/{id}` | Client | `JobPosts` |
| 12 | DELETE | `/jobs/{id}` | Client | `JobPosts` |
| 13 | POST | `/jobs/{id}/publish` | Client | `JobPosts` |
| 14 | POST | `/jobs/{id}/cancel` | Client | `JobPosts` |
| 15 | POST | `/jobs/{id}/recommendations/generate` | Client | `RecommendationResults` |
| 16 | GET | `/jobs/{id}/recommendations` | Client | `RecommendationResults` |

---

# FLOW 2: Proposal & Project Creation

> **Mục tiêu:** Expert nộp proposal, Client chọn 1 expert, System tạo project.
> **Actors:** Expert, Client.
> **Status:** `Proposal: NULL → SUBMITTED → ACCEPTED / REJECTED`, `Job: OPEN → IN_PROGRESS`, `Project: NULL → PENDING_PAYMENT`
> **Tables:** `JobPosts`, `Proposals`, `ProposalMilestones`, `Projects`, `Milestones`, `Conversations`, `Messages`

## 2.1. Nộp proposal

```
POST /api/v1/jobs/{jobId}/proposals
```

**Auth:** `ExpertPolicy`.

**Request body:**
```json
{
  "coverLetter": "Tôi có 5 năm kinh nghiệm xây dựng chatbot...",
  "proposedBudget": 1200.00,
  "proposedTimelineDays": 18,
  "milestones": [
    {
      "title": "Phân tích yêu cầu & prototype",
      "description": "Thu thập yêu cầu, thiết kế luồng hội thoại, xây dựng prototype",
      "amount": 500.00,
      "dueDay": 7,
      "acceptanceCriteria": "Prototype chạy được với 10 intent cơ bản"
    },
    {
      "title": "Tích hợp website & testing",
      "description": "Tích hợp chatbot vào website, viết test case",
      "amount": 700.00,
      "dueDay": 18,
      "acceptanceCriteria": "Chatbot hoạt động trên website, pass 90% test cases"
    }
  ]
}
```

**Validation:**
- 1 proposal / expert / job.
- `JobPosts.Status = OPEN`.
- `proposedBudget ≥ 0`.

**Status transition:** `Proposals: none → SUBMITTED`.

**Response 201:**
```json
{
  "success": true,
  "data": { "proposalId": "guid", "status": "SUBMITTED", "submittedAt": "2026-06-10T10:00:00Z" }
}
```

## 2.2. Xem danh sách proposal của một job

```
GET /api/v1/jobs/{jobId}/proposals
```

**Auth:** `ClientPolicy`, chủ job.

## 2.3. Xem chi tiết proposal

```
GET /api/v1/proposals/{proposalId}
```

**Auth:** `ClientPolicy` (chủ job) hoặc `ExpertPolicy` (chủ proposal).

## 2.4. Xem danh sách proposal của Expert

```
GET /api/v1/proposals/me
```

**Auth:** `ExpertPolicy`.

## 2.5. Rút proposal

```
PUT /api/v1/proposals/{proposalId}/withdraw
```

**Auth:** `ExpertPolicy`, chủ proposal.

**Status transition:** `SUBMITTED / SHORTLISTED → WITHDRAWN`.

## 2.6. Shortlist proposal

```
PUT /api/v1/proposals/{proposalId}/shortlist
```

**Auth:** `ClientPolicy`, chủ job.

**Status transition:** `SUBMITTED → SHORTLISTED`.

## 2.7. Reject proposal

```
PUT /api/v1/proposals/{proposalId}/reject
```

**Auth:** `ClientPolicy`, chủ job.

**Status transition:** `SUBMITTED / SHORTLISTED → REJECTED`.

## 2.8. Accept proposal (atomic — bắt buộc transaction)

```
PUT /api/v1/proposals/{proposalId}/accept
```

**Auth:** `ClientPolicy`, chủ job.

**Preconditions:**
- `JobPosts.Status = OPEN`.
- Proposal Status = `SUBMITTED` hoặc `SHORTLISTED`.

**Transaction:**
1. Validate job / proposal / client / expert.
2. Selected proposal → `ACCEPTED`.
3. Sibling proposals (`SUBMITTED` / `SHORTLISTED`) → `REJECTED`.
4. `JobPosts.Status = IN_PROGRESS`.
5. Tạo `Projects` (Status = `PENDING_PAYMENT`).
6. Tạo `Milestones` từ `ProposalMilestones` (Status = `CREATED`).
7. Commit.

**Rollback:** Nếu bất kỳ bước nào fail → rollback toàn bộ, không tạo project, không đổi status proposal.

**Response 200:**
```json
{
  "success": true,
  "data": {
    "projectId": "guid",
    "status": "PENDING_PAYMENT",
    "jobId": "guid",
    "acceptedProposalId": "guid"
  }
}
```

## Flow 2 — API Summary

| # | Method | Endpoint | Auth | Tables |
|---|--------|----------|------|--------|
| 1 | POST | `/jobs/{id}/proposals` | Expert | `Proposals`, `ProposalMilestones` |
| 2 | GET | `/jobs/{id}/proposals` | Client | `Proposals` |
| 3 | GET | `/proposals/{id}` | Client/Expert | `Proposals`, `ProposalMilestones` |
| 4 | GET | `/proposals/me` | Expert | `Proposals` |
| 5 | PUT | `/proposals/{id}/withdraw` | Expert | `Proposals` |
| 6 | PUT | `/proposals/{id}/shortlist` | Client | `Proposals` |
| 7 | PUT | `/proposals/{id}/reject` | Client | `Proposals` |
| 8 | PUT | `/proposals/{id}/accept` | Client | `Proposals`, `JobPosts`, `Projects`, `Milestones` |

---

# FLOW 3: Milestone, Escrow & Deliverable

> **Mục tiêu:** Client & Expert xác nhận milestones, Client fund escrow, Expert nộp deliverable, Client review.
> **Actors:** Client, Expert.
> **Status:**
> - `Project: CREATED → PENDING_PAYMENT → ACTIVE → IN_REVIEW → COMPLETED` (hoặc `DISPUTED`)
> - `Milestone: CREATED → FUNDED → SUBMITTED → APPROVED → PAID` (revision: `SUBMITTED → REVISION_REQUESTED → SUBMITTED`, dispute: `SUBMITTED → DISPUTED`)
> - `Payment: NULL → PENDING → HELD → RELEASED` (dispute: `HELD → FROZEN`)
> **Tables:** `Projects`, `Milestones`, `Payments`, `Wallets`, `WalletTransactions`, `Deliverables`, `Disputes`, `DisputeEvidence`

## 3.1. Xem danh sách projects

```
GET /api/v1/projects?status=&pageIndex=1&pageSize=20
```

**Auth:** `ClientPolicy` hoặc `ExpertPolicy` (chỉ xem project mình tham gia).

## 3.2. Xem chi tiết project

```
GET /api/v1/projects/{projectId}
```

**Auth:** tham gia project.

**Response:**
```json
{
  "success": true,
  "data": {
    "projectId": "guid",
    "status": "PENDING_PAYMENT",
    "jobId": "guid",
    "clientId": "guid",
    "expertId": "guid",
    "milestones": [
      {
        "milestoneId": "guid",
        "title": "Phân tích yêu cầu & prototype",
        "description": "...",
        "amount": 500.00,
        "status": "CREATED",
        "orderIndex": 1,
        "acceptanceCriteria": "Prototype chạy được với 10 intent cơ bản"
      }
    ]
  }
}
```

## 3.3. Hủy project

```
PUT /api/v1/projects/{projectId}/cancel
```

**Auth:** `ClientPolicy`, chủ project.

## 3.4. Tạo milestone thủ công

```
POST /api/v1/projects/{projectId}/milestones
```

**Auth:** `ClientPolicy`, chủ project.

**Request body:**
```json
{
  "title": "Milestone mới",
  "description": "Mô tả",
  "amount": 300.00,
  "dueDate": "2026-07-01T00:00:00Z",
  "acceptanceCriteria": "Tiêu chí chấp nhận",
  "orderIndex": 2
}
```

**Validation:** `amount ≥ 0`.

## 3.5. Xem chi tiết milestone

```
GET /api/v1/milestones/{milestoneId}
```

**Auth:** tham gia project.

## 3.6. Cập nhật milestone

```
PUT /api/v1/milestones/{milestoneId}
```

**Auth:** `ClientPolicy`, chủ project. Chỉ khi Status = `CREATED`.

## 3.7. Demo deposit (nạp tiền ảo)

```
POST /api/v1/wallet/deposit-demo
```

**Auth:** `ClientPolicy`.

**Request body:**
```json
{ "amount": 5000.00 }
```

**Side effects:**
- `Wallet.AvailableBalance += amount`.
- Tạo `WalletTransactions` (Type = `DEMO_DEPOSIT`, Direction = `CREDIT`).

## 3.8. Xem số dư ví

```
GET /api/v1/wallet/me
```

**Auth:** bắt buộc.

**Response:**
```json
{
  "success": true,
  "data": { "availableBalance": 5000.00, "heldBalance": 0.00, "totalEarned": 0.00 }
}
```

## 3.9. Fund milestone (atomic — bắt buộc transaction)

```
PUT /api/v1/milestones/{milestoneId}/fund
```

**Auth:** `ClientPolicy`, chủ project.

**Preconditions:**
- `Milestone.Status = CREATED`.
- `Project.Status = PENDING_PAYMENT` hoặc `ACTIVE`.
- `Wallet.AvailableBalance ≥ Milestone.Amount`.

**Transaction:**
1. Kiểm tra số dư.
2. `Wallet.AvailableBalance -= Amount`.
3. `Wallet.HeldBalance += Amount`.
4. Tạo `Payments` (Status = `HELD`, `HeldAt = UTC now`).
5. Tạo `WalletTransactions` (Type = `ESCROW_HOLD`, Direction = `DEBIT`, UserId = ClientId).
6. `Milestone.Status = FUNDED`.
7. Nếu `Project.Status = PENDING_PAYMENT` → `ACTIVE`.
8. Commit.

**Response 200:**
```json
{
  "success": true,
  "data": {
    "paymentId": "guid",
    "milestoneId": "guid",
    "amount": 500.00,
    "status": "HELD",
    "projectId": "guid",
    "projectStatus": "ACTIVE"
  }
}
```

**Lỗi:** 400 nếu số dư không đủ. 409 nếu milestone đã funded.

## 3.10. Xem lịch sử payment

```
GET /api/v1/payments/history?pageIndex=1&pageSize=20
```

**Auth:** bắt buộc.

## 3.11. Nộp deliverable

```
POST /api/v1/milestones/{milestoneId}/deliverables
```

**Auth:** `ExpertPolicy`, expert được assign vào project.

**Request body:**
```json
{
  "description": "Đã hoàn thành prototype chatbot với 15 intent",
  "fileUrl": "https://res.cloudinary.com/.../prototype.zip",
  "demoUrl": "https://chatbot-demo.example.com",
  "sourceCodeUrl": "https://github.com/expert/chatbot-prototype",
  "note": "Vui lòng test trên Chrome"
}
```

**Preconditions:**
- `Milestone.Status ∈ { FUNDED, IN_PROGRESS, REVISION_REQUESTED }`.
- `Project.Status = ACTIVE`.
- `Payment.Status = HELD`.

**Validation:** Phải có ít nhất 1 trong: `description`, `fileUrl`, `demoUrl`, `sourceCodeUrl`, `note`.

**Status transitions:**
- `Milestone: FUNDED / IN_PROGRESS / REVISION_REQUESTED → SUBMITTED`.
- `Deliverable: none → SUBMITTED`.
- `Project: ACTIVE → IN_REVIEW`.

**Side effects:**
- `RevisionNumber` = 1 (lần đầu) hoặc `max(RevisionNumber) + 1` (resubmit).
- `Milestone.SubmittedAt = UTC now`.

**Response 201:**
```json
{
  "success": true,
  "data": { "deliverableId": "guid", "revisionNumber": 1, "status": "SUBMITTED", "submittedAt": "2026-06-10T10:00:00Z" }
}
```

## 3.12. Xem danh sách deliverable của milestone

```
GET /api/v1/milestones/{milestoneId}/deliverables
```

**Auth:** tham gia project.

## 3.13. Approve deliverable (atomic — bắt buộc transaction)

```
PUT /api/v1/milestones/{milestoneId}/approve
```

**Auth:** `ClientPolicy`, chủ project.

**Preconditions:**
- `Milestone.Status = SUBMITTED`.
- `Payment.Status = HELD`.

**Transaction:**
1. Latest `Deliverable.Status = APPROVED`, `ReviewedAt = UTC now`.
2. `Milestone.Status = APPROVED`, `ApprovedAt = UTC now`.
3. `Payment.Status = RELEASED`, `ReleasedAt = UTC now`.
4. `Client.Wallet.HeldBalance -= Amount`.
5. `Expert.Wallet.AvailableBalance += Amount`.
6. `Expert.Wallet.TotalEarned += Amount`.
7. Tạo `WalletTransactions`:
   - Client: Type = `PAYMENT_RELEASE`, Direction = `DEBIT`.
   - Expert: Type = `PAYMENT_RELEASE`, Direction = `CREDIT`.
8. `Milestone.Status = PAID`, `PaidAt = UTC now`.
9. Nếu tất cả milestones đều `PAID` → `Project.Status = COMPLETED`, `JobPosts.Status = COMPLETED`.
10. Ngược lại → `Project.Status = ACTIVE`.
11. Commit.

**Response 200:**
```json
{
  "success": true,
  "data": {
    "milestoneId": "guid",
    "status": "PAID",
    "paymentId": "guid",
    "releasedAmount": 500.00,
    "projectId": "guid",
    "projectStatus": "ACTIVE"
  }
}
```

## 3.14. Request revision

```
PUT /api/v1/milestones/{milestoneId}/request-revision
```

**Auth:** `ClientPolicy`, chủ project.

**Request body:**
```json
{ "reason": "Cần thêm intent cho câu hỏi về chính sách đổi trả" }
```

**Status transitions:**
- `Deliverable: SUBMITTED → REVISION_REQUESTED`.
- `Milestone: SUBMITTED → REVISION_REQUESTED`.
- `Project: IN_REVIEW → ACTIVE`.
- `Payment: HELD` (không đổi).

## 3.15. Open dispute (atomic — bắt buộc transaction)

```
POST /api/v1/milestones/{milestoneId}/dispute
```

**Auth:** `ClientPolicy`, chủ project.

**Request body:**
```json
{
  "reason": "Deliverable không đạt acceptance criteria",
  "description": "Chatbot không trả lời được câu hỏi về chính sách đổi trả"
}
```

**Transaction:**
1. Tạo `Disputes` (Status = `OPEN`).
2. `Milestone.Status = DISPUTED`.
3. `Payment.Status = FROZEN`, `FrozenAt = UTC now`.
4. `Project.Status = DISPUTED`.
5. Commit.

**Response 201:**
```json
{
  "success": true,
  "data": { "disputeId": "guid", "status": "OPEN", "milestoneId": "guid", "projectId": "guid" }
}
```

## Flow 3 — API Summary

| # | Method | Endpoint | Auth | Tables |
|---|--------|----------|------|--------|
| 1 | GET | `/projects` | Client/Expert | `Projects` |
| 2 | GET | `/projects/{id}` | Participant | `Projects`, `Milestones` |
| 3 | PUT | `/projects/{id}/cancel` | Client | `Projects` |
| 4 | POST | `/projects/{id}/milestones` | Client | `Milestones` |
| 5 | GET | `/milestones/{id}` | Participant | `Milestones` |
| 6 | PUT | `/milestones/{id}` | Client | `Milestones` |
| 7 | PUT | `/milestones/{id}/fund` | Client | `Wallets`, `Payments`, `WalletTransactions`, `Milestones`, `Projects` |
| 8 | PUT | `/milestones/{id}/approve` | Client | `Deliverables`, `Milestones`, `Payments`, `Wallets`, `WalletTransactions`, `Projects`, `JobPosts` |
| 9 | PUT | `/milestones/{id}/request-revision` | Client | `Deliverables`, `Milestones`, `Projects` |
| 10 | POST | `/milestones/{id}/dispute` | Client | `Disputes`, `Milestones`, `Payments`, `Projects` |
| 11 | GET | `/milestones/{id}/deliverables` | Participant | `Deliverables` |
| 12 | POST | `/milestones/{id}/deliverables` | Expert | `Deliverables`, `Milestones`, `Projects` |
| 13 | POST | `/wallet/deposit-demo` | Client | `Wallets`, `WalletTransactions` |
| 14 | GET | `/wallet/me` | Any | `Wallets` |
| 15 | GET | `/payments/history` | Any | `Payments` |

---

# FLOW 4: Completion, Payment, and Review

> **Mục tiêu:** System release payment, complete project, handle dispute (nếu có), cho phép review.
> **Actors:** Client, Expert, System. Admin chỉ tham gia khi có dispute.
> **Status:** `Project: COMPLETED`, `Job: COMPLETED`, `Payment: RELEASED`, `Reviews: created`
> **Tables:** `Projects`, `JobPosts`, `Milestones`, `Payments`, `Wallets`, `WalletTransactions`, `Reviews`, `Disputes`, `DisputeEvidence`

> **Lưu ý:** Các API release payment, approve deliverable, request revision, open dispute đã liệt kê ở Flow 3. Flow 4 tập trung vào **dispute resolution** và **review**.

## 4.1. Mở dispute trực tiếp

```
POST /api/v1/disputes
```

**Auth:** `ClientPolicy` hoặc `ExpertPolicy`. Cho phép mở dispute mà không cần qua milestone endpoint.

## 4.2. Xem danh sách disputes của user

```
GET /api/v1/disputes
```

**Auth:** bắt buộc. Trả về disputes mà user tham gia.

## 4.3. Xem chi tiết dispute

```
GET /api/v1/disputes/{disputeId}
```

**Auth:** tham gia dispute.

## 4.4. Thêm evidence

```
POST /api/v1/disputes/{disputeId}/evidence
```

**Auth:** tham gia dispute (Client/Expert/Admin).

**Request body:**
```json
{
  "description": "Screenshot của deliverable",
  "fileUrl": "https://res.cloudinary.com/.../evidence.png"
}
```

## 4.5. Resolve dispute (atomic — bắt buộc transaction)

```
PUT /api/v1/disputes/{disputeId}/resolve
```

**Auth:** `AdminPolicy`.

**Request body:**
```json
{
  "resolutionType": "RELEASE_TO_EXPERT",
  "resolutionNote": "Expert đã đạt 80% acceptance criteria, release toàn bộ payment.",
  "splitPercentage": null
}
```

**`resolutionType` ∈ { `RELEASE_TO_EXPERT`, `REFUND_TO_CLIENT`, `SPLIT_PAYMENT`, `REQUEST_REVISION` }.**

**Resolution A — `RELEASE_TO_EXPERT`:**
- `Payment: FROZEN → RELEASED`.
- `Expert.Wallet.AvailableBalance += Amount`.
- `Expert.Wallet.TotalEarned += Amount`.
- `Client.Wallet.HeldBalance -= Amount`.
- `Milestone: DISPUTED → PAID`.

**Resolution B — `REFUND_TO_CLIENT`:**
- `Payment: FROZEN → REFUNDED`.
- `Client.Wallet.HeldBalance -= Amount`.
- `Client.Wallet.AvailableBalance += Amount`.
- `Milestone: DISPUTED → REFUNDED`.

**Resolution C — `SPLIT_PAYMENT`:**
- `Payment: FROZEN → PARTIALLY_RELEASED`.
- `splitPercentage` bắt buộc (phần trăm release cho expert, ví dụ 60 = expert 60%, client 40%).

**Resolution D — `REQUEST_REVISION`:**
- `Payment: FROZEN → HELD`.
- `Milestone: DISPUTED → REVISION_REQUESTED`.
- `Project: DISPUTED → ACTIVE`.

**Chung:**
- `Disputes.Status = RESOLVED`.
- `Disputes.ResolutionType`, `ResolutionNote`, `AdminId`, `ResolvedAt`.

**Validation:**
- Chỉ Admin mới resolve.
- `ResolutionNote` bắt buộc.
- Payment FROZEN không thể release/refund 2 lần.

## 4.6. Tạo review

```
POST /api/v1/reviews
```

**Auth:** bắt buộc, tham gia project.

**Request body:**
```json
{
  "projectId": "guid",
  "revieweeId": "guid",
  "rating": 5,
  "comment": "Expert làm việc rất chuyên nghiệp, deliverable đúng hạn.",
  "communicationRating": 5,
  "qualityRating": 5,
  "deadlineRating": 5
}
```

**Preconditions:**
- `Project.Status = COMPLETED`.
- Reviewer tham gia project.
- Chưa review cặp (reviewer, reviewee, project) này.

**Validation:**
- `rating` ∈ [1, 5].
- `revieweeId ≠ reviewerId`.

**Side effects:**
- Tạo `Reviews`.
- Cập nhật `ExpertProfiles.RatingAvg` và `CompletedProjects` (nếu reviewee là expert).

**Response 201:**
```json
{
  "success": true,
  "data": { "reviewId": "guid", "rating": 5, "createdAt": "2026-06-10T10:00:00Z" }
}
```

## 4.7. Xem reviews của user

```
GET /api/v1/users/{userId}/reviews?pageIndex=1&pageSize=20
```

**Auth:** không bắt buộc (public).

## Flow 4 — API Summary

| # | Method | Endpoint | Auth | Tables |
|---|--------|----------|------|--------|
| 1 | POST | `/disputes` | Client/Expert | `Disputes` |
| 2 | GET | `/disputes` | Client/Expert | `Disputes` |
| 3 | GET | `/disputes/{id}` | Participant | `Disputes`, `DisputeEvidence` |
| 4 | POST | `/disputes/{id}/evidence` | Participant | `DisputeEvidence` |
| 5 | PUT | `/disputes/{id}/resolve` | Admin | `Disputes`, `Payments`, `Wallets`, `WalletTransactions`, `Milestones`, `Projects` |
| 6 | POST | `/reviews` | Participant | `Reviews`, `ExpertProfiles` |
| 7 | GET | `/users/{id}/reviews` | — | `Reviews` |

---

# Supporting APIs (Cross-flow)

> Các API không thuộc trực tiếp 4 main flows nhưng cần thiết để hệ thống hoạt động.

## Auth & Profile

| # | Method | Endpoint | Auth | Tables |
|---|--------|----------|------|--------|
| 1 | POST | `/auth/register` | — | `Users`, `Wallets`, `ClientProfiles`/`ExpertProfiles` |
| 2 | POST | `/auth/login` | — | `Users` |
| 3 | POST | `/auth/refresh-token` | — | — |
| 4 | GET | `/auth/me` | Any | `Users` |
| 5 | PUT | `/profiles/client` | Client | `ClientProfiles` |
| 6 | PUT | `/profiles/expert` | Expert | `ExpertProfiles` |
| 7 | GET | `/profiles/expert/{expertId}` | — | `ExpertProfiles` |
| 8 | GET | `/profiles/experts/featured` | — | `ExpertProfiles` |
| 9 | PUT | `/users/me` | Any | `Users` |

## Skills & Categories

| # | Method | Endpoint | Auth | Tables |
|---|--------|----------|------|--------|
| 1 | GET | `/categories` | — | `Categories` |
| 2 | GET | `/categories/{id}` | — | `Categories` |
| 3 | POST | `/categories` | Admin | `Categories` |
| 4 | GET | `/skills` | — | `Skills` |
| 5 | GET | `/skills/{id}` | — | `Skills` |
| 6 | POST | `/skills` | Admin | `Skills` |
| 7 | POST | `/skills/expert/me` | Expert | `ExpertSkills` |
| 8 | DELETE | `/skills/expert/me/{skillId}` | Expert | `ExpertSkills` |

## Messaging

| # | Method | Endpoint | Auth | Tables |
|---|--------|----------|------|--------|
| 1 | POST | `/conversations/init` | Any | `Conversations` |
| 2 | GET | `/conversations` | Any | `Conversations` |
| 3 | GET | `/conversations/{id}/messages` | Participant | `Messages` |
| 4 | POST | `/conversations/{id}/read` | Participant | `Messages` |

**SignalR Hub:** `/api/v1/chat`
- `SendMessage(conversationId, content)` → `ReceiveMessage`, `ReadConfirmation`, `Error`

## Media & Notifications

| # | Method | Endpoint | Auth | Tables |
|---|--------|----------|------|--------|
| 1 | POST | `/media/upload-image` | Any | Cloudinary |
| 2 | POST | `/media/upload-file` | Any | Cloudinary |
| 3 | DELETE | `/media/{publicId}` | Any | Cloudinary |
| 4 | GET | `/notifications` | Any | `Notifications` |
| 5 | GET | `/notifications/unread-count` | Any | `Notifications` |
| 6 | PUT | `/notifications/{id}/read` | Any | `Notifications` |
| 7 | PUT | `/notifications/read-all` | Any | `Notifications` |

## Admin

| # | Method | Endpoint | Auth | Tables |
|---|--------|----------|------|--------|
| 1 | GET | `/admin/stats` | Admin | Aggregate |
| 2 | GET | `/admin/users` | Admin | `Users` |
| 3 | PUT | `/admin/users/{id}/suspend` | Admin | `Users` |
| 4 | PUT | `/admin/users/{id}/unsuspend` | Admin | `Users` |

## Service Publishing (Optional MVP)

| # | Method | Endpoint | Auth | Tables |
|---|--------|----------|------|--------|
| 1 | POST | `/ai/service-generator` | Expert | — |

---

# Status Transition Rules

> Mọi endpoint cập nhật status **phải** validate transition hợp lệ. Transition không hợp lệ → `409 Conflict`.

## Job
```
DRAFT → OPEN → IN_PROGRESS → COMPLETED
DRAFT / OPEN → CANCELLED / CLOSED
```

## Proposal
```
SUBMITTED → SHORTLISTED
SUBMITTED / SHORTLISTED → ACCEPTED / REJECTED
SUBMITTED → WITHDRAWN
```

## Project
```
PENDING_PAYMENT → ACTIVE → IN_REVIEW → COMPLETED
ACTIVE / IN_REVIEW → DISPUTED
DISPUTED → ACTIVE / IN_REVIEW / COMPLETED / CANCELLED
```

## Milestone
```
CREATED → FUNDED → SUBMITTED → APPROVED → PAID
SUBMITTED → REVISION_REQUESTED → SUBMITTED
SUBMITTED → DISPUTED → PAID / REFUNDED / REVISION_REQUESTED
```

## Payment
```
PENDING → HELD → RELEASED
HELD → FROZEN → RELEASED / REFUNDED / PARTIALLY_RELEASED
```

## Deliverable
```
SUBMITTED → APPROVED
SUBMITTED → REVISION_REQUESTED → SUBMITTED
SUBMITTED → REJECTED
```

---

# Atomic Transactions

> Các endpoint sau **bắt buộc** chạy trong single DB transaction. Nếu bất kỳ bước nào fail → rollback toàn bộ.

| Endpoint | Flow | Reason |
|----------|------|--------|
| `PUT /proposals/{id}/accept` | Flow 2 | Accept proposal + reject siblings + create project + create milestones |
| `PUT /milestones/{id}/fund` | Flow 3 | Update wallet + create payment + update milestone + update project |
| `PUT /milestones/{id}/approve` | Flow 3 | Approve deliverable + release payment + update wallets + update milestone + update project + update job |
| `POST /milestones/{id}/dispute` | Flow 3 | Create dispute + update milestone + freeze payment + update project |
| `PUT /disputes/{id}/resolve` | Flow 4 | Update dispute + update payment + update wallets + update milestone + update project |

---

# Authorization Matrix

| Policy | Roles |
|--------|-------|
| `ClientPolicy` | CLIENT |
| `ExpertPolicy` | EXPERT |
| `AdminPolicy` | ADMIN |
| `Any` | bất kỳ authenticated user |
| `Participant` | user tham gia entity (project, conversation, dispute) |
| `—` | public, không cần auth |

---

# Negative Test Cases
>>>>>>> main

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
