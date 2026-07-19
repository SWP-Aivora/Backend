# CLAUDE.md — Aivora Backend

> Project-level context for Claude Code. Loaded automatically in every session.
> Tech stack, quick start, và env vars đã có ở [`AGENTS.md`](./AGENTS.md) — không lặp lại ở đây. File này chỉ chứa kiến trúc chi tiết và gotchas đặc thù mà Claude Code cần biết.

---

## 🌐 Ngôn ngữ

- **Luôn trả lời bằng Tiếng Việt** cho mọi câu hỏi, giải thích, phân tích.
- Chỉ dùng tiếng Anh cho code, tên kỹ thuật, tên file, và khái niệm không có bản dịch phổ biến.

---

## 📋 Tổng quan dự án

**Aivora** là nền tảng marketplace kết nối Client (người thuê) với Expert (chuyên gia AI/tech) để thực hiện dự án theo milestone, có escrow payment, dispute resolution, và review.

**Base URL:** `/api/v1`
**Auth:** JWT Bearer token
**Response wrapper:** `{ success: bool, message: string, data: T?, errors?: object, traceId: string }`

---

## 🔑 Key Architecture Patterns

### Middleware Pipeline Order

```
ExceptionMiddleware → Scalar/OpenAPI UI → HTTPS Redirect → CORS → RateLimiter → Auth → Authorization → Controllers / SignalR Hub
```

**Note:** The `ExceptionMiddleware` is the first middleware and handles all unhandled exceptions globally.

### Authorization Policies

| Policy         | Roles  |
| -------------- | ------ |
| `ClientPolicy` | CLIENT |
| `ExpertPolicy` | EXPERT |
| `AdminPolicy`  | ADMIN  |

### Rate Limiting (Fixed Window)

| Policy    | Target         | Limit   | Window |
| --------- | --------------- | ------- | ------ |
| `Strict`  | Auth endpoints  | 10 req  | 1 min  |
| `AI`      | AI endpoints    | 20 req  | 1 min  |
| `General` | All others      | 100 req | 1 min  |

### Service Registration Convention

All services use **interface-based DI** with `IService` interface and `Service` implementation:

```csharp
builder.Services.AddScoped<IService, Service>();
```

Each service namespace = `Aivora.Services.{ServiceName}` with `IService.cs` + `Service.cs`.

### AI Job Assistant Pattern

Uses Strategy pattern for AI providers:

- `IAIJobSuggestionProvider` / `IAIJobRefinementProvider` / `IAIServiceDescriptionProvider`
- Resolution: If `AIProvider:Provider=Gemini` + `ApiKey` set → use Gemini; otherwise → Mock
- Prompt building via `AIJobSuggestionPromptBuilder` / `AIJobRefinementPromptBuilder` / `AIServiceDescriptionPromptBuilder`
- Parsing via `AIJobSuggestionParser` / `AIJobRefinementParser` / `AIServiceDescriptionParser`
- **Lưu ý:** `AIJobAssistantService` (refine `AIJobSuggestion`) và `AIJobRefinementService` (refine `Job` đã tạo) là 2 pipeline riêng biệt, gần giống nhau — xem `docs/ARCHITECTURE.md` mục Known Debt.

---

## 📁 Important Files

| File                                                                  | Purpose                                                       |
| ---------------------------------------------------------------------- | --------------------------------------------------------------- |
| `Aivora.api/Program.cs`                                               | Service registration, middleware pipeline, database migration |
| `docs/ENV.md`                                                         | All environment variables                                     |
| `docs/ARCHITECTURE.md`                                                | Layer overview, service inventory, known architectural debt   |
| `docs/flows/MAINFLOW_v2.md`                                           | 4 main business flows (source of truth)                       |
| `docs/flows/API_BY_FLOW.md`                                           | Complete API endpoint reference                                |
| `Aivora.Repositories/Data/Configurations/`                            | EF Core entity configurations                                 |
| `Aivora.Repositories/Data/Interceptors/AuditableEntityInterceptor.cs` | Auto-set CreatedAt/UpdatedAt                                   |

---

## ⚠️ Gotchas

1. **Enum serialization:** All API-facing enums use `[JsonConverter(typeof(JsonStringEnumConverter))]` — string values accepted (e.g., `"EXPERT"`, `"HOURLY"`).
2. **Partial update:** `PATCH /ai/job-assistant/{id}` only updates fields that are explicitly sent. Null fields are ignored.
3. **Accept proposal is atomic:** Accepting a proposal creates project + milestones + rejects siblings in a single DB transaction. Cannot be rolled back manually.
4. **Payment release is automatic:** Payment is released internally when deliverable is approved — no separate "release payment" endpoint.
5. **Review constraint:** Both users can only review after project is `COMPLETED`. Rating must be 1-5. Self-review is forbidden. One review per user-pair per project.
6. **Mock AI fallback:** When `AIProvider__ApiKey` is not set, all AI endpoints return Mock responses. This is intentional for development/testing.
7. **SignalR hub:** Chat uses `/api/v1/chat` hub (not REST), implemented in `Aivora.Services/Hubs/ChatHub.cs`. Methods: `SendMessage`, `JoinConversation`, `LeaveConversation`, `UserTyping`, `MarkAsRead`. Events: `ReceiveMessage`, `ReadConfirmation`, `JobStatusUpdated`, `NewJobPublished`. `Error` is reserved but not currently emitted.
8. **Database seeding with duplicate keys:** Robust duplicate key handling implemented - all `SaveChangesAsync()` calls are wrapped in `SaveChangesWithDuplicateHandling()` which catches Postgres error 23505 and continues gracefully with warnings. Seeding will never crash due to duplicate constraints.
9. **Dispute resolution does not auto-move money:** `PUT /disputes/{id}/resolve` sets `Disputes.Status = RESOLVED`, records `ResolutionNote`, and **unlocks** the milestone (→ `SUBMITTED` if a deliverable was already submitted, else `IN_PROGRESS`) and project (→ `ACTIVE` if no other milestone in the project is still `DISPUTED`) — but it never touches `Payments` or `Wallets` (that flow was removed in issue #94). Money still only moves through the normal milestone approve/refund path.

---

## Agent skills

### Issue tracker

Issues live in GitHub Issues (`SWP-Aivora/Backend`), via the `gh` CLI. External PRs are not a triage surface. See `docs/agents/issue-tracker.md`.

### Triage labels

Label strings match the `/triage` role names as-is (state roles `needs-triage`/`needs-info`/`ready-for-agent`/`ready-for-human`/`wontfix`, category roles `bug`/`enhancement`). See `docs/agents/triage-labels.md`.

### Domain docs

Single-context — `CONTEXT.md` + `docs/adr/` at the repo root. See `docs/agents/domain.md`.

---

## 📚 References

- [Tech stack, Quick Start, Env Vars](AGENTS.md)
- [Environment Variables (full)](docs/ENV.md)
- [Architecture & Known Debt](docs/ARCHITECTURE.md)
- [4 Main Business Flows](docs/flows/MAINFLOW_v2.md)
- [Complete API Reference](docs/flows/API_BY_FLOW.md)
