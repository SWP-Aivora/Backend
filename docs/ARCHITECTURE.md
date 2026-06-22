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
| HTTP endpoints | 18 Controllers in `/Controllers/` |
| Real-time | SignalR Hub in `/Hubs/ChatHub.cs` |
| Exception handling | `/Middlewares/ExceptionMiddleware.cs` |
| Config extensions | `/Extensions/` — `ClaimsExtensions`, `ControllerExtensions`, `JwtExtensions`, `OpenApiExtensions` |
| DI + Pipeline | `Program.cs` — single file, single responsibility |

### Aivora.Services (Business Logic)
**SDK:** `Microsoft.NET.Sdk` (class library)

| Namespace | File(s) | Responsibility |
|-----------|---------|----------------|
| `AIJobAssistantService` | `AIJobSuggestionPromptBuilder`, `AIJobRefinementPromptBuilder`, `AIServiceDescriptionPromptBuilder` | Build prompts for AI calls |
| `AIJobAssistantService.Parsing` | `AIJobSuggestionParser`, `AIJobRefinementParser`, `AIServiceDescriptionParser` | Parse AI responses into structured DTOs |
| `AIJobAssistantService.Providers` | `IAIJobSuggestionProvider`, `GeminiJobSuggestionProvider`, `MockJobSuggestionProvider`, `IAIJobRefinementProvider`, `AIServiceDescriptionProvider` | Strategy pattern for AI providers |
| `AdminService` | `IAdminService`, `Service` | Admin dashboard operations |
| `CategoryService` | `ICategoryService`, `Service` | Job categories CRUD |
| `DeliverableService` | `IDeliverableService`, `Service` | Milestone deliverables |
| `DisputeService` | `IDisputeService`, `Service` | Dispute creation and resolution |
| `HiringService` | `IHiringService`, `Service` | Job hiring workflow |
| `IdentityService` | `IIdentityService`, `Service` | User identity management |
| `JobService` | `IJobService`, `Service` | Job CRUD and search |
| `JwtService` | `IJwtService`, `Service` | JWT token generation |
| `MediaService` | `IMediaService`, `Service` | Cloudinary file uploads |
| `MessageService` | `IMessageService`, `Service` | Messaging between users |
| `MilestoneService` | `IMilestoneService`, `Service` | Milestone lifecycle |
| `NotificationService` | `INotificationService`, `Service` | User notifications |
| `ProfileService` | `IProfileService`, `Service` | Expert/client profiles |
| `ProjectService` | `IProjectService`, `Service` | Project state management |
| `ProposalService` | `IProposalService`, `Service` | Proposal submission and acceptance |
| `RecommendationService` | `IRecommendationService`, `Service` | Expert recommendations |
| `ReviewService` | `IReviewService`, `Service` | Project reviews |
| `SkillService` | `ISkillService`, `Service` | Skill catalog management |
| `Treasury` | (files) | Platform treasury operations |
| `WalletService` | `IWalletService`, `Service` | User wallet and transactions |
| `Base/` | `BaseModels` | Common base request/response models |

### Aivora.Repositories (Data Access)
**SDK:** `Microsoft.NET.Sdk` (class library)

| Folder | Contents |
|--------|----------|
| `Abstractions/` | `BaseEntity`, `IAuditableEntity` — Entity base class and auditing interface |
| `Data/` | `AivoraDbContext`, `AuditableEntityInterceptor` (auto-set timestamps) |
| `Entities/` | All EF Core entities (`Job`, `Proposal`, `Project`, `Milestone`, `Payment`, `Deliverable`, `Review`, `Skill`, `Category`, `User`, `Conversation`, `Message`, `Notification`, `Dispute`, `Wallet`, `Transaction`) |
| `Enums/` | `JobStatus`, `ProposalStatus`, `ProjectStatus`, `MilestoneStatus`, `PaymentStatus`, `DeliverableStatus`, `DisputeStatus`, `UserRole`, etc. |
| `Configurations/` | Fluent API entity configurations |
| `Migrations/` | EF Core migrations |
| `Exceptions/` | Data access exceptions |

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
3. Token sent in `Authorization: Bearer <token>` header
4. `[Authorize]` attributes validate token on protected endpoints
5. Role-based policies restrict access:
   - `ClientPolicy` → `UserRole.Client`
   - `ExpertPolicy` → `UserRole.Expert`
   - `AdminPolicy` → `UserRole.Admin`

### Default seed accounts
| Email | Role |
|-------|------|
| `client@test.com` | CLIENT |
| `expert@test.com` | EXPERT |
| `admin@test.com` | ADMIN |

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
  └── Notifications (1:N) — User notifications

Job
  ├── Proposals (1:N)
  ├── Project (1:1 or 1:0) — Created when proposal accepted
  ├── Category (N:1)
  └── Skills (N:M) — via join table

Project
  ├── Milestones (1:N)
  └── Deliverables (1:N, via milestones)

Milestone
  ├── Deliverables (1:N)
  └── Payment (1:1)

Payment — Records financial transactions
Dispute — Linked to project or milestone
```

### Entity base class pattern
All entities inherit from `Entity`:
```csharp
public abstract class Entity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
```

`AuditableEntityInterceptor` auto-sets `CreatedAt` on insert and `UpdatedAt` on save.

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

**Source of truth:** `Program.cs` config validation + `appsettings.json` / `appsettings.Development.json`

**Variable naming:** Use `__` as section separator (e.g., `ConnectionStrings__DefaultConnection`). Compatible with all hosting providers.

**Placeholder detection:** Values containing `__SET`, `CHANGE_ME`, or `PLACEHOLDER` cause startup failure — ensures no accidental deployment with default config.

### Full configuration table

| Section | Key | Required | Notes |
|---------|-----|----------|-------|
| `ConnectionStrings` | `DefaultConnection` | ✅ | PostgreSQL connection string |
| `JwtSettings` | `Secret` | ✅ | Min 32 characters |
| `JwtSettings` | `Issuer` | ✅ | Typically "Aivora" |
| `JwtSettings` | `Audience` | ✅ | Typically "Aivora" |
| `JwtSettings` | `ExpiryInMinutes` | ✅ | Integer |
| `CloudinaryOptions` | `CloudName` | ✅ | Cloudinary cloud name |
| `CloudinaryOptions` | `ApiKey` | ✅ | Cloudinary API key |
| `CloudinaryOptions` | `ApiSecret` | ✅ | Cloudinary API secret |
| `AIProvider` | `Provider` | ❌ | `Mock` or `Gemini` |
| `AIProvider` | `ApiKey` | ❌ | Gemini API key (empty = Mock) |
| `AIProvider` | `BaseUrl` | ❌ | Default: generativelanguage.googleapis.com |
| `AIProvider` | `Model` | ❌ | Default: gemini-2.5-flash |
| `AIProvider` | `EnableFallback` | ❌ | Default: true |
| `RateLimit` | `Strict/Wnd/Limit` | ❌ | Auth endpoint limits |
| `RateLimit` | `AI/Wnd/Limit` | ❌ | AI endpoint limits |
| `RateLimit` | `General/Wnd/Limit` | ❌ | General endpoint limits |

---

## SignalR Chat Hub

**Hub path:** `/api/v1/chat`

**Methods:**
| Method | Parameters | Description |
|--------|-----------|-------------|
| `SendMessage` | `conversationId: Guid, content: string` | Send a message to a conversation |

**Events:**
| Event | Payload | Description |
|-------|---------|-------------|
| `ReceiveMessage` | `ConversationMessage` | New message received |
| `ReadConfirmation` | `{ userId, conversationId, readAt }` | Message read receipt |
| `Error` | `{ message: string }` | Error notification |

---

## Scalar OpenAPI

API documentation is served via **Scalar** (not Swagger UI). Access the interactive docs at the root URL when running locally.

Document filters in `OpenApiExtensions`:
- Hides SignalR hub methods from REST API docs
- Hides the `HEAD` endpoint
- Adds JWT Bearer auth scheme to the document

---

## Known Architectural Debt

See [IMPROVEMENTS.md](architecture/IMPROVEMENTS.md) for planned improvements:

1. **Recommendation Scoring** — Strategy pattern extraction needed
2. **Domain Lifecycle Transitions** — Anemic model → Rich domain for Job/Proposal/Project state machines
