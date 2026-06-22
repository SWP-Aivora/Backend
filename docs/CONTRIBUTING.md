# Contributing to Aivora Backend

> Development setup, scripts, testing, code style, and PR checklist.

---

## Prerequisites

| Tool | Version | Purpose |
|------|---------|---------|
| .NET SDK | 10.x | Build, run, test |
| PostgreSQL | 14+ | Primary database |
| Git | 2.x+ | Version control |
| dotnet-ef tool | 10.0.8 | EF Core migrations |

```bash
dotnet tool install --global dotnet-ef --version 10.0.8
```

---

## Development Setup

### 1. Clone the repository
```bash
git clone <repo-url>
cd Aivora-Backend
```

### 2. Initialize user secrets
```bash
cd Aivora.api
dotnet user-secrets init
```

### 3. Set required environment variables
See [Environment Variables](ENV.md) for the full reference. Minimum secrets for local development:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=aivora;Username=postgres;Password=yourpassword"
dotnet user-secrets set "JwtSettings:Secret" "your-super-secret-key-at-least-32-characters-long!"
dotnet user-secrets set "JwtSettings:Issuer" "Aivora"
dotnet user-secrets set "JwtSettings:Audience" "Aivora"
dotnet user-secrets set "JwtSettings:ExpiryInMinutes" "60"
dotnet user-secrets set "CloudinaryOptions:CloudName" "your-cloud-name"
dotnet user-secrets set "CloudinaryOptions:ApiKey" "your-api-key"
dotnet user-secrets set "CloudinaryOptions:ApiSecret" "your-api-secret"
```

> **Note:** AI features use Mock provider by default. Set `AIProvider:ApiKey` with a Gemini API key only if you need real AI responses.

### 4. Run the application
```bash
dotnet run
```

The API will be available at `https://localhost:7286` (check `launchSettings.json` for exact ports). Scalar OpenAPI UI is served at the root path.

### 5. Database migrations
```bash
# Create a new migration
dotnet ef migrations add MigrationName --project Aivora.Repositories --startup-project Aivora.api

# Apply migrations
dotnet ef database update --project Aivora.Repositories --startup-project Aivora.api
```


---

## Available Scripts

| Command | Description |
|---------|-------------|
| `dotnet run --project Aivora.api` | Start the API server |
| `dotnet build` | Build the entire solution |
| `dotnet test` | Run all tests |
| `dotnet test --filter "FullyQualifiedName~ClassName"` | Run specific test class |
| `dotnet test --logger "console;verbosity=detailed"` | Run tests with verbose output |
| `dotnet watch run --project Aivora.api` | Hot-reload development mode |
| `dotnet ef migrations add <name>` | Create new EF Core migration |
| `dotnet ef database update` | Apply pending migrations |
| `dotnet ef database update 0` | Roll back all migrations |
| `dotnet ef migrations remove` | Remove the last migration |

---

## Testing

### Framework
- **xUnit** — test runner
- **FluentAssertions** — readable assertions (`result.Should().Be(expected)`)
- **Moq** — mocking dependencies
- **EF Core InMemory** — in-memory database for repository tests

### Running tests
```bash
# All tests
dotnet test

# With coverage (if configured)
dotnet test --collect:"XPlat Code Coverage"

# Specific test project
dotnet test Aivora.Tests/Aivora.Tests.csproj
```

### Writing tests
Follow the **AAA pattern** (Arrange → Act → Assert):

```csharp
[Fact]
public async Task GetJobById_WithExistingJob_ReturnsJobDetail()
{
    // Arrange
    var jobId = Guid.NewGuid();
    var job = new Job { Id = jobId, Title = "Test Job" };
    _context.Jobs.Add(job);
    await _context.SaveChangesAsync();

    // Act
    var result = await _service.GetJobById(jobId);

    // Assert
    result.Should().NotBeNull();
    result!.Title.Should().Be("Test Job");
}
```

### Test naming convention
```
MethodName_WithCondition_ExpectedResult
```

Examples:
- `AcceptProposal_WithValidProposal_CreatesProjectAndMilestones`
- `Register_WithDuplicateEmail_ReturnsBadRequest`
- `ApproveDeliverable_WithNonExistingId_ReturnsNotFound`

### Test coverage target: **80%**

Focus areas:
- Service layer business logic
- Controller authorization checks
- AI provider strategy selection
- Edge cases in the 4 main flows (Job → Proposal → Milestone → Review)

---

## Code Style

### General rules
- **Nullable Reference Types (NRT)** are enabled. Use `?` for nullable reference types, don't use `!` null-forgiving operator unless absolutely necessary.
- **File-scoped namespaces** preferred: `namespace Aivora.Services.JobService;`
- **`var`** allowed when type is obvious from the right-hand side.
- **Expression-bodied members** allowed for simple one-liners.

### Naming conventions
| Element | Convention | Example |
|---------|-----------|---------|
| Interfaces | `IPascalCase` | `IJobService` |
| Classes | `PascalCase` | `JobService` |
| Methods | `PascalCase` | `GetJobByIdAsync` |
| Parameters | `camelCase` | `jobId` |
| Private fields | `_camelCase` | `_repository` |
| Constants | `PascalCase` | `MaxRetryCount` |
| Enums | `PascalCase` | `JobStatus.InProgress` |

### Service pattern
Every business service follows the interface + implementation pattern:
- `Aivora.Services/{ServiceName}/IService.cs` — interface
- `Aivora.Services/{ServiceName}/Service.cs` — implementation
- Registration: `builder.Services.AddScoped<IService, Service>();`

### Controller rules
- Controllers are thin — delegate to services.
- Use `[Authorize(Policy = "...")]` for role-based access.
- Return `IActionResult` with proper HTTP status codes.
- Use `[FromBody]`, `[FromRoute]`, `[FromQuery]` explicitly.

### EF Core conventions
- Entity configurations in `Aivora.Repositories/Data/Configurations/`
- Interceptors in `Aivora.Repositories/Data/Interceptors/`
- Use Fluent API, not data annotations.
- All entities inherit from `Entity` base class with `Guid Id`, `DateTime CreatedAt`, `DateTime? UpdatedAt`.

### Error handling
- Use custom exception types in `Aivora.Services.Exceptions/` and `Aivora.Repositories.Exceptions/`.
- Global exception handling via `ExceptionMiddleware` — don't wrap everything in try/catch.
- Never swallow exceptions silently.

---

## Project Architecture

```
Aivora.api/              ← Entry point
├── Controllers/          ← HTTP endpoints (thin layer)
├── Extensions/           ← Extension methods for Program.cs
├── Hubs/                 ← SignalR hubs (ChatHub)
├── Middlewares/          ← Custom middleware (ExceptionMiddleware)
└── Program.cs            ← DI + middleware pipeline

Aivora.Services/         ← Business logic
├── {Service}/            ← One folder per domain service
│   ├── IService.cs       ← Interface
│   └── Service.cs        ← Implementation
├── AIJobAssistantService/ ← AI-specific (Prompting, Parsing, Providers)
└── Exceptions/           ← Custom exceptions

Aivora.Repositories/     ← Data access
├── Abstractions/         ← Repository interfaces
├── Configurations/       ← EF Core entity configurations
├── Data/                 ← DbContext, interceptors
├── Entities/             ← Entity models
├── Enums/                ← Domain enums
└── Migrations/           ← EF Core migrations

Aivora.Tests/            ← Tests
└── Services/             ← Service-layer tests
```

---

## PR Checklist

Before submitting a pull request, verify:

### Code quality
- [ ] Solution builds without errors: `dotnet build`
- [ ] All existing tests pass: `dotnet test`
- [ ] New functionality has tests with 80%+ coverage
- [ ] No `TODO` comments left in new code
- [ ] No hardcoded secrets, passwords, or API keys
- [ ] Nullable reference types handled correctly (no unnecessary `!`)

### Architecture
- [ ] Business logic is in Services, not Controllers
- [ ] New entities have EF Core configurations in `Configurations/`
- [ ] New services follow `IService` + `Service` + `AddScoped` pattern
- [ ] Authorization policies applied correctly

### Database
- [ ] New entities have corresponding migration
- [ ] Migration builds successfully: `dotnet ef migrations add`
- [ ] No data loss in migration (review `Up()` method)
- [ ] Seed data updated if needed

### API design
- [ ] Endpoints follow REST conventions
- [ ] HTTP status codes are appropriate (200, 201, 400, 401, 403, 404, 429, 500)
- [ ] Request/response DTOs documented in XML comments
- [ ] Validation rules applied via Data Annotations or FluentValidation

### Git
- [ ] Branch is up to date with `main`
- [ ] No merge conflicts
- [ ] Commit messages follow conventional format: `feat:`, `fix:`, `refactor:`, `docs:`, `test:`, `chore:`
