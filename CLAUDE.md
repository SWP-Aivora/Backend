# CLAUDE.md — Aivora Backend

> Project-level context for Claude Code. Loaded automatically in every session.

---

## 🌐 Ngôn ngữ

- **Luôn trả lời bằng Tiếng Việt** cho mọi câu hỏi, giải thích, phân tích.
- Chỉ dùng tiếng Anh cho code, tên kỹ thuật, tên file, và khái niệm không có bản dịch phổ biến.

---

## 📋 Tổng quan dự án

**Aivora** là nền tảng marketplace kết nối Client (người thuê) với Expert (chuyên gia AI/tech) để thực hiện dự án theo milestone, có escrow payment, dispute resolution, và review.

**Base URL:** `/api/v1`
**Auth:** JWT Bearer token
**Response wrapper:** `{ success: bool, message: string, data: T?, errors?: object }`

---

## 🏗️Tech Stack

| Layer              | Technology                                            |
| ------------------ | ----------------------------------------------------- |
| **Framework**      | .NET 10 (ASP.NET Core)                                |
| **Language**       | C# 13 with NRT (nullable reference types)             |
| **Database**       | PostgreSQL via EF Core 10                             |
| **Auth**           | JWT Bearer (System.IdentityModel.Tokens.Jwt 8.x)      |
| **AI**             | Google Gemini 2.5 Flash (with Mock provider fallback) |
| **File Storage**   | Cloudinary                                            |
| **Real-time**      | SignalR (ChatHub)                                     |
| **API Docs**       | Scalar (OpenAPI)                                      |
| **Test Framework** | xUnit + FluentAssertions + Moq + EF Core InMemory     |

---

## 📐 Solution Structure

```
Aivora.sln
├── Aivora.api/              ← Entry point (Controllers, Middleware, Extensions, Hubs)
├── Aivora.Services/         ← Business logic layer (scoped services)
├── Aivora.Repositories/     ← Data access layer (EF Core, entities, configs)
├── Aivora.Tests/            ← Unit & integration tests
└── docs/                    ← Architecture, flows, environment reference
```

**Dependency flow:** `Aivora.api` → `Aivora.Services` → `Aivora.Repositories`

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
| --------- | -------------- | ------- | ------ |
| `Strict`  | Auth endpoints | 10 req  | 1 min  |
| `AI`      | AI endpoints   | 20 req  | 1 min  |
| `General` | All others     | 100 req | 1 min  |

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

---

## 🚀 Quick Start

### Prerequisites

- .NET 10 SDK
- PostgreSQL (local or cloud)
- Cloudinary account
- Gemini API key (optional, falls back to Mock)

### Run locally

```bash
# Restore NuGet packages
dotnet restore

# Build the solution
dotnet build

# Set up environment variables
cp .env.example .env
# Edit .env with your values

# Initialize user secrets (optional, for local development)
cd Aivora.api
dotnet user-secrets init
# Set required secrets (see docs/ENV.md)

# Run the application
cd Aivora.api
dotnet run
```

### Docker Development

```bash
# Start with docker-compose
docker-compose up -d

# View logs
docker-compose logs -f

# Stop services
docker-compose down
```

### Environment Setup

1. Copy the example environment file:

   ```bash
   cp .env.example .env
   ```

2. Edit `.env` with your actual values:
   - PostgreSQL connection
   - JWT settings (generate a secure secret)
   - Cloudinary credentials
   - Gemini API key (optional)

3. The app will crash at startup if any required variables are missing or contain placeholders.

### Package Management

- This solution uses standard NuGet package management
- Packages are defined in each project's `.csproj` file
- No additional package managers are required

### Run tests

```bash
dotnet test
```

### Apply migrations

```bash
cd Aivora.Repositories
dotnet ef migrations add MigrationName --startup-project ../Aivora.api
dotnet ef database update --startup-project ../Aivora.api
```

---

## 📁 Important Files

| File                                                                  | Purpose                                                       |
| --------------------------------------------------------------------- | ------------------------------------------------------------- |
| `Aivora.api/Program.cs`                                               | Service registration, middleware pipeline, database migration |
| `docs/ENV.md`                                                         | All environment variables                                     |
| `docs/architecture/IMPROVEMENTS.md`                                   | Known architectural debt & planned improvements               |
| `docs/flows/MAINFLOW.md`                                              | 4 main business flows (source of truth)                       |
| `docs/flows/API_BY_FLOW.md`                                           | Complete API endpoint reference                               |
| `Aivora.Repositories/Data/Configurations/`                            | EF Core entity configurations                                 |
| `Aivora.Repositories/Data/Interceptors/AuditableEntityInterceptor.cs` | Auto-set CreatedAt/UpdatedAt                                  |

---

## ⚠️ Gotchas

1. **Enum serialization:** All API-facing enums use `[JsonConverter(typeof(JsonStringEnumConverter))]` — string values accepted (e.g., `"EXPERT"`, `"HOURLY"`).
2. **Partial update:** `PATCH /ai/job-assistant/{id}` only updates fields that are explicitly sent. Null fields are ignored.
3. **Accept proposal is atomic:** Accepting a proposal creates project + milestones + rejects siblings in a single DB transaction. Cannot be rolled back manually.
4. **Payment release is automatic:** Payment is released internally when deliverable is approved — no separate "release payment" endpoint.
5. **Review constraint:** Both users can only review after project is `COMPLETED`. Rating must be 1-5. Self-review is forbidden. One review per user-pair per project.
6. **Placeholder detection:** App crashes at startup if config values contain `__SET`, `CHANGE_ME`, or `PLACEHOLDER`.
7. **Mock AI fallback:** When `AIProvider__ApiKey` is not set, all AI endpoints return Mock responses. This is intentional for development/testing.
8. **SignalR hub:** Chat uses `/api/v1/chat` hub (not REST). Methods: `SendMessage(conversationId, content)`. Events: `ReceiveMessage`, `ReadConfirmation`, `Error`.
9. **Database seeding with duplicate keys:** Robust duplicate key handling implemented - all `SaveChangesAsync()` calls are wrapped in `SaveChangesWithDuplicateHandling()` which catches Postgres error 23505 and continues gracefully with warnings. Seeding will never crash due to duplicate constraints.

---

## 📚 References

- [Environment Variables](docs/ENV.md)
- [Architecture Improvements](docs/architecture/IMPROVEMENTS.md)
- [4 Main Business Flows](docs/flows/MAINFLOW.md)
- [Complete API Reference](docs/flows/API_BY_FLOW.md)
