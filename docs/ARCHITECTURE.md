# Architecture — Aivora Backend

> Layered architecture overview, project references, service inventory, and middleware pipeline.

---

## Project Dependency Graph

```
Aivora.Tests ──────────► Aivora.api
                            │
Aivora.Repositories ◄───────┤
                            │
Aivora.Services ◄───────────┘
```

**Unidirectional flow:** `Aivora.api` → `Aivora.Services` → `Aivora.Repositories`

No circular references. `Aivora.Repositories` and `Aivora.Services` have **no upward references**.

---

## Layer Descriptions

### Aivora.api (Entry Point)
**SDK:** `Microsoft.NET.Sdk.Web`

| Concern | Implementation |
|---------|---------------|
| HTTP endpoints | 20 Controllers in `/Controllers/` |
| Exception handling | `/Middlewares/ExceptionMiddleware.cs` |
| Config extensions | `/Extensions/` — `ClaimsExtensions`, `ControllerExtensions`, `JwtExtensions`, `OpenApiExtensions`, `SeedingServiceExtensions` |
| DI + Pipeline | `Program.cs` — single file, single responsibility |

**Controllers:** Auth, User, Profile, Category, Skill, Job, Proposal, Project, Milestone, Dispute, Wallet, Payment, Review, Message, Notification, Media, AI, Admin, ExpertVerification, Health.

**SignalR hub:** `ChatHub` lives in `Aivora.Services/Hubs/ChatHub.cs` (namespace `Aivora.api.Hubs`), mapped at `/api/v1/chat` in `Program.cs`.

### Aivora.Services (Business Logic)
**SDK:** `Microsoft.NET.Sdk` (class library)

| Namespace | Responsibility |
|-----------|----------------|
| `AIJobAssistantService` (+ `.Parsing`, `.Providers`) | AI job-suggestion prompt building, response parsing, provider strategy (Gemini/Mock) |
| `AIJobRefinementService` | Refines an already-created `Job` (separate from `AIJobAssistantService`'s suggestion refine — see Known Debt) |
| `AIMilestoneStepAssistantService` | AI-assisted milestone step suggestions |
| `AdminService` | Admin dashboard operations |
| `CategoryService` | Job categories CRUD |
| `DeliverableService` | Milestone deliverables |
| `DisputeService` | Dispute creation, evidence, resolution |
| `ExpertVerificationService` | Expert skill/certificate verification submissions + AI auto-grading + escalation |
| `HiringService` | Job hiring workflow |
| `IdentityService` | User identity management |
| `JobService` | Job CRUD and search |
| `JwtService` | JWT token generation |
| `MediaService` | Cloudinary file uploads |
| `MessageService` | Messaging between users |
| `MilestoneService` | Milestone lifecycle + milestone steps |
| `NotificationService` | User notifications |
| `ProfileService` | Expert/client profiles |
| `ProjectService` | Project state management |
| `ProposalService` | Proposal submission and acceptance |
| `RealtimeService` | Emits `JobStatusUpdated` over `ChatHub` on job create/cancel/proposal-accept/project-complete |
| `RecommendationService` | Expert recommendations |
| `ReviewService` | Project reviews |
| `SkillService` | Skill catalog management |
| `Treasury` | Platform treasury / commission operations |
| `WalletService` | User wallet, deposits (VNPay), withdrawals, transfers, transactions |
| `Base/` | `ServiceBase` common utilities |

> **Deprecated:** `FinancialLedger` no longer exists — money movement is now handled by `Treasury` (see `CONTEXT.md`).

### Aivora.Repositories (Data Access)
**SDK:** `Microsoft.NET.Sdk` (class library)

| Folder | Contents |
|--------|----------|
| `Abstractions/` | `BaseEntity<TKey>` / `BaseEntity` / `AuditableBaseEntity` — no generic repository interface exists |
| `Data/AivoraDbContext.cs` | EF Core `DbContext` |
| `Data/Interceptors/` | `AuditableEntityInterceptor` (auto-set timestamps) |
| `Data/Configurations/` | Fluent API entity configurations |
| `Data/Migrations/` | EF Core migrations |
| `Data/Seeders/` | `AivoraDataSeeder` |
| `Entities/` | All EF Core entities |
| `Enums/` | `JobStatus`, `ProposalStatus`, `ProjectStatus`, `MilestoneStatus`, `PaymentStatus`, `DeliverableStatus`, `DisputeStatus`, `UserRole`, etc. |
| `Constants/` | Repository-layer constants |

> Data-access exceptions (`NotFoundException`, `ValidationException`, etc.) actually live in `Aivora.Services/Exceptions/` — there is no separate `Aivora.Repositories/Exceptions/`.

### Aivora.Tests (Tests)
**Framework:** xUnit + FluentAssertions + Moq + EF Core InMemory

| Folder | Contents |
|--------|----------|
| `Services/` | Service-layer unit tests with mocked repositories |

---

## Middleware Pipeline

```
┌─────────────────────────────────────────────────────┐
│  1. ExceptionMiddleware                             │
│     Global try/catch → standardized error response  │
├─────────────────────────────────────────────────────┤
│  2. Scalar / OpenAPI UI                             │
│     API documentation interface                     │
├─────────────────────────────────────────────────────┤
│  3. HTTPS Redirection                               │
│     HTTP → HTTPS redirect                           │
├─────────────────────────────────────────────────────┤
│  4. CORS                                            │
│     Cross-origin request handling                   │
├─────────────────────────────────────────────────────┤
│  5. Rate Limiting                                   │
│     Strict (auth) → AI (Gemini) → General           │
│     Fixed Window algorithm, IP-based partitions     │
├─────────────────────────────────────────────────────┤
│  6. Authentication                                  │
│     JWT Bearer token validation                     │
├─────────────────────────────────────────────────────┤
│  7. Authorization                                   │
│     Policy-based: Client, Expert, Admin             │
├─────────────────────────────────────────────────────┤
│  8. Endpoints                                       │
│     Controllers + SignalR Hub (/api/v1/chat)        │
└─────────────────────────────────────────────────────┘
```

---

## Authentication & Authorization

### JWT Flow
1. User calls `/api/v1/auth/login` or `/api/v1/auth/register`
2. `JwtService` generates a Bearer token with claims: `sub` (userId), `email`, `role`, `nameid`
3. Token sent in `Authorization: Bearer <token>` header (also set as HttpOnly cookies `accessToken`/`refreshToken`)
4. `[Authorize]` attributes validate token on protected endpoints
5. Role-based policies restrict access:
   - `ClientPolicy` → `UserRole.Client`
   - `ExpertPolicy` → `UserRole.Expert`
   - `AdminPolicy` → `UserRole.Admin`

### Default seed accounts
See [`SEED_DATA.md`](SEED_DATA.md) for the full list. Quick reference:

| Email | Role |
|-------|------|
| `admin@aivora.com` | ADMIN |
| `client1@example.com` | CLIENT |
| `expert1@example.com` | EXPERT |

---

## AI Provider Strategy

```
                    ┌──────────────┐
                    │ Program.cs   │
                    │ Config Check │
                    └──────┬───────┘
                           │
              ┌────────────┴────────────┐
              │                         │
    Provider = "Gemini"       Provider = "Mock"
    AND ApiKey set?           (default)
              │                         │
              ▼                         ▼
    ┌─────────────────┐       ┌─────────────────┐
    │   Gemini        │       │   Mock          │
    │   Provider      │       │   Provider      │
    │   (live API)    │       │   (dev/test)    │
    └─────────────────┘       └─────────────────┘
              │
              ▼ Fallback on failure (if enabled)
    ┌─────────────────┐
    │   Mock          │
    │   Provider      │
    └─────────────────┘
```

**Provider registration:**
```csharp
if (provider == "Gemini" && !string.IsNullOrEmpty(apiKey))
    services.AddScoped<IAIJobSuggestionProvider, GeminiJobSuggestionProvider>();
else
    services.AddScoped<IAIJobSuggestionProvider, MockJobSuggestionProvider>();
```

Same pattern applies to `IAIJobRefinementProvider`, `IAIServiceDescriptionProvider`, and the milestone-step assistant provider.

---

## Database (PostgreSQL via EF Core)

**Provider:** `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.2
**NetTopologySuite:** Enabled for potential geospatial queries (job location filtering)

### Key entities and relationships
```
User (Client/Expert)
  ├── Jobs (1:N) — Client creates jobs
  ├── Proposals (1:N) — Expert submits proposals
  ├── Reviews (1:N) — Both can review
  ├── Wallet (1:1) — Each user has a wallet
  ├── Conversations (N:M) — via participant table
  ├── Messages (1:N) — Messages in conversations
  ├── Notifications (1:N) — User notifications
  └── ExpertVerifications (1:N) — Expert only

Job
  ├── Proposals (1:N)
  ├── Project (1:1 or 1:0) — Created when proposal accepted
  ├── Category (N:1)
  └── Skills (N:M) — via join table

Project
  ├── Milestones (1:N)
  └── Deliverables (1:N, via milestones)

Milestone
  ├── MilestoneSteps (1:N)
  ├── Deliverables (1:N)
  └── Payment (1:1)

Payment — Records escrow/financial transactions
Dispute — Linked to a milestone; has DisputeEvidences (1:N)
```

### Entity base class pattern
Defined in `Aivora.Repositories/Abstractions/BaseEntity.cs`:
```csharp
public abstract class BaseEntity<TKey>
{
    public TKey Id { get; set; } = default!;
    public bool IsDeleted { get; set; } // Soft delete
}

public abstract class BaseEntity : BaseEntity<Guid> { /* Id = Guid.NewGuid() */ }

public abstract class AuditableBaseEntity : BaseEntity, IAuditableEntity
{
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}
```

Most entities inherit `AuditableBaseEntity` (timestamps + soft delete); a few inherit plain `BaseEntity` only. `AuditableEntityInterceptor` auto-sets `CreatedAt` on insert and `UpdatedAt` on save. Timestamps are `DateTimeOffset`, not `DateTime`.

### Table name reference (selected)

Most tables share their entity's plural name (`Jobs`→`JobPosts`, `Proposals`, `Projects`, `Milestones`, `Payments`, `Wallets`, `Reviews`, `Users`, etc — see `Aivora.Repositories/Data/Configurations/*.ToTable()` for the authoritative list). One frequent gotcha:

- Entity `DisputeEvidence` → table **`DisputeEvidences`** (plural, unlike the entity name).

---

## Rate Limiting

| Policy | Target | Permit Limit | Window | Partition Key |
|--------|--------|-------------|--------|---------------|
| `Strict` | Auth endpoints | 10 | 1 min | `HttpContext.Connection.RemoteIpAddress` |
| `AI` | AI endpoints | 20 | 1 min | `HttpContext.Connection.RemoteIpAddress` |
| `General` | All others | 100 | 1 min | `HttpContext.Connection.RemoteIpAddress` |

Rejection response: `429 Too Many Requests` with body:
```json
{ "message": "Too many requests. Please try again after X second(s)." }
```

---

## API Response Format

All endpoints return a consistent envelope:

### Success (200/201)
```json
{
  "success": true,
  "message": "✅ Job created successfully!",
  "data": { /* T */ },
  "traceId": "00-abc-def-00"
}
```

### Error (400/401/403/404/429/500)
```json
{
  "success": false,
  "message": "A job with this title already exists.",
  "traceId": "00-abc-def-00"
}
```

`traceId` is always present — maps to the HTTP request trace ID for debugging.

---

## Configuration Architecture

**Source of truth:** `Program.cs` config validation + `.env.example` + `appsettings.json` / `appsettings.Development.json`. Full reference: [`ENV.md`](ENV.md).

**Variable naming:** Use `__` as section separator (e.g., `ConnectionStrings__DefaultConnection`). Compatible with all hosting providers.

**Placeholder detection:** Values containing `__SET`, `CHANGE_ME`, or `PLACEHOLDER` cause startup failure — ensures no accidental deployment with default config.

### Full configuration table

| Section | Key | Required | Notes |
|---------|-----|----------|-------|
| `ConnectionStrings` | `DefaultConnection` | ✅ | PostgreSQL connection string |
| `JwtSettings` | `Secret` | ✅ | Min 32 characters |
| `JwtSettings` | `Issuer` | ✅ | `AivoraApi` |
| `JwtSettings` | `Audience` | ✅ | `AivoraClient` |
| `JwtSettings` | `ExpiryInMinutes` | ✅ | Integer |
| `CloudinaryOptions` | `CloudName` | ✅ | Cloudinary cloud name |
| `CloudinaryOptions` | `ApiKey` | ✅ | Cloudinary API key |
| `CloudinaryOptions` | `ApiSecret` | ✅ | Cloudinary API secret |
| `AIProvider` | `Provider` | ❌ | `Mock` or `Gemini` |
| `AIProvider` | `ApiKey` | ❌ | Gemini API key (empty = Mock) |
| `AIProvider` | `BaseUrl` | ❌ | Default: generativelanguage.googleapis.com |
| `AIProvider` | `Model` | ❌ | Default: gemini-2.5-flash |
| `AIProvider` | `EnableFallback` | ❌ | Default: true |
| `FrontendUrl` | — | ❌ | Used to build VNPay return redirect |
| `VNPay` | `TmnCode` / `HashSecret` / `BaseUrl` / `ReturnUrl` / `IpnUrl` | ❌ | VNPay payment gateway integration |
| `Commission` | `Rate` / `MaxDebtLimit` | ❌ | Platform commission on released payments |
| `RateLimit` | `Strict/AI/General × PermitLimit/WindowInMinutes` | ❌ | Rate limit policies |

---

## SignalR Chat Hub

**Location:** `Aivora.Services/Hubs/ChatHub.cs` (namespace `Aivora.api.Hubs`)
**Hub path:** `/api/v1/chat`
**Auth:** JWT via query string `access_token` for WebSocket connections.

**Methods:**
| Method | Parameters | Description |
|--------|-----------|-------------|
| `SendMessage` | `request: { conversationId: Guid, content?: string, attachmentUrl?: string }` | Send a message to a conversation |
| `JoinConversation` | `conversationId: Guid` | Join a conversation group |
| `LeaveConversation` | `conversationId: Guid` | Leave a conversation group |
| `UserTyping` | `conversationId: Guid, isTyping: bool` | Broadcast typing indicator |
| `MarkAsRead` | `conversationId: Guid` | Mark messages read, broadcast `ReadConfirmation` |

**Events:**
| Event | Payload | Description |
|-------|---------|-------------|
| `ReceiveMessage` | `ConversationMessage` | New message received |
| `ReadConfirmation` | `{ userId, conversationId, readAt }` | Message read receipt |
| `UserTyping` | `{ conversationId, userId, isTyping, timestamp }` | Typing indicator broadcast to other participants |
| `JobStatusUpdated` | `{ jobId, status, title? }` | Emitted by `RealtimeService` to `Clients.User(userId)` on job publish (open), cancel (cancelled), proposal accepted (in_progress), project completed (completed) |
| `NewJobPublished` | `{ jobId, title }` | Emitted by `RealtimeService` to `Clients.All` when a job is published |
| `Error` | `{ message: string }` | **Reserved, not currently emitted.** Errors surface via SignalR's default `HubException` instead — kept here for future use, don't rely on it client-side yet. |

---

## Scalar OpenAPI

API documentation is served via **Scalar** (not Swagger UI). Access the interactive docs at the root URL when running locally.

Document filters in `OpenApiExtensions`:
- Hides SignalR hub methods from REST API docs
- Hides the `HEAD` endpoint
- Adds JWT Bearer auth scheme to the document

---

## Known Architectural Debt

1. **Duplicate AI "refine" flows.** `POST /ai/job-assistant/{id}/refine` (edits an `AIJobSuggestion`, via `AIJobAssistantService.IAIJobRefinementProvider`) and `POST /ai/jobs/{jobId}/refine` (edits an already-created `Job`, via `AIJobRefinementService.IAIJobRefinementProvider`) are two near-identical interface/implementation pairs registered separately in DI. Both are used by the frontend and work correctly — not a bug, but worth consolidating.
2. **`Error` SignalR event is documented but never emitted** by `ChatHub` — errors currently surface through SignalR's default `HubException`. Either implement the emit or drop the event from client-facing docs once confirmed unused.
3. **Recommendation Scoring** — the weighted scoring formula (`0.35*SkillScore + 0.20*PortfolioScore + ...`) lives inline in `RecommendationService`; a Strategy-pattern extraction would make individual score components independently testable.
4. **Domain Lifecycle Transitions** — Job/Proposal/Project state machines are anemic (status is a plain enum field validated ad-hoc per service method) rather than enforced by a rich domain model.
