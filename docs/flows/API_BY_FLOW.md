# Aivora — 4 Main Flows (API Reference)

> **Mục đích:** Tổng hợp toàn bộ API endpoint đang có trong code, nhóm theo 4 main flows từ [`MAINFLOW_v2.md`](./MAINFLOW_v2.md). Đây là tài liệu tham chiếu khi viết integration test, làm frontend integration, hoặc implement thêm.
>
> **Base path:** `/api/v1`
> **Auth:** `Authorization: Bearer <accessToken>` (hoặc HttpOnly cookie `accessToken`)
> **Response wrapper:** `{ success, message, data, traceId }` — xem [`../ARCHITECTURE.md`](../ARCHITECTURE.md) mục "API Response Format".

---

# FLOW 1: Create Job & Match Expert

> **Mục tiêu:** Client tạo job, System hỗ trợ làm rõ requirement bằng AI, tính toán expert phù hợp.
> **Actors:** Client (chính), Expert (phụ — xem job).
> **Status:** `Job: NULL → DRAFT → OPEN`
> **Tables:** `JobPosts`, `JobSkills`, `JobPostMilestones`, `AIJobSuggestions`, `RecommendationResults`, `ExpertProfiles`, `ExpertSkills`, `Skills`, `Categories`

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

---

## 1.3. Chỉnh sửa gợi ý AI (Partial Update)

```
PATCH /api/v1/ai/job-assistant/{suggestionId}
```

**Auth:** `ClientPolicy`. Chỉ cập nhật các field được gửi lên (partial update).

**Field Reference (`PatchSuggestionRequest`):** `suggestedTitle`, `suggestedDescription`, `businessDomain`, `expectedOutcome`, `budgetType`, `currency`, `suggestedBudgetMin`, `suggestedBudgetMax`, `suggestedTimelineDays`, `experienceLevel`, `suggestedSkills[]`, `suggestedMilestones[]`, `clarifyingAnswers[]` — tất cả optional, null = không đổi.

> **Lưu ý:** Enum (`experienceLevel`, `budgetType`) chấp nhận giá trị chuỗi nhờ `[JsonConverter(typeof(JsonStringEnumConverter))]`.

**Status:** `200` (thành công), `400` (suggestion đã xử lý).

---

## 1.4. Refine gợi ý AI

```
POST /api/v1/ai/job-assistant/{suggestionId}/refine
```

**Auth:** `ClientPolicy`. Gửi message để AI cải thiện suggestion (sửa `AIJobSuggestion` — trước khi job được tạo).

**Request body:**
```json
{ "message": "Increase budget to 20000 and add PyTorch to the skills" }
```

**Validation:** `message` >= 3 chars.

**Response `200`:**
```json
{
  "success": true,
  "data": {
    "suggestion": { "suggestedBudgetMin": 20000, "suggestedBudgetMax": 20000 },
    "aiResponse": "I updated the suggested budget.",
    "changedFields": ["suggestedBudgetMin", "suggestedBudgetMax"]
  }
}
```

> **Không nhầm với 1.4b** (`/ai/jobs/{jobId}/refine`) — endpoint đó sửa `Job` đã tạo, không phải suggestion. Xem `../ARCHITECTURE.md` mục Known Debt về 2 pipeline refine song song.

## 1.4b. Refine job đã tạo

```
POST /api/v1/ai/jobs/{jobId}/refine
```

**Auth:** `ClientPolicy`. **Rate Limit:** `AI`.

**Request body:**
```json
{ "message": "Increase the budget range and add a note about mobile support" }
```

**Response `200`:**
```json
{
  "success": true,
  "data": {
    "job": { "id": "guid", "title": "...", "budgetMin": 6000, "budgetMax": 12000 },
    "aiResponse": "Updated the budget range as requested.",
    "changedFields": ["budgetMin", "budgetMax"]
  }
}
```

Dùng `AIJobRefinementService` (khác pipeline với 1.4, xem lưu ý ở trên).

---

## 1.5. Tạo job draft từ gợi ý AI (Accept)

```
POST /api/v1/ai/job-assistant/{suggestionId}/accept
```

**Auth:** `ClientPolicy`.

**Request body:**
```json
{ "categoryId": "681b2016-dc4d-40a8-a727-ec1b26b3e5e2", "selectedSkillIds": [] }
```

| Field | Type | Required | Ghi chú |
|-------|------|----------|---------|
| `categoryId` | Guid? | **Yes** | Category phải tồn tại |
| `selectedSkillIds` | List\<Guid\>? | No | Skill IDs từ seed data |

**Side effects:** `AIJobSuggestions.Status = ACCEPTED`. Tạo `JobPosts` (Status = `DRAFT`) + `JobSkills` + `JobPostMilestones`.

**Status:** `201 Created`, `400` (thiếu categoryId / suggestion đã xử lý).

---

## 1.6. Từ chối gợi ý AI

```
POST /api/v1/ai/job-assistant/{suggestionId}/reject
```

**Auth:** `ClientPolicy`.

**Request body:**
```json
{ "reason": "The budget is too high for our current stage. We will create a simpler job." }
```

**Validation:** `reason` >= 3 và <= 500 chars.

**Side effects:** `AIJobSuggestions.Status = REJECTED`, `RejectionReason = reason`.

**Status:** `200`, `400`.

---

## 1.7. AI Service Generator (Expert only)

```
POST /api/v1/ai/service-generator
```

**Auth:** `ExpertPolicy`. **Rate Limit:** `AI`. Tạo mô tả dịch vụ + 3 gói (Basic/Standard/Premium) cho Expert.

**Request body (`GenerateServiceDescriptionRequest`):**
```json
{
  "rawInput": "I am a senior AI engineer with 8 years of experience...",
  "skills": ["Python", "Machine Learning", "Deep Learning"],
  "priceFrom": 1500,
  "deliveryDays": 30,
  "tone": "professional",
  "targetClient": "enterprise",
  "language": "en"
}
```

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| `rawInput` | string | **Yes** | 20–4000 chars |
| `skills` | List\<string\> | **Yes** | 1–20 items |
| `priceFrom` | decimal | **Yes** | 1–100000 |
| `deliveryDays` | int | **Yes** | 1–365 |
| `tone` | string | No (`"professional"`) | `professional`\|`friendly`\|`premium`\|`technical` |
| `targetClient` | string | No (`"startup"`) | `startup`\|`sme`\|`enterprise`\|`individual` |
| `language` | string | No (`"vi"`) | `vi`\|`en` |

**Status:** `201`, `400`, `403` (Client gọi endpoint này).

> **Gói tiers:** luôn 3 gói. Giá: Basic = `priceFrom`, Standard = 2x, Premium = 4x.

---

## 1.8. Tạo job draft thủ công

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

**Validation:** `originalDescription` bắt buộc. `title` bắt buộc khi publish. `budgetMin ≤ budgetMax` nếu cả hai đều có.

## 1.9. Cập nhật job draft

```
PUT /api/v1/jobs/{jobId}
```

**Auth:** `ClientPolicy`, chủ job. Chỉ khi Status = `DRAFT`.

## 1.10. Publish job

```
POST /api/v1/jobs/{jobId}/publish
```

**Auth:** `ClientPolicy`, chủ job. **Validation:** job phải có `title` + mô tả.

**Status transition:** `DRAFT → OPEN`. **Side effects:** `PublishedAt = UTC now`. Emit `JobStatusUpdated` (status=open) qua SignalR tới chủ job.

## 1.11. Xem danh sách job (public)

```
GET /api/v1/jobs?status=OPEN&categoryId=&skillId=&pageIndex=1&pageSize=20
```

**Auth:** không bắt buộc.

## 1.12. Xem chi tiết job

```
GET /api/v1/jobs/{jobId}
```

**Auth:** không bắt buộc (public cho OPEN job).

## 1.13. Hủy job

```
POST /api/v1/jobs/{jobId}/cancel
```

**Auth:** `ClientPolicy`, chủ job. **Status transition:** `DRAFT / OPEN → CANCELLED`. Emit `JobStatusUpdated` (status=cancelled).

## 1.14. Xóa job

```
DELETE /api/v1/jobs/{jobId}
```

**Auth:** `ClientPolicy`, chủ job. Chỉ khi Status = `DRAFT`.

## 1.15. Tính toán expert recommendations

```
POST /api/v1/jobs/{jobId}/recommendations/generate
```

**Auth:** `ClientPolicy`, chủ job. **Preconditions:** `JobPosts.Status = OPEN`.

**Side effects:** Tính `TotalScore = 0.40*SkillScore + 0.20*BudgetScore + 0.20*RatingScore + 0.10*AvailabilityScore + 0.10*CompletionScore` (trọng số cấu hình qua `RecommendationOptions`, phải luôn tổng 1.0). `PortfolioScore` (`RecommendationService/Response.cs:14`) luôn `0` — cột reserved, chưa có trọng số, không cộng vào `TotalScore`. Lưu vào `RecommendationResults`.

**Lưu ý Mock auto-approve:** `ConfigurationValidationExtensions.cs:66-82` đã chặn ở startup — Production bắt buộc `AIProvider:Provider=Gemini`, có `ApiKey`, và `EnableFallback=false`, nên hệ thống không thể âm thầm dùng Mock để tự động APPROVED một hồ sơ expert ở môi trường thật.

## 1.16. Xem expert recommendations cho job

```
GET /api/v1/jobs/{jobId}/recommendations
```

**Auth:** `ClientPolicy`, chủ job.

**Response — flat list (KHÔNG bọc trong `{jobId, generatedAt, recommendations}`):**
```json
{
  "success": true,
  "data": [
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
```

## Flow 1 — API Summary

| # | Method | Endpoint | Auth | Tables |
|---|--------|----------|------|--------|
| 1 | POST | `/ai/job-assistant` | Client | `AIJobSuggestions` |
| 2 | GET | `/ai/job-assistant/{id}` | Client | `AIJobSuggestions` |
| 3 | PATCH | `/ai/job-assistant/{id}` | Client | `AIJobSuggestions` |
| 4 | POST | `/ai/job-assistant/{id}/refine` | Client | `AIJobSuggestions` |
| 5 | POST | `/ai/jobs/{jobId}/refine` | Client | `JobPosts` |
| 6 | POST | `/ai/job-assistant/{id}/accept` | Client | `AIJobSuggestions`, `JobPosts`, `JobSkills`, `JobPostMilestones` |
| 7 | POST | `/ai/job-assistant/{id}/reject` | Client | `AIJobSuggestions` |
| 8 | POST | `/ai/service-generator` | Expert | — |
| 9 | POST | `/jobs` | Client | `JobPosts`, `JobSkills`, `JobPostMilestones` |
| 10 | GET | `/jobs` | — | `JobPosts` |
| 11 | GET | `/jobs/{id}` | — | `JobPosts`, `JobSkills` |
| 12 | PUT | `/jobs/{id}` | Client | `JobPosts` |
| 13 | DELETE | `/jobs/{id}` | Client | `JobPosts` |
| 14 | POST | `/jobs/{id}/publish` | Client | `JobPosts` |
| 15 | POST | `/jobs/{id}/cancel` | Client | `JobPosts` |
| 16 | POST | `/jobs/{id}/recommendations/generate` | Client | `RecommendationResults` |
| 17 | GET | `/jobs/{id}/recommendations` | Client | `RecommendationResults` |

---

# FLOW 2: Project Creation

> **Mục tiêu:** Tạo Project qua 1 trong 2 con đường độc lập — Path A (Job → Proposal) hoặc Path B (Service → Request → Offer). Cả 2 đều tạo ra cùng 1 loại `Projects` row rồi join vào Flow 3.
> **Actors:** Expert, Client.

## FLOW 2 — PATH A: Proposal & Project Creation

> **Mục tiêu:** Expert nộp proposal, Client chọn 1 expert, System tạo project.
> **Status:** `Proposal: NULL → SUBMITTED → ACCEPTED / REJECTED / WITHDRAWN`, `Job: OPEN → IN_PROGRESS`, `Project: NULL → PENDING_PAYMENT`
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
    { "title": "Phân tích yêu cầu & prototype", "description": "...", "amount": 500.00, "dueDays": 7, "acceptanceCriteria": "Prototype chạy được với 10 intent cơ bản" },
    { "title": "Tích hợp website & testing", "description": "...", "amount": 700.00, "dueDays": 18, "acceptanceCriteria": "Chatbot hoạt động trên website, pass 90% test cases" }
  ]
}
```

**Validation:** 1 proposal / expert / job. `JobPosts.Status = OPEN`. `proposedBudget ≥ 0`.

**Status transition:** `Proposals: none → SUBMITTED`.

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

**Auth:** `ExpertPolicy`, chủ proposal. **Status transition:** `SUBMITTED / SHORTLISTED → WITHDRAWN`.

## 2.6. Shortlist / Unshortlist proposal

```
PUT /api/v1/proposals/{proposalId}/shortlist
PUT /api/v1/proposals/{proposalId}/unshortlist
```

**Auth:** `ClientPolicy`, chủ job. **Status transition:** `SUBMITTED ↔ SHORTLISTED`.

## 2.7. Reject proposal

```
PUT /api/v1/proposals/{proposalId}/reject
```

**Auth:** `ClientPolicy`, chủ job. **Status transition:** `SUBMITTED / SHORTLISTED → REJECTED`.

## 2.8. Resubmit proposal

```
PUT /api/v1/proposals/{proposalId}
```

**Auth:** `ExpertPolicy`, chủ proposal. Cập nhật + resubmit proposal (VD sau khi bị reject).

## 2.9. Accept proposal (atomic — bắt buộc transaction)

```
PUT /api/v1/proposals/{proposalId}/accept
```

**Auth:** `ClientPolicy`, chủ job.

**Preconditions:** `JobPosts.Status = OPEN`. Proposal Status = `SUBMITTED` hoặc `SHORTLISTED`.

**Transaction:**
1. Validate job / proposal / client / expert.
2. Selected proposal → `ACCEPTED`.
3. Sibling proposals (`SUBMITTED` / `SHORTLISTED`) → `REJECTED`.
4. `JobPosts.Status = IN_PROGRESS`.
5. Tạo `Projects` (Status = `PENDING_PAYMENT`).
6. Tạo `Milestones` từ `ProposalMilestones` (Status = `CREATED`).
7. Commit. Emit `JobStatusUpdated` (status=in_progress) tới Client qua SignalR.

**Rollback:** nếu bất kỳ bước nào fail → rollback toàn bộ.

## Flow 2 Path A — API Summary

| # | Method | Endpoint | Auth | Tables |
|---|--------|----------|------|--------|
| 1 | POST | `/jobs/{id}/proposals` | Expert | `Proposals`, `ProposalMilestones` |
| 2 | GET | `/jobs/{id}/proposals` | Client | `Proposals` |
| 3 | GET | `/proposals/{id}` | Client/Expert | `Proposals`, `ProposalMilestones` |
| 4 | GET | `/proposals/me` | Expert | `Proposals` |
| 5 | PUT | `/proposals/{id}` (resubmit) | Expert | `Proposals` |
| 6 | PUT | `/proposals/{id}/withdraw` | Expert | `Proposals` |
| 7 | PUT | `/proposals/{id}/shortlist` | Client | `Proposals` |
| 8 | PUT | `/proposals/{id}/unshortlist` | Client | `Proposals` |
| 9 | PUT | `/proposals/{id}/reject` | Client | `Proposals` |
| 10 | PUT | `/proposals/{id}/accept` | Client | `Proposals`, `JobPosts`, `Projects`, `Milestones` |

---

## FLOW 2 — PATH B: Service & Project Creation

> **Mục tiêu:** Expert publish 1 service có sẵn packages, Client request 1 package, Expert gửi offer, Client accept offer → System tạo project. Không dùng `JobPosts`/`Proposals`.
> **Status:** `Service: NULL → DRAFT → PUBLISHED`, `ServiceRequest: NULL → PENDING → ACCEPTED / DECLINED`, `ServiceOffer: NULL → PENDING → ACCEPTED`, `Project: NULL → PENDING_PAYMENT`
> **Tables:** `Services` (`ServiceListing`), `ServicePackages`, `ServiceFaqs`, `ServiceRequests`, `ServiceOffers`, `ServiceOfferMilestones`, `Projects`, `Milestones`, `Conversations`

### 2B.1. Tạo service (draft)

```
POST /api/v1/services
```

**Auth:** `ExpertPolicy`.

**Request body (`CreateServiceRequest`):**
```json
{
  "title": "AI Chatbot Development",
  "description": "I build production-ready AI chatbots for e-commerce.",
  "attachmentUrl": "https://res.cloudinary.com/.../portfolio.pdf",
  "packages": [
    { "tier": "BASIC", "title": "Basic", "description": "Simple FAQ bot", "price": 500, "deliveryDays": 7, "features": ["FAQ bot", "1 revision"] },
    { "tier": "STANDARD", "title": "Standard", "description": "Bot with product recommendation", "price": 1200, "deliveryDays": 14, "features": ["FAQ bot", "Product recommendation", "3 revisions"] }
  ],
  "faqs": [
    { "question": "How long does delivery take?", "answer": "7-30 days depending on the package." }
  ]
}
```

**Validation:** `title`, `description` bắt buộc. Ít nhất 1 package (`price > 0`, `deliveryDays` trong khoảng hợp lệ) và 1 FAQ.

**Side effects:** Tạo `Services` (Status = `DRAFT`) + `ServicePackages` + `ServiceFaqs`.

**Status:** `201 Created`, `400` (validation), `403` (Client gọi endpoint này).

### 2B.2. Cập nhật service (partial update)

```
PUT /api/v1/services/{id}
```

**Auth:** `ExpertPolicy`, chủ service. Body giống `CreateServiceRequest` nhưng mọi field optional (`null` = không đổi). Gửi `packages`/`faqs` sẽ **thay toàn bộ** tập cũ bằng tập mới (không phải merge từng item).

### 2B.3. Publish service

```
POST /api/v1/services/{id}/publish
```

**Auth:** `ExpertPolicy`, chủ service. **Validation:** phải có ít nhất 1 package và 1 FAQ.

**Status transition:** `DRAFT → PUBLISHED`. **Side effects:** `PublishedAt = UTC now`.

### 2B.4. Unpublish service

```
POST /api/v1/services/{id}/unpublish
```

**Auth:** `ExpertPolicy`, chủ service. **Status transition:** `PUBLISHED → DRAFT`.

### 2B.5. Xem danh sách service đã publish (public)

```
GET /api/v1/services?pageIndex=1&pageSize=20&searchTerm=
```

**Auth:** không bắt buộc. Chỉ trả service `Status = PUBLISHED`.

### 2B.6. Xem service của tôi (Expert)

```
GET /api/v1/services/mine
```

**Auth:** `ExpertPolicy`. Trả cả `DRAFT` và `PUBLISHED`.

### 2B.7. Xem chi tiết service

```
GET /api/v1/services/{id}
```

**Auth:** không bắt buộc cho service `PUBLISHED`; chủ service xem được cả khi `DRAFT`.

### 2B.8. Client gửi request cho 1 package

```
POST /api/v1/services/{id}/requests
```

**Auth:** `ClientPolicy`.

**Request body (`CreateServiceRequestRequest`):**
```json
{ "packageId": "guid", "note": "Cần bot hỗ trợ tiếng Việt và tiếng Anh." }
```

**Preconditions:** `Service.Status = PUBLISHED`. Client không phải chủ service. Client chưa có request `PENDING` nào khác trên service này.

**Side effects:** Tạo `ServiceRequests` (Status = `PENDING`), snapshot `PackageTitle`/`PackagePrice`/`PackageDeliveryDays` tại thời điểm request (edit package sau đó không ảnh hưởng request đã gửi).

**Status:** `201 Created`, `400` (chưa publish / đã có request pending / tự request service của mình), `404` (package không thuộc service).

### 2B.9. Expert xem requests của 1 service

```
GET /api/v1/services/{id}/requests
```

**Auth:** `ExpertPolicy`, chủ service.

### 2B.10. Expert xem tất cả service requests của mình (mọi service)

```
GET /api/v1/experts/me/service-requests?status=
```

**Auth:** `ExpertPolicy`. Query optional `status` (`PENDING`/`ACCEPTED`/`DECLINED`).

### 2B.11. Client xem service requests của mình

```
GET /api/v1/clients/me/service-requests?pageIndex=1&pageSize=20&status=
```

**Auth:** `ClientPolicy`. Có phân trang + search theo tên service/package.

### 2B.12. Xem chi tiết 1 service request

```
GET /api/v1/service-requests/{id}
```

**Auth:** `ClientOrExpertPolicy` — chỉ Client gửi hoặc Expert sở hữu service mới xem được.

### 2B.13. Expert accept service request

```
POST /api/v1/service-requests/{id}/accept
```

**Auth:** `ExpertPolicy`, chủ service. **Preconditions:** `Status = PENDING`.

**Side effects:** `Status → ACCEPTED`. Tạo/mở `Conversations` giữa Client và Expert (`serviceRequestId` gắn kèm).

### 2B.14. Expert decline service request

```
POST /api/v1/service-requests/{id}/decline
```

**Auth:** `ExpertPolicy`, chủ service. **Preconditions:** `Status = PENDING`. **Side effects:** `Status → DECLINED`.

### 2B.15. Expert gửi offer

```
POST /api/v1/service-requests/{id}/offers
```

**Auth:** `ExpertPolicy`, chủ service.

**Request body (`CreateServiceOfferRequest`):**
```json
{
  "amount": 1200.00,
  "milestones": [
    { "title": "Prototype", "description": "Basic bot with FAQ", "amount": 500, "dueDays": 7, "acceptanceCriteria": "Bot trả lời đúng 10 câu FAQ mẫu", "orderIndex": 1 },
    { "title": "Final delivery", "description": "Full integration", "amount": 700, "dueDays": 14, "acceptanceCriteria": "Bot hoạt động trên website thật", "orderIndex": 2 }
  ]
}
```

**Preconditions:** `ServiceRequest.Status = ACCEPTED`. `amount > 0`. Ít nhất 1 milestone, mỗi milestone `amount > 0` và `dueDays` hợp lệ.

**Side effects:** Tạo `ServiceOffers` (Status = `PENDING`) + `ServiceOfferMilestones`.

**Status:** `201 Created`, `400` (validation / request chưa accepted), `403` (không phải chủ service).

### 2B.16. Client accept offer (atomic — bắt buộc transaction)

```
POST /api/v1/service-offers/{id}/accept
```

**Auth:** `ClientPolicy`, chính là Client đã gửi request gốc.

**Preconditions:** `ServiceOffer.Status = PENDING`.

**Transaction:**
1. Validate offer `PENDING`, người gọi là chủ `ServiceRequest` gốc.
2. `Offer.Status = ACCEPTED`.
3. Tạo `Projects` (Status = `PENDING_PAYMENT`), `ServiceRequestId` gắn kèm, `JobId`/`AcceptedProposalId = null`.
4. Tạo `Milestones` từ `ServiceOfferMilestones`.
5. Commit.

**Race condition guard:** partial unique index trên `Projects.ServiceRequestId` — nếu 2 accept chạy đồng thời trên cùng 1 service request, lần commit thứ 2 sẽ bắt lỗi `23505` và trả `400` ("This service request already has an accepted offer.") thay vì tạo project trùng.

**Status:** `200`, `400` (offer không còn `PENDING` / request đã có project), `403` (không phải Client gốc), `404`.

### 2B.17. Client xem offer của 1 service request

```
GET /api/v1/service-requests/{id}/offer
```

**Auth:** `ClientPolicy`, chủ request. Trả offer mới nhất (ưu tiên `ACCEPTED` nếu có, không thì offer mới nhất theo `CreatedAt`).

## Flow 2 Path B — API Summary

| # | Method | Endpoint | Auth | Tables |
|---|--------|----------|------|--------|
| 1 | POST | `/services` | Expert | `Services`, `ServicePackages`, `ServiceFaqs` |
| 2 | PUT | `/services/{id}` | Expert | `Services`, `ServicePackages`, `ServiceFaqs` |
| 3 | POST | `/services/{id}/publish` | Expert | `Services` |
| 4 | POST | `/services/{id}/unpublish` | Expert | `Services` |
| 5 | GET | `/services` | — | `Services` |
| 6 | GET | `/services/mine` | Expert | `Services` |
| 7 | GET | `/services/{id}` | — | `Services` |
| 8 | POST | `/services/{id}/requests` | Client | `ServiceRequests` |
| 9 | GET | `/services/{id}/requests` | Expert | `ServiceRequests` |
| 10 | GET | `/experts/me/service-requests` | Expert | `ServiceRequests` |
| 11 | GET | `/clients/me/service-requests` | Client | `ServiceRequests` |
| 12 | GET | `/service-requests/{id}` | Client/Expert | `ServiceRequests` |
| 13 | POST | `/service-requests/{id}/accept` | Expert | `ServiceRequests`, `Conversations` |
| 14 | POST | `/service-requests/{id}/decline` | Expert | `ServiceRequests` |
| 15 | POST | `/service-requests/{id}/offers` | Expert | `ServiceOffers`, `ServiceOfferMilestones` |
| 16 | POST | `/service-offers/{id}/accept` | Client | `ServiceOffers`, `Projects`, `Milestones` |
| 17 | GET | `/service-requests/{id}/offer` | Client | `ServiceOffers`, `ServiceOfferMilestones` |

---

# FLOW 3: Milestone, Escrow & Deliverable

> **Mục tiêu:** Client & Expert xác nhận milestones, Client fund escrow, Expert nộp deliverable, Client review.
> **Actors:** Client, Expert.
> **⚠️ Mô hình tiền KHÔNG phải "hold rồi release".** Toàn bộ tiền đi qua `Treasury` (`Aivora.Services/Treasury/Treasury.cs`). Fund trả ngay 30% (`DepositRate`, default) thẳng Client → Expert; Approve trả nốt 70% trừ 10% commission platform (`CommissionRate`, default). Mọi `Payments` row tạo ra đều `Status = RELEASED` ngay lập tức — enum `HELD`/`FROZEN`/`PARTIALLY_RELEASED` tồn tại nhưng **không bao giờ được gán** trong pipeline hiện tại.
> **Status:**
> - `Project: CREATED → PENDING_PAYMENT → ACTIVE → IN_REVIEW → COMPLETED` (hoặc `DISPUTED`) — completion hoàn toàn tự động qua `Treasury.SyncProjectStatusAsync`, không có action "hoàn thành project" thủ công.
> - `Milestone: CREATED → IN_PROGRESS → SUBMITTED → RELEASED` (thực tế — enum có thêm `FUNDED`/`APPROVED`/`COMPLETED` nhưng không bao giờ được set; revision: `SUBMITTED → REVISION_REQUESTED → SUBMITTED`, dispute: `IN_PROGRESS/SUBMITTED → DISPUTED`)
> - `Payment: NULL → RELEASED` (2 row/milestone: deposit lúc Fund, remaining lúc Approve — cả 2 đều tạo `RELEASED` ngay, không có giai đoạn `HELD`/`FROZEN` nào)
> **Tables:** `Projects`, `Milestones`, `MilestoneSteps`, `Payments`, `Wallets`, `WalletTransactions`, `Deliverables`, `Disputes`, `DisputeEvidences`

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
{ "title": "Milestone mới", "description": "Mô tả", "amount": 300.00, "dueDate": "2026-07-01T00:00:00Z", "acceptanceCriteria": "Tiêu chí chấp nhận", "orderIndex": 2 }
```

**Validation:** `amount ≥ 0`.

## 3.5. Xem chi tiết milestone

```
GET /api/v1/milestones/{milestoneId}
```

**Auth:** tham gia project. **Response bao gồm** `steps[]` nếu milestone có milestone steps.

## 3.6. Cập nhật milestone

```
PUT /api/v1/milestones/{milestoneId}
```

**Auth:** `ClientPolicy`, chủ project. Body: `{ title?, description?, acceptanceCriteria?, amount?, dueDate? }`.

## 3.7. Milestone Steps

Granular task tracking bên trong 1 milestone.

```
GET  /api/v1/milestones/{milestoneId}/steps
```
**Auth:** tham gia project. Response: `MilestoneStepResponse[]` — `{ id, milestoneId, title, description?, orderIndex, status, dueDate?, completedAt?, completedByUserId?, blockedReason? }`.

```
POST /api/v1/milestones/{milestoneId}/steps
```
**Auth:** `ExpertPolicy`. Body: `{ title, description?, dueDate?, orderIndex }`.

```
POST /api/v1/milestones/{milestoneId}/steps/suggest
```
**Auth:** `ExpertPolicy`. **Rate Limit:** `AI`. AI đề xuất danh sách step. Response: `{ steps: [{ title, description? }], aiModel }`.

```
PUT  /api/v1/milestones/{milestoneId}/steps/reorder
```
**Auth:** `ExpertPolicy`. Body: `List<Guid>` (stepIds theo thứ tự mới).

```
PUT    ~/api/v1/steps/{stepId}
DELETE ~/api/v1/steps/{stepId}
```
**Auth:** `ExpertPolicy`. PUT body: `{ title?, description?, dueDate?, orderIndex? }` (partial update).

```
PUT ~/api/v1/steps/{stepId}/status
```
**Auth:** `ClientOrExpertPolicy`. Body: `{ status: "PENDING"|"IN_PROGRESS"|"COMPLETED"|"SKIPPED"|"BLOCKED", reason? }`.

> Lưu ý route: các step endpoint không lồng theo `milestones/{id}/steps/{stepId}` mà đứng riêng ở `~/api/v1/steps/{id}` (trừ list/create/suggest/reorder vẫn ở dưới `milestones/{id}/steps`).

> **3 step hệ thống tự sinh:** `"Created"` (lúc tạo milestone), `"Funded"` (lúc fund), `"Completed"` (lúc approve) — title reserved, Expert không tự tạo/sửa/xóa được (guard `IsSystemDefaultStep` trong `MilestoneService`). `GET .../steps` trả trộn chung các step này với step Expert tự tạo, chỉ phân biệt được qua `Title`. `POST .../steps/suggest` chỉ trả draft AI gợi ý, KHÔNG tự lưu DB — Expert phải tự `POST .../steps` từng cái nếu muốn dùng.

## 3.8. Ví (Wallet)

```
GET /api/v1/wallet/me
```
**Auth:** bắt buộc. Response: `{ availableBalance, heldBalance, totalEarned, currency, updatedAt }`.

```
GET /api/v1/wallet/transactions?pageIndex=1&pageSize=20&searchTerm=
```
**Auth:** bắt buộc. Response: trang `TransactionResponse[]` — `{ id, walletId, paymentId?, type, direction, amount, balanceBefore, balanceAfter, description?, createdAt }`.

```
POST /api/v1/wallet/deposit-demo
```
**Auth:** `ClientPolicy`. Body: `{ amount, description? }`. Cộng thẳng tiền vào ví (`Type = DEMO_DEPOSIT`) — chỉ dùng cho dev/test, không qua cổng thanh toán thật.

```
POST /api/v1/wallet/deposit
```
**Auth:** `ClientPolicy`. Body: `{ amount, paymentMethod?: "credit_card"|"bank_transfer"|"crypto", paymentToken?, description? }`. Nạp tiền qua phương thức chung (khác với luồng VNPay ở dưới).

```
POST /api/v1/wallet/vnpay/deposit
```
**Auth:** `ClientPolicy`. Body: `{ amount }`. Response: `{ paymentUrl, txnRef }` — Client redirect sang VNPay.

```
GET /api/v1/wallet/vnpay-ipn
```
**Auth:** `AllowAnonymous` (callback server-to-server từ VNPay). Query: đầy đủ tham số VNPay (`vnp_TxnRef`, `vnp_Amount`, `vnp_ResponseCode`, `vnp_SecureHash`,...). Verify hash → nếu `vnp_ResponseCode == "00"` thì cộng `Wallet.AvailableBalance`. Response **không bọc `ApiResponse`**: `{ RspCode: "00"|"99", Message }`. Duplicate `vnp_TxnRef` → trả success nhưng không cộng tiền lần 2.

```
GET /api/v1/wallet/vnpay-return
```
**Auth:** `AllowAnonymous`. Callback trình duyệt redirect user về sau khi thanh toán — server redirect tiếp sang `{FrontendUrl}/payment-result?...`.

```
POST /api/v1/wallet/withdraw
```
**Auth:** `WithdrawPolicy`. Body: `{ amount, description?, paymentMethod?: "bank"|"paypal"|"crypto" }`.

```
POST /api/v1/wallet/transfer/{expertId}
```
**Auth:** `ClientPolicy`. Chuyển tiền trực tiếp Client → Expert, ngoài luồng escrow. Body: `{ amount, description? }`.

## 3.9. Fund milestone — trả deposit ngay (atomic — bắt buộc transaction)

```
PUT /api/v1/milestones/{milestoneId}/fund
```

**Auth:** `ClientPolicy`, chủ project. Xử lý bởi `MilestoneService.FundMilestoneAsync` → forward toàn bộ sang `Treasury.PayDepositAsync`.

**Preconditions:** `Milestone.Status = CREATED`. `Wallet.AvailableBalance ≥ depositAmount` (KHÔNG phải ≥ `Milestone.Amount` đầy đủ).

**Transaction (`Treasury.PayDepositAsync`):**
1. `depositAmount = Milestone.Amount × EscrowOptions.DepositRate` (default **30%**).
2. Kiểm tra số dư ≥ `depositAmount`.
3. `Milestone.Status = CREATED → IN_PROGRESS` (claim trước khi chuyển tiền — dùng làm optimistic-concurrency token, request fund trùng lặp/đồng thời sẽ thua và nhận lỗi `400` thay vì double-fund).
4. `Client.Wallet.AvailableBalance -= depositAmount`.
5. `Expert.Wallet.AvailableBalance += depositAmount`, `Expert.Wallet.TotalEarned += depositAmount` — **chuyển thẳng, không giữ ở platform**.
6. Tạo `Payments` — **`Status = RELEASED` ngay lập tức**, không phải `HELD`.
7. Tạo 2 `WalletTransactions` (Client: `PAYMENT_RELEASE`/`DEBIT`; Expert: `PAYMENT_RELEASE`/`CREDIT`), amount = `depositAmount`.
8. Tự thêm `MilestoneStep` title `"Funded"` (status `COMPLETED`) nếu chưa có step title này.
9. Nếu `Project.Status = PENDING_PAYMENT` → `ACTIVE`.
10. Commit.

**Lỗi:** `400` nếu số dư không đủ cho `depositAmount`, hoặc milestone không còn `CREATED` (race thua cũng rơi vào nhánh này, message giống lỗi tuần tự bình thường).

**Response (`FundResultResponse`):** `{ milestone, payment: { id, projectId, milestoneId, payerId, payeeId, amount, currency, status, heldAt }, wallet: { availableBalance, heldBalance, currency } }` — `payment.status` thực tế luôn là `"RELEASED"` dù field response tên là `heldAt`.

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
{ "description": "Đã hoàn thành prototype chatbot với 15 intent", "fileUrl": "https://res.cloudinary.com/.../prototype.zip", "demoUrl": "https://chatbot-demo.example.com", "sourceCodeUrl": "https://github.com/expert/chatbot-prototype", "note": "Vui lòng test trên Chrome" }
```

**Preconditions:** `Milestone.Status ∈ { FUNDED, IN_PROGRESS, REVISION_REQUESTED }` (code check cả 3 giá trị, nhưng `FUNDED` thực tế không bao giờ xảy ra — Fund luôn set `IN_PROGRESS`, xem §3.9 — nên trong thực tế chỉ `IN_PROGRESS`/`REVISION_REQUESTED` khớp).

**Validation:** phải có ít nhất 1 trong `description`, `fileUrl`, `demoUrl`, `sourceCodeUrl`, `note`.

**Status transitions:** `Milestone → SUBMITTED` (kèm `SubmittedAt`). `Deliverable` tạo mới `Status = SUBMITTED` (`RevisionNumber` tăng dần mỗi lần nộp lại). `Project: ACTIVE → IN_REVIEW` (qua `Treasury.SyncProjectStatusAsync`).

## 3.12. Xem danh sách deliverable của milestone

```
GET /api/v1/milestones/{milestoneId}/deliverables
```

**Auth:** tham gia project.

## 3.13. Approve deliverable — trả nốt remaining, trừ commission (atomic — bắt buộc transaction)

```
PUT /api/v1/milestones/{milestoneId}/approve
```

**Auth:** `ClientPolicy`, chủ project. Xử lý bởi `MilestoneService.ApproveMilestoneAsync` → forward toàn bộ sang `Treasury.PayRemainingAsync`.

**⚠️ `Deliverable.Status` KHÔNG đổi thành `APPROVED`.** Không có bất kỳ code path nào trong `Treasury`/`MilestoneService`/`DeliverableService` set `Deliverable.Status = APPROVED`. Deliverable mãi ở `SUBMITTED`, `ReviewedAt` mãi `null`. Muốn biết deliverable "đã duyệt" chưa, phải kiểm tra `Milestone.Status == RELEASED`.

**Preconditions:** `Milestone.Status = SUBMITTED`. Không có dispute `OPEN`/`UNDER_REVIEW` nào khác trên milestone. `Client.Wallet.AvailableBalance ≥ remainingAmount`.

**Transaction (`Treasury.PayRemainingAsync`):**
1. `remainingAmount = Milestone.Amount × EscrowOptions.RemainingRate` (default **70%**).
2. `commissionAmount = Milestone.Amount × CommissionOptions.Rate` (default **10% của TOÀN BỘ `Milestone.Amount`**, không phải của `remainingAmount`).
3. `expertAmount = remainingAmount − commissionAmount`.
4. `Milestone.Status = SUBMITTED → RELEASED` (claim trước khi chuyển tiền, cùng cơ chế concurrency token như Fund — bỏ qua hoàn toàn giá trị `APPROVED` trong enum).
5. `Client.Wallet.AvailableBalance -= remainingAmount`.
6. `Expert.Wallet.AvailableBalance += expertAmount`, `TotalEarned += expertAmount`.
7. `Platform.Wallet.AvailableBalance += commissionAmount` (ví hệ thống `SystemConstants.SystemUserId`), `TotalEarned += commissionAmount`.
8. Tạo `Payments` — **`Status = RELEASED` ngay** (row thứ 2 của milestone này, row đầu đã tạo lúc Fund).
9. Tạo `WalletTransactions`: Client (`PAYMENT_RELEASE`/`DEBIT`, amount=`remainingAmount`), Expert (`PAYMENT_RELEASE`/`CREDIT`, amount=`expertAmount`), Platform (`PLATFORM_FEE`/`CREDIT`, amount=`commissionAmount`, chỉ ghi nếu > 0).
10. Tự thêm `MilestoneStep` title `"Completed"` (status `COMPLETED`) nếu chưa có, đồng thời auto-complete mọi step PENDING/IN_PROGRESS/BLOCKED còn lại.
11. `Treasury.SyncProjectStatusAsync`: nếu **mọi** milestone của project đều `RELEASED`/`REFUNDED` → `Project.Status = COMPLETED` (+ `JobPosts.Status = COMPLETED` nếu project có `JobId`, emit `JobStatusUpdated`). Ngược lại → `Project.Status = ACTIVE`.
12. Commit.

**Đây là điểm release payment duy nhất cho phần remaining — không có endpoint "release payment" riêng.** Deposit (30%) đã release riêng từ lúc Fund (§3.9) — approve không đụng lại khoản đó.

## 3.14. Request revision

```
PUT /api/v1/milestones/{milestoneId}/request-revision
```

**Auth:** `ClientPolicy`, chủ project. Body: `{ reason }` (string).

**Status transitions:** `Milestone: SUBMITTED → REVISION_REQUESTED`. `Project: IN_REVIEW → ACTIVE` (qua `Treasury.SyncProjectStatusAsync`). **`Deliverable.Status` không đổi** (mãi `SUBMITTED`). **Không có `Payments`/`Wallets` nào bị đụng** — deposit đã trả từ lúc fund vẫn nằm nguyên trong ví Expert, không có gì để "giữ".

## 3.15. Open dispute (atomic — bắt buộc transaction)

```
POST /api/v1/milestones/{milestoneId}/dispute
```

**Auth:** bắt buộc (Client hoặc Expert của project). Body: `{ reason }` (string).

**Transaction:**
1. Tạo `Disputes` (Status = `OPEN`).
2. `Milestone.Status = DISPUTED`.
3. `Project.Status = DISPUTED`.
4. Commit.

**Payment không đổi.** `PaymentStatus.FROZEN` tồn tại trong enum nhưng không code path nào gán — deposit đã trả (nếu có) vẫn RELEASED nguyên trong ví Expert suốt thời gian dispute mở.

## Flow 3 — API Summary

| # | Method | Endpoint | Auth | Tables |
|---|--------|----------|------|--------|
| 1 | GET | `/projects` | Client/Expert | `Projects` |
| 2 | GET | `/projects/{id}` | Participant | `Projects`, `Milestones` |
| 3 | PUT | `/projects/{id}/cancel` | Client | `Projects` |
| 4 | POST | `/projects/{id}/milestones` | Client | `Milestones` |
| 5 | GET | `/milestones/{id}` | Participant | `Milestones` |
| 6 | PUT | `/milestones/{id}` | Client | `Milestones` |
| 7 | GET | `/milestones/{id}/steps` | Participant | `MilestoneSteps` |
| 8 | POST | `/milestones/{id}/steps` | Expert | `MilestoneSteps` |
| 9 | POST | `/milestones/{id}/steps/suggest` | Expert | — |
| 10 | PUT | `/milestones/{id}/steps/reorder` | Expert | `MilestoneSteps` |
| 11 | PUT | `~/steps/{id}` | Expert | `MilestoneSteps` |
| 12 | DELETE | `~/steps/{id}` | Expert | `MilestoneSteps` |
| 13 | PUT | `~/steps/{id}/status` | Client/Expert | `MilestoneSteps` |
| 14 | GET | `/wallet/me` | Any | `Wallets` |
| 15 | GET | `/wallet/transactions` | Any | `WalletTransactions` |
| 16 | POST | `/wallet/deposit-demo` | Client | `Wallets`, `WalletTransactions` |
| 17 | POST | `/wallet/deposit` | Client | `Wallets`, `WalletTransactions` |
| 18 | POST | `/wallet/vnpay/deposit` | Client | — |
| 19 | GET | `/wallet/vnpay-ipn` | — | `Wallets`, `WalletTransactions` |
| 20 | GET | `/wallet/vnpay-return` | — | — |
| 21 | POST | `/wallet/withdraw` | Any (`WithdrawPolicy`) | `Wallets`, `WalletTransactions` |
| 22 | POST | `/wallet/transfer/{expertId}` | Client | `Wallets`, `WalletTransactions` |
| 23 | PUT | `/milestones/{id}/fund` | Client | `Wallets`, `Payments`, `WalletTransactions`, `Milestones`, `Projects` |
| 24 | PUT | `/milestones/{id}/approve` | Client | `Deliverables`, `Milestones`, `Payments`, `Wallets`, `WalletTransactions`, `Projects`, `JobPosts` |
| 25 | PUT | `/milestones/{id}/request-revision` | Client | `Deliverables`, `Milestones`, `Projects` |
| 26 | POST | `/milestones/{id}/dispute` | Participant | `Disputes`, `Milestones`, `Payments`, `Projects` |
| 27 | GET | `/milestones/{id}/deliverables` | Participant | `Deliverables` |
| 28 | POST | `/milestones/{id}/deliverables` | Expert | `Deliverables`, `Milestones`, `Projects` |
| 29 | GET | `/payments/history` | Any | `Payments` |

---

# FLOW 4: Completion, Payment, and Review

> **Mục tiêu:** System release payment (đã cover ở Flow 3), complete project, xử lý dispute (nếu có), cho phép review.
> **Actors:** Client, Expert, System. Admin chỉ tham gia khi có dispute (vai trò **quan sát**, không phán quyết — nền tảng không tự đứng ra giải quyết dispute, Client/Expert tự thương lượng qua milestone) hoặc duyệt hồ sơ expert.
> **Status:** `Project: COMPLETED`, `Job: COMPLETED`, `Payment: RELEASED`, `Reviews: created`
> **Tables:** `Projects`, `JobPosts`, `Milestones`, `Payments`, `Wallets`, `WalletTransactions`, `Reviews`, `Disputes`, `DisputeEvidences`

> **Lưu ý:** Release payment, approve deliverable, request revision, open dispute đã liệt kê ở Flow 3. Flow 4 tập trung vào **dispute handling** và **review**.
> **Refund/Split dead code:** `Treasury.RefundMilestoneAsync` và `SplitMilestoneFundsAsync` đã implement đầy đủ (clawback qua `CommissionOptions.MaxDebtLimit`, chia lại tiền) nhưng KHÔNG controller/service nào gọi tới — dispute resolve hiện tại không tự động refund hay split tiền, xem §4.8.

## 4.1. Mở dispute trực tiếp

```
POST /api/v1/disputes
```

**Auth:** `ClientPolicy` hoặc `ExpertPolicy`. Mở dispute mà không cần qua milestone endpoint.

## 4.2. Xem danh sách disputes của user

```
GET /api/v1/disputes
```

**Auth:** bắt buộc. Trả về disputes mà user tham gia.

## 4.3. Xem chi tiết dispute

```
GET /api/v1/disputes/{disputeId}
```

**Auth:** tham gia dispute. Response bao gồm `evidences[]` lồng sẵn.

## 4.4. Thêm evidence

```
POST /api/v1/disputes/{disputeId}/evidence
```

**Auth:** tham gia dispute (Client/Expert/Admin).

**Request body (`AddEvidenceRequest`):**
```json
{ "content": "Screenshot của deliverable", "fileUrl": "https://res.cloudinary.com/.../evidence.png" }
```

| Field | Type | Required |
|-------|------|----------|
| `content` | string | **Yes** |
| `fileUrl` | string? | No |

## 4.5. Close dispute (người mở dispute tự close)

```
PUT /api/v1/disputes/{disputeId}/close
```

**Auth:** bắt buộc. Chỉ `OpenedBy` mới được close. **Side effects:** `Disputes.Status = CLOSED`. `Milestone` unlock về `IN_PROGRESS` (**luôn luôn**, bất kể deliverable đã từng `SUBMITTED` hay chưa — khác với Resolve ở §4.8 vốn unlock thông minh theo `SubmittedAt`; đây là inconsistency giữa 2 code path, chưa có ticket). `Project.Status → ACTIVE` nếu không còn milestone `DISPUTED` khác. **Validation:** dispute đã `RESOLVED`/`CLOSED` thì không close lại được.

## 4.6. Admin yêu cầu bổ sung bằng chứng

```
PUT /api/v1/disputes/{disputeId}/request-evidence
```

**Auth:** `AdminPolicy`. Body: `{ note }`.

**Side effects:** nếu `Status == OPEN` → `UNDER_REVIEW`. Tạo `Notification` (Type = `DISPUTE`) cho `OpenedBy`.

## 4.7. Xóa dispute evidence

```
DELETE /api/v1/disputes/{disputeId}/evidence/{evidenceId}
```

**Auth:** `OpenedBy` hoặc `SubmittedBy` của evidence đó. **Validation:** dispute đã `RESOLVED`/`CLOSED` thì không xóa được.

## 4.8. Resolve dispute

```
PUT /api/v1/disputes/{disputeId}/resolve
```

**Auth:** `AdminPolicy`.

> ⚠️ **Admin ở đây chỉ quan sát và ghi nhận, không phán quyết.** Nền tảng không đứng ra giải quyết dispute — `resolutionNote` là ghi chú của Admin sau khi xem evidence, không phải quyết định thắng/thua. Sau khi resolve, Client và Expert **tự** giải quyết tiếp với nhau qua hành động milestone bình thường (Client approve để release tiền, hoặc request-revision).

**Request body — CHỈ có 1 field:**
```json
{ "resolutionNote": "Đã xem xét evidence từ cả hai bên." }
```

| Field | Type | Required |
|-------|------|----------|
| `resolutionNote` | string | **Yes** |

> Bản thiết kế cũ có `resolutionType` (`RELEASE_TO_EXPERT`/`REFUND_TO_CLIENT`/`SPLIT_PAYMENT`/`REQUEST_REVISION`) + `splitPercentage` để Admin tự quyết và tự động cập nhật `Payments`/`Wallets` — **toàn bộ đã bị gỡ ở issue #94**. Enum `DisputeResolutionType` không còn tồn tại trong code. Nền tảng chủ động rút khỏi vai trò trọng tài tài chính.

**Side effects:**
- `Disputes.Status = RESOLVED`, ghi `ResolutionNote`, `AdminId`, `ResolvedAt`.
- `Milestone` được **unlock**: → `SUBMITTED` nếu deliverable đã từng nộp (`SubmittedAt` có giá trị), ngược lại → `IN_PROGRESS`.
- `Project.Status → ACTIVE` nếu không còn milestone nào khác trong project đang `DISPUTED`.
- **`Payments` và `Wallets` không đổi** — Admin resolve không chuyển tiền, chỉ mở khóa để Client/Expert tự xử lý tiếp.

**Sau khi resolve:** milestone đã unlock nên Client có thể approve (release tiền cho Expert) hoặc request-revision như bình thường — đây là bước Client/Expert tự thương lượng, không có shortcut Admin nào can thiệp tài chính.

**Validation:** chỉ Admin mới resolve được. `resolutionNote` bắt buộc. Dispute đã `RESOLVED` thì không resolve lại được (dispute `CLOSED` vẫn resolve được — code chỉ chặn `RESOLVED`).

## 4.9. Tạo review

```
POST /api/v1/reviews
```

**Auth:** bắt buộc, tham gia project.

**Request body:**
```json
{ "projectId": "guid", "revieweeId": "guid", "rating": 5, "comment": "Expert làm việc rất chuyên nghiệp, deliverable đúng hạn.", "communicationRating": 5, "qualityRating": 5, "deadlineRating": 5, "requirementClarityRating": 5 }
```

**Preconditions:** `Project.Status = COMPLETED`. Reviewer tham gia project. Chưa review cặp (reviewer, reviewee, project) này.

**Validation:** `rating ∈ [1,5]`. `revieweeId ≠ reviewerId`.

**Side effects:** tạo `Reviews`. Cập nhật `ExpertProfiles.RatingAvg` + `CompletedProjects` (nếu reviewee là expert).

## 4.10. Xem reviews của user

```
GET /api/v1/users/{userId}/reviews?pageIndex=1&pageSize=20
```

**Auth:** không bắt buộc (public).

## Flow 4 — API Summary

| # | Method | Endpoint | Auth | Tables |
|---|--------|----------|------|--------|
| 1 | POST | `/disputes` | Client/Expert | `Disputes` |
| 2 | GET | `/disputes` | Client/Expert | `Disputes` |
| 3 | GET | `/disputes/{id}` | Participant | `Disputes`, `DisputeEvidences` |
| 4 | POST | `/disputes/{id}/evidence` | Participant | `DisputeEvidences` |
| 5 | PUT | `/disputes/{id}/close` | Opener | `Disputes` |
| 6 | PUT | `/disputes/{id}/request-evidence` | Admin | `Disputes`, `Notifications` |
| 7 | DELETE | `/disputes/{did}/evidence/{eid}` | Opener/Submitter | `DisputeEvidences` |
| 8 | PUT | `/disputes/{id}/resolve` | Admin | `Disputes`, `Milestones` (unlock), `Projects` (unlock) — no `Payments`/`Wallets` change |
| 9 | POST | `/reviews` | Participant | `Reviews`, `ExpertProfiles` |
| 10 | GET | `/users/{id}/reviews` | — | `Reviews` |

---

# Expert Verification (Cross-flow, gates Expert credibility)

> Expert nộp bằng chứng kỹ năng/chứng chỉ, System (AI) tự chấm, Expert có thể escalate lên Admin nếu không đồng ý kết quả.

## V.1. Nộp bằng chứng xác minh

```
POST /api/v1/expert/verifications
```

**Auth:** `ExpertPolicy`. **Rate Limit:** `AI`. Multipart form: `{ expertSkillId: Guid, file: IFormFile }`.

**Status:** `201`. Response: `{ id, expertSkillId, skillName?, expertId, evidenceFileUrl, status, aiConfidenceScore?, aiReasoning?, adminId?, adminDecisionReason?, reviewedAt?, createdAt, canEscalate }`.

## V.2. Xem danh sách verification của Expert

```
GET /api/v1/expert/verifications?expertSkillId=&pageIndex=1&pageSize=20
```

**Auth:** `ExpertPolicy`.

## V.3. Escalate lên Admin

```
POST /api/v1/expert/verifications/{id}/escalate
```

**Auth:** `ExpertPolicy`. Chuyển `Status → ESCALATED` khi Expert không đồng ý kết quả AI chấm.

**Status enum (`ExpertVerificationStatus`):** `APPROVED`, `REJECTED`, `NEEDS_REVIEW`, `ESCALATED`.

---

# Admin (Cross-flow)

## Dashboard & User Management

| # | Method | Endpoint | Auth | Ghi chú |
|---|--------|----------|------|---------|
| 1 | GET | `/admin/stats` | Admin | `{ totalUsers, totalClients, totalExperts, totalJobs, activeProjects, openDisputes, totalEscrowAmount }` |
| 2 | GET | `/admin/expert-reviews` | Admin | Danh sách review có phân trang + search |
| 3 | GET | `/admin/users` | Admin | Danh sách user có phân trang + search |
| 4 | PUT | `/admin/users/{id}/suspend` | Admin | Body: `{ reason }` |
| 5 | PUT | `/admin/users/{id}/unsuspend` | Admin | — |

## Duyệt cập nhật hồ sơ Expert (profile updates)

```
GET  /api/v1/admin/expert-profile-updates?status=PENDING&pageIndex=1&pageSize=20
GET  /api/v1/admin/expert-profile-updates/{id}
PUT  /api/v1/admin/expert-profile-updates/{id}/review
```

**Auth:** `AdminPolicy`. `PUT .../review` body: `{ isApproved: bool, rejectionReason?: string }`.

Response (`ExpertProfileUpdateResponse`) cho thấy cả giá trị đề xuất và giá trị hiện tại để Admin so sánh: `title/bio/hourlyRate/experienceYears` (đề xuất) vs `currentTitle/currentBio/currentHourlyRate/currentExperienceYears` (hiện tại). Status enum (`ProfileUpdateStatus`): `PENDING`, `APPROVED`, `REJECTED`.

## Duyệt Expert Verification

```
GET /api/v1/admin/expert-verifications?status=&expertId=&pageIndex=1&pageSize=20
PUT /api/v1/admin/expert-verifications/{id}/review
```

**Auth:** `AdminPolicy`. `PUT .../review` body: `{ isApproved: bool, rejectionReason?: string }`.

> Đây là 2 resource **khác nhau** dễ nhầm: "expert-profile-updates" (đổi bio/title/rate) và "expert-verifications" (bằng chứng kỹ năng/chứng chỉ, đi kèm `POST /expert/verifications` ở trên).

---

# Supporting APIs (Cross-flow)

## Auth & Profile

| # | Method | Endpoint | Auth | Tables |
|---|--------|----------|------|--------|
| 1 | POST | `/auth/register` | — | `Users`, `Wallets`, `ClientProfiles`/`ExpertProfiles` |
| 2 | POST | `/auth/login` | — | `Users` |
| 3 | POST | `/auth/refresh-token` | — | — |
| 4 | POST | `/auth/logout` | Any | — |
| 5 | GET | `/auth/me` | Any | `Users` |
| 6 | PUT | `/users/me` | Any | `Users` |
| 7 | GET | `/profiles/client` | Client | `ClientProfiles` |
| 8 | PUT | `/profiles/client` | Client | `ClientProfiles` |
| 9 | GET | `/profiles/expert` | Expert | `ExpertProfiles` |
| 10 | PUT | `/profiles/expert` | Expert | `ExpertProfiles` |
| 11 | GET | `/profiles/expert/{expertId}` | — | `ExpertProfiles` |
| 12 | GET | `/profiles/experts/featured` | — | `ExpertProfiles` |
| 13 | GET | `/profiles/experts/search` | — | `ExpertProfiles` |
| 14 | GET | `/profiles/expert/{id}/completed-projects` | — | `Projects` |

## Skills & Categories

| # | Method | Endpoint | Auth | Tables |
|---|--------|----------|------|--------|
| 1 | GET | `/categories` | — | `Categories` |
| 2 | GET | `/categories/{id}` | — | `Categories` |
| 3 | GET | `/skills` | — | `Skills` |
| 4 | GET | `/skills/{id}` | — | `Skills` |
| 5 | POST | `/skills/expert/me` | Expert | `ExpertSkills` |
| 6 | DELETE | `/skills/expert/me/{skillId}` | Expert | `ExpertSkills` |

## Messaging & Realtime

| # | Method | Endpoint | Auth | Tables |
|---|--------|----------|------|--------|
| 1 | POST | `/conversations/init` | Any | `Conversations` — `expertId` là id đối phương (Client gọi → expert; Expert gọi → client). Expert cần proposal chưa bị reject/withdraw trên `jobId`, hoặc `projectId` chung với client — nếu không → 403 |
| 2 | GET | `/conversations/{id}/messages` | Participant | `Messages` |
| 3 | POST | `/conversations/{id}/read` | Participant | `Messages` |
| 4 | POST/GET | `/conversations/admin` | Admin | `Conversations` |

**SignalR Hub:** `/api/v1/chat` (implemented in `Aivora.Services/Hubs/ChatHub.cs`)
- **Client → Server:** `SendMessage({conversationId, content?, attachmentUrl?})`, `JoinConversation(conversationId)`, `LeaveConversation(conversationId)`, `UserTyping(conversationId, isTyping)`, `MarkAsRead(conversationId)`.
- **Server → Client:** `ReceiveMessage`, `ReadConfirmation`, `JobStatusUpdated` (emitted by `RealtimeService` on job publish/cancel/proposal-accept/project-complete), `NewJobPublished` (broadcast to all clients when a job is published). `Error` is reserved, not currently emitted.

## Media & Notifications

| # | Method | Endpoint | Auth | Tables |
|---|--------|----------|------|--------|
| 1 | POST | `/media/upload-image?folder=` | Any | Cloudinary |
| 2 | POST | `/media/upload-file?folder=` | Any | Cloudinary |
| 3 | GET | `/media` | Any | Cloudinary — list media của user hiện tại: `{url, publicId, format, bytes, createdAt}[]` |
| 4 | DELETE | `/media/{**publicId}` | Any | Cloudinary — user thường chỉ xóa được media của chính mình; Admin xóa được bất kỳ media nào. `publicId` là catch-all path param (có thể chứa `/`, không được encode) |
| 5 | GET | `/notifications` | Any | `Notifications` |
| 6 | GET | `/notifications/unread-count` | Any | `Notifications` |
| 7 | PUT | `/notifications/{id}/read` | Any | `Notifications` |
| 8 | PUT | `/notifications/read-all` | Any | `Notifications` |

## Health

```
GET /health
```

**Auth:** none. Ẩn khỏi OpenAPI docs (`IgnoreApi = true`). Response: `{ status: "healthy", timestamp, version }`. Dùng làm healthcheck target cho hosting (xem `render.yaml` → `healthCheckPath`).

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
CREATED → IN_PROGRESS → SUBMITTED → RELEASED
SUBMITTED → REVISION_REQUESTED → IN_PROGRESS → SUBMITTED
SUBMITTED → DISPUTED → SUBMITTED / IN_PROGRESS (unlock, follow-up action, not automatic)
```
> `FUNDED` và `APPROVED` tồn tại trong enum nhưng KHÔNG BAO GIỜ được gán trong pipeline hiện tại — Fund luôn set thẳng `IN_PROGRESS` (bỏ qua `FUNDED`), Approve luôn set thẳng `RELEASED` (bỏ qua `APPROVED`). `REFUNDED` chỉ đạt được qua `Treasury.RefundMilestoneAsync` — dead code, không controller nào gọi.

## Payment
```
Mỗi milestone có tối đa 2 dòng Payments, cả 2 tạo với Status = RELEASED ngay lập tức:
  - dòng 1: tạo lúc Fund (deposit = 30% Amount)
  - dòng 2: tạo lúc Approve (remaining = 70% Amount − commission)
```
> `PENDING`, `HELD`, `FROZEN`, `PARTIALLY_RELEASED` tồn tại trong enum nhưng KHÔNG code path nào gán — không có trạng thái "tiền đang giữ chờ" trong hệ thống thật. `REFUNDED` chỉ đạt được qua `Treasury.RefundMilestoneAsync` — dead code.

## Deliverable
```
(không có transition) — Deliverable tạo mới Status = SUBMITTED và KHÔNG BAO GIỜ đổi sau đó.
```
> `APPROVED`/`REVISION_REQUESTED`/`REJECTED` tồn tại trong enum nhưng không code path nào gán; `ReviewedAt` mãi `null`. Nộp lại deliverable (sau request-revision) tạo **row mới** với `RevisionNumber` tăng dần, không update row cũ.

## Dispute
```
OPEN → UNDER_REVIEW → RESOLVED
OPEN → UNDER_REVIEW → CLOSED
OPEN → RESOLVED
OPEN → CLOSED
```

## Expert Verification
```
NEEDS_REVIEW → APPROVED / REJECTED
NEEDS_REVIEW → ESCALATED → APPROVED / REJECTED (by Admin)
```

## Expert Profile Update
```
PENDING → APPROVED / REJECTED
```

## Service (Flow 2 Path B)
```
DRAFT → PUBLISHED → DRAFT (unpublish)
```

## ServiceRequest (Flow 2 Path B)
```
PENDING → ACCEPTED / DECLINED
```

## ServiceOffer (Flow 2 Path B)
```
PENDING → ACCEPTED
```

---

# Atomic Transactions

> Các endpoint sau **bắt buộc** chạy trong single DB transaction. Nếu bất kỳ bước nào fail → rollback toàn bộ.

| Endpoint | Flow | Reason |
|----------|------|--------|
| `PUT /proposals/{id}/accept` | Flow 2 (Path A) | Accept proposal + reject siblings + create project + create milestones |
| `POST /service-offers/{id}/accept` | Flow 2 (Path B) | Accept offer + create project + create milestones |
| `PUT /milestones/{id}/fund` | Flow 3 | `Treasury.PayDepositAsync` — claim milestone status (concurrency token) + move deposit wallet-to-wallet + create payment RELEASED + wallet transactions + auto-add step "Funded" + update milestone (→ IN_PROGRESS) + update project |
| `PUT /milestones/{id}/approve` | Flow 3 | `Treasury.PayRemainingAsync` — claim milestone status + move remaining wallet-to-wallet (trừ commission cho ví platform) + create payment RELEASED + wallet transactions + auto-add/complete step "Completed" + update milestone (→ RELEASED) + `SyncProjectStatusAsync` (+ update job nếu tất cả milestone settled) |
| `POST /milestones/{id}/dispute` | Flow 3 | Create dispute + update milestone (→ DISPUTED) + update project (→ DISPUTED) — **không đụng Payment/Wallet** |
| `PUT /disputes/{id}/resolve` | Flow 4 | Resolve dispute + unlock milestone + unlock project — single `SaveChangesAsync()`, no explicit `BeginTransaction`. Simpler than the others: never touches `Payments`/`Wallets`. |

---

# Authorization Matrix

| Policy | Roles |
|--------|-------|
| `ClientPolicy` | CLIENT |
| `ExpertPolicy` | EXPERT |
| `AdminPolicy` | ADMIN |
| `ClientOrExpertPolicy` | CLIENT or EXPERT |
| `WithdrawPolicy` | any authenticated user allowed to withdraw |
| `Any` | bất kỳ authenticated user |
| `Participant` | user tham gia entity (project, conversation, dispute) |
| `—` | public, không cần auth |

---

# Negative Test Cases

| Test Case | Expected Result |
|---|---|
| Release payment before deliverable approval | Should fail — no standalone endpoint exists |
| Review before project completed | Should not be allowed |
| Rating = 0 or 6 | Should fail |
| ReviewerId = RevieweeId | Should fail |
| Duplicate review for same project/reviewer/reviewee | Should fail |
| Client requests revision | No `Payments`/`Wallets` row is created or changed — deposit already released to Expert stays |
| Client opens dispute | No `Payments`/`Wallets` row changes; `Milestone → DISPUTED`, `Project → DISPUTED` |
| Non-admin resolves dispute | Should fail |
| Resolve dispute with `resolutionType`/`splitPercentage` fields | Ignored — field no longer exists, only `resolutionNote` is read |
| Non-owner Client accepts proposal | Should fail |
| Expert submits proposal to non-OPEN job | Should fail |
| Client funds milestone with insufficient balance | Should fail — `AvailableBalance < depositAmount` |
| Client approves milestone with insufficient balance for remaining | Should fail — `AvailableBalance < remainingAmount`, concurrency claim rolled back |
| Fund milestone when `Milestone.Status != CREATED` | Should fail — `409 Conflict` or `ValidationException` |
| Approve milestone when another milestone in the same project has an active dispute | Should fail |
| Expert submits deliverable to project they do not own | Should fail |
| Expert creates/edits/deletes a milestone step titled "Created"/"Funded"/"Completed" | Should fail — `IsSystemDefaultStep` guard blocks it |
| Non-owner deletes another user's media | Should fail with `401` and a clear message |

---

# Seed Accounts (Demo)

Full dataset: [`../SEED_DATA.md`](../SEED_DATA.md).

| Email | Role | Mục đích |
|-------|------|----------|
| `admin@aivora.com` | ADMIN | Demo Flow 4, Admin dashboard |
| `client1@example.com` | CLIENT | Demo Flow 1, 2, 3 (Tech Corp) |
| `client2@example.com` | CLIENT | Demo Flow 1, 2, 3 (StartupXYZ) |
| `expert1@example.com` | EXPERT | Demo Flow 2, 3 (Full-stack) |
| `expert3@example.com` | EXPERT | Demo Flow 2, 3, 4 (Mobile — project completed + reviewed) |

---

# References

- [`MAINFLOW_v2.md`](./MAINFLOW_v2.md) — business flow source of truth (4 main flows).
- [`../ARCHITECTURE.md`](../ARCHITECTURE.md) — response wrapper, auth format, pagination, known debt.
- [`../../AGENTS.md`](../../AGENTS.md) — tech stack, quick start, env vars.
- [`../../CLAUDE.md`](../../CLAUDE.md) — architecture patterns, gotchas.
