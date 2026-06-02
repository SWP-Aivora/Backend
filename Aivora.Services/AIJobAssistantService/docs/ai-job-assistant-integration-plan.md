# AI Job Assistant Full Backend Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate the TypeScript reference backend AI behavior into the existing ASP.NET Core backend without converting the demo application wholesale.

**Architecture:** Keep the ASP.NET Core API, EF Core entities, service layer, and current authorization model as the production boundary. Port the TypeScript reference behavior into focused C# services: provider interfaces, prompt builders, response parsers, deterministic mock providers, Gemini providers, persistence mapping, AI assistant endpoints, service-generator endpoint, and recommendation scoring. The reference React, Express, SQLite, Vite, demo seed data, and frontend scaffolding stay unchanged.

**Tech Stack:** ASP.NET Core controllers, EF Core with Npgsql, C# service classes, xUnit, FluentAssertions, Moq, deterministic mock AI providers, Gemini over `HttpClient`.

---

## Decision

Do not convert all TypeScript files to C#.

Use `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\reference\chat-demo` only as behavior reference. The production work belongs in the existing C# backend:

- AI job suggestion generation.
- AI suggestion retrieval, patching, refinement, acceptance, and rejection.
- Gemini and deterministic mock providers for suggestion generation, refinement, and expert service description generation.
- Structured AI suggestion persistence.
- Accepting a suggestion into a draft job.
- Expert service-generator endpoint.
- Expert recommendation scoring upgrade.

Do not port or copy:

- React components.
- Express routing scaffolding.
- SQLite or PostgreSQL helper code from the demo.
- Vite project files.
- Demo seed data.
- Demo frontend state management.
- Reference documentation scaffolding.

## Current State Evidence

- Current AI service is mock-only: `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\Service.cs`.
- Current C# AI API only has generate, accept, and reject: `D:\projects\swp-2026\Backend\Aivora.api\Controllers\AIController.cs`.
- The TypeScript reference exposes required backend behavior: generate, get, refine, patch, accept, reject, recommendation generation, recommendation retrieval, and service generation in `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\reference\chat-demo\server\src\interface\routes.ts`.
- `AIJobSuggestion` does not yet persist budget type, currency, experience level, business domain, expected outcome, clarifying answers, or rejection reason.
- `JobPost` already has `BusinessDomain`, `ExpectedOutcome`, `BudgetType`, `Currency`, `TimelineDays`, and `ExperienceLevel`.
- `JobService` request and response DTOs do not expose every `JobPost` field needed by accepted AI suggestions.
- Current accept logic uses `Guid.Empty` when no category id is supplied; that fallback must be removed.
- Current recommendation service keeps the correct endpoints but uses a simple score and does not persist every existing score component.
- Local `dotnet --info` currently reports `.NET SDKs installed: No SDKs were found.` Build, test, and EF migration commands require installing or selecting a compatible SDK.
- `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\docs` and `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\reference` are currently untracked in git; preserve the reference tree and keep production edits scoped.

## Core Backend Migration Definition Of Done

- `POST /api/v1/ai/job-assistant` returns `201 Created`, persists a generated suggestion, and returns structured fields.
- `GET /api/v1/ai/job-assistant/{id}` returns the client-owned suggestion.
- `PATCH /api/v1/ai/job-assistant/{id}` updates only allowed fields on generated suggestions.
- `POST /api/v1/ai/job-assistant/{id}/refine` returns the updated suggestion, `aiResponse`, and `changedFields`.
- `POST /api/v1/ai/job-assistant/{id}/accept` requires a real category id, creates a `DRAFT` job, maps structured AI fields into the job, and marks the suggestion `ACCEPTED`.
- `POST /api/v1/ai/job-assistant/{id}/reject` validates and stores the rejection reason, blocks accepted suggestions, and marks generated suggestions `REJECTED`.
- `POST /api/v1/ai/service-generator` is expert-only, validates the request, supports Gemini/mock providers, and returns title, description, three package tiers, and FAQs.
- AI provider boundaries exist for suggestion generation, suggestion refinement, and service description generation.
- Prompt builders and response parsers are separate from provider transport code.
- Deterministic mock providers work without AI credentials.
- Gemini providers are configurable and covered by fake HTTP tests.
- Recommendation generation uses the TypeScript scoring model as the baseline and persists all supported component scores.
- EF migration adds structured AI suggestion fields with safe defaults for required new columns.
- `ClarifyingAnswersJson` uses the same JSON string storage approach as current AI suggestion JSON fields unless the project first changes the full AI suggestion JSON storage strategy.
- Reference TypeScript files remain unchanged.
- Focused AI, service-generator, and recommendation tests pass after a .NET SDK is available.
- Full solution tests pass after a .NET SDK is available.

## File Structure

### Existing Files To Modify

- `D:\projects\swp-2026\Backend\Aivora.Repositories\Entities\AIJobSuggestion.cs`
  Add structured suggestion fields and rejection reason.
- `D:\projects\swp-2026\Backend\Aivora.Repositories\Data\Configurations\AIJobSuggestionConfiguration.cs`
  Configure enum conversions, max lengths, JSON string columns, and default values.
- `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\Request.cs`
  Add request DTOs for patch, refine, and service generation.
- `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\Response.cs`
  Add structured suggestion fields, refine response, service-generator response, package DTOs, and FAQ DTOs.
- `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\IService.cs`
  Add get, patch, refine, and service-generator methods.
- `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\Service.cs`
  Replace inline mock logic with provider-backed generation, persistence, patching, refinement, accept, reject, and service generation.
- `D:\projects\swp-2026\Backend\Aivora.Services\JobService\Request.cs`
  Add `BusinessDomain`, `ExpectedOutcome`, and `Currency` to create/update requests.
- `D:\projects\swp-2026\Backend\Aivora.Services\JobService\Response.cs`
  Return `BusinessDomain` and `ExpectedOutcome`.
- `D:\projects\swp-2026\Backend\Aivora.Services\JobService\Service.cs`
  Map added job fields and preserve `AICOIN` when currency is omitted or blank.
- `D:\projects\swp-2026\Backend\Aivora.Services\RecommendationService\Service.cs`
  Upgrade scoring and persist all score components.
- `D:\projects\swp-2026\Backend\Aivora.Services\RecommendationService\Response.cs`
  Ensure returned responses include every persisted score component.
- `D:\projects\swp-2026\Backend\Aivora.api\Controllers\AIController.cs`
  Add get, patch, refine, and service-generator endpoints; update status codes.
- `D:\projects\swp-2026\Backend\Aivora.api\Program.cs`
  Register options, providers, prompt builders, parsers, and `HttpClient` integrations.
- `D:\projects\swp-2026\Backend\Aivora.api\appsettings.json`
  Add non-secret AI provider defaults.
- `D:\projects\swp-2026\Backend\Aivora.Tests\Services\AIJobAssistantServiceTests.cs`
  Expand AI service tests.

### New Files To Create

- `D:\projects\swp-2026\Backend\Aivora.Services\Options\AIProviderOptions.cs`
- `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\AIJobSuggestionDraft.cs`
- `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\AIJobRefinementDraft.cs`
- `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\AIServiceDescriptionDraft.cs`
- `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\IAIJobSuggestionProvider.cs`
- `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\IAIJobRefinementProvider.cs`
- `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\IAIServiceDescriptionProvider.cs`
- `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\Prompting\AIJobSuggestionPromptBuilder.cs`
- `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\Prompting\AIJobRefinementPromptBuilder.cs`
- `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\Prompting\AIServiceDescriptionPromptBuilder.cs`
- `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\Parsing\AIJobSuggestionParser.cs`
- `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\Parsing\AIJobRefinementParser.cs`
- `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\Parsing\AIServiceDescriptionParser.cs`
- `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\Providers\MockAIJobSuggestionProvider.cs`
- `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\Providers\MockAIJobRefinementProvider.cs`
- `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\Providers\MockAIServiceDescriptionProvider.cs`
- `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\Providers\GeminiAIJobSuggestionProvider.cs`
- `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\Providers\GeminiAIJobRefinementProvider.cs`
- `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\Providers\GeminiAIServiceDescriptionProvider.cs`
- `D:\projects\swp-2026\Backend\Aivora.Tests\Services\AIJobAssistantProviderTests.cs`
- `D:\projects\swp-2026\Backend\Aivora.Tests\Services\AIServiceGeneratorTests.cs`
- `D:\projects\swp-2026\Backend\Aivora.Tests\Services\RecommendationServiceTests.cs`
- EF migration under `D:\projects\swp-2026\Backend\Aivora.Repositories\Data\Migrations`

## PR Splitting Strategy

- PR 1: Persistence, DTOs, provider contracts, prompt builders, parsers, deterministic mock generation.
- PR 2: Generate, get, patch, refine, accept, and reject service/controller behavior.
- PR 3: Gemini providers for generation, refinement, and service description generation.
- PR 4: Expert service-generator endpoint and tests.
- PR 5: Recommendation scoring upgrade and tests.

Each PR must build and pass its focused tests independently after the .NET SDK gate is cleared.

## Full Backend Migration Execution Board

### Task 0: Prepare Safe Execution Workspace

**Files:**

- Read: `D:\projects\swp-2026\Backend\Aivora.sln`
- Read: `D:\projects\swp-2026\Backend\Aivora.Tests\Aivora.Tests.csproj`
- Read: `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\reference\chat-demo\server\src\interface\routes.ts`
- Read: `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\reference\chat-demo\server\src\interface\validation.ts`

- [ ] **Step 1: Inspect worktree**

```powershell
git status --short
```

Expected: record existing untracked or modified files before editing. Do not modify `Aivora.Services\AIJobAssistantService\reference`.

- [ ] **Step 2: Confirm current branch**

```powershell
git branch --show-current
```

Expected: branch name is recorded.

- [ ] **Step 3: Create a feature branch when currently on a protected branch**

```powershell
git checkout -b codex/ai-job-assistant-full-backend-migration
```

Expected: implementation happens on `codex/ai-job-assistant-full-backend-migration`.

- [ ] **Step 4: Check SDK gate**

```powershell
dotnet --info
```

Expected before implementation verification: at least one compatible .NET SDK is listed. Current observed blocker is `No SDKs were found.`

- [ ] **Step 5: Run baseline tests after SDK is available**

```powershell
dotnet test D:\projects\swp-2026\Backend\Aivora.sln --no-restore
```

Expected: baseline result is recorded. If baseline fails, list failing tests and continue once the team decides whether failures are unrelated to this migration.

### Task 1: Add Persistence Fields And EF Configuration

**Files:**

- Modify: `D:\projects\swp-2026\Backend\Aivora.Repositories\Entities\AIJobSuggestion.cs`
- Modify: `D:\projects\swp-2026\Backend\Aivora.Repositories\Data\Configurations\AIJobSuggestionConfiguration.cs`
- Create migration under: `D:\projects\swp-2026\Backend\Aivora.Repositories\Data\Migrations`

- [ ] **Step 1: Add entity fields**

Add these properties to `AIJobSuggestion`:

```csharp
public BudgetType SuggestedBudgetType { get; set; } = BudgetType.FIXED;
public string Currency { get; set; } = "AICOIN";
public SkillLevel? SuggestedExperienceLevel { get; set; }
public string? SuggestedBusinessDomain { get; set; }
public string? SuggestedExpectedOutcome { get; set; }
public string? ClarifyingAnswersJson { get; set; }
public string? RejectionReason { get; set; }
```

Expected: required fields have safe in-memory defaults for new entity instances.

- [ ] **Step 2: Configure persistence**

Add these configuration rules:

```csharp
builder.Property(x => x.SuggestedBudgetType)
    .HasConversion<string>()
    .IsRequired()
    .HasDefaultValue(BudgetType.FIXED);

builder.Property(x => x.Currency)
    .HasMaxLength(10)
    .IsRequired()
    .HasDefaultValue("AICOIN");

builder.Property(x => x.SuggestedExperienceLevel)
    .HasConversion<string>();

builder.Property(x => x.SuggestedBusinessDomain).HasMaxLength(255);
builder.Property(x => x.SuggestedExpectedOutcome).HasMaxLength(1000);
builder.Property(x => x.RejectionReason).HasMaxLength(500);
builder.Property(x => x.ClarifyingAnswersJson);
```

Expected: `ClarifyingAnswersJson` is configured consistently with existing JSON string columns such as `SuggestedSkillsJson`.

- [ ] **Step 3: Generate migration after SDK is available**

```powershell
dotnet ef migrations add AddAIJobAssistantStructuredFields --project D:\projects\swp-2026\Backend\Aivora.Repositories --startup-project D:\projects\swp-2026\Backend\Aivora.api
```

Expected migration behavior:

```csharp
migrationBuilder.AddColumn<string>(
    name: "SuggestedBudgetType",
    table: "AIJobSuggestions",
    type: "text",
    nullable: false,
    defaultValue: "FIXED");

migrationBuilder.AddColumn<string>(
    name: "Currency",
    table: "AIJobSuggestions",
    type: "character varying(10)",
    maxLength: 10,
    nullable: false,
    defaultValue: "AICOIN");
```

Expected: existing rows receive safe defaults and the migration does not require destructive data changes.

- [ ] **Step 4: Verify idempotent script**

```powershell
dotnet ef migrations script --idempotent --project D:\projects\swp-2026\Backend\Aivora.Repositories --startup-project D:\projects\swp-2026\Backend\Aivora.api
```

Expected: script includes default values for `SuggestedBudgetType` and `Currency`.

- [ ] **Step 5: Commit PR 1 slice**

```powershell
git add Aivora.Repositories\Entities\AIJobSuggestion.cs Aivora.Repositories\Data\Configurations\AIJobSuggestionConfiguration.cs Aivora.Repositories\Data\Migrations
git commit -m "feat(ai): add structured suggestion persistence"
```

### Task 2: Expand DTOs And Service Contracts

**Files:**

- Modify: `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\Request.cs`
- Modify: `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\Response.cs`
- Modify: `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\IService.cs`

- [ ] **Step 1: Expand generation request**

Add:

```csharp
public BudgetType? BudgetType { get; set; }
public string? Currency { get; set; }
public SkillLevel? ExperienceLevel { get; set; }
```

Expected: existing request fields remain compatible: `RawInput`, `BusinessDomain`, `ExpectedOutcome`, `BudgetMin`, `BudgetMax`, `TimelineDays`.

- [ ] **Step 2: Add patch and refine requests**

```csharp
public class PatchSuggestionRequest
{
    public string? SuggestedTitle { get; set; }
    public string? SuggestedDescription { get; set; }
    public string? BusinessDomain { get; set; }
    public string? ExpectedOutcome { get; set; }
    public BudgetType? BudgetType { get; set; }
    public string? Currency { get; set; }
    public decimal? SuggestedBudgetMin { get; set; }
    public decimal? SuggestedBudgetMax { get; set; }
    public int? SuggestedTimelineDays { get; set; }
    public SkillLevel? ExperienceLevel { get; set; }
    public List<string>? SuggestedSkills { get; set; }
    public List<Response.SuggestedMilestone>? SuggestedMilestones { get; set; }
    public List<string>? ClarifyingAnswers { get; set; }
}

public class RefineSuggestionRequest
{
    public string Message { get; set; } = null!;
}
```

Expected: patch updates form-editable fields only. Refine accepts the chat message from the TypeScript reference behavior.

- [ ] **Step 3: Add service-generator request**

```csharp
public class GenerateServiceDescriptionRequest
{
    public string RawInput { get; set; } = null!;
    public List<string> Skills { get; set; } = new();
    public decimal PriceFrom { get; set; }
    public int DeliveryDays { get; set; }
    public string Tone { get; set; } = "professional";
    public string TargetClient { get; set; } = "startup";
    public string Language { get; set; } = "vi";
}
```

Expected validation rules are implemented in Task 10.

- [ ] **Step 4: Expand suggestion response**

Add:

```csharp
public string? BusinessDomain { get; set; }
public string? ExpectedOutcome { get; set; }
public BudgetType BudgetType { get; set; }
public string Currency { get; set; } = "AICOIN";
public SkillLevel? ExperienceLevel { get; set; }
public List<string> ClarifyingAnswers { get; set; } = new();
public string? RejectionReason { get; set; }
```

Expected: API responses expose the structured data that TypeScript reference stores and displays.

- [ ] **Step 5: Add refine and service-generator responses**

```csharp
public class RefineSuggestionResponse
{
    public SuggestionResponse Suggestion { get; set; } = null!;
    public string AIResponse { get; set; } = null!;
    public List<string> ChangedFields { get; set; } = new();
}

public class ServiceDescriptionResponse
{
    public string SuggestedTitle { get; set; } = null!;
    public string SuggestedDescription { get; set; } = null!;
    public List<ServicePackageResponse> Packages { get; set; } = new();
    public List<ServiceFaqResponse> Faqs { get; set; } = new();
}

public class ServicePackageResponse
{
    public string Name { get; set; } = null!;
    public decimal Price { get; set; }
    public int DeliveryDays { get; set; }
    public string Description { get; set; } = null!;
    public List<string> Features { get; set; } = new();
}

public class ServiceFaqResponse
{
    public string Question { get; set; } = null!;
    public string Answer { get; set; } = null!;
}
```

Expected: service-generator output supports the TypeScript Basic, Standard, Premium packages and FAQs.

- [ ] **Step 6: Expand service interface**

```csharp
Task<Response.SuggestionResponse> GenerateSuggestionAsync(Guid clientId, Request.GenerateSuggestionRequest request, CancellationToken cancellationToken = default);
Task<Response.SuggestionResponse> GetSuggestionAsync(Guid clientId, Guid suggestionId, CancellationToken cancellationToken = default);
Task<Response.SuggestionResponse> PatchSuggestionAsync(Guid clientId, Guid suggestionId, Request.PatchSuggestionRequest request, CancellationToken cancellationToken = default);
Task<Response.RefineSuggestionResponse> RefineSuggestionAsync(Guid clientId, Guid suggestionId, Request.RefineSuggestionRequest request, CancellationToken cancellationToken = default);
Task<Response.AcceptResultResponse> AcceptSuggestionAsync(Guid clientId, Guid suggestionId, Request.AcceptSuggestionRequest request, CancellationToken cancellationToken = default);
Task<Response.SuggestionResponse> RejectSuggestionAsync(Guid clientId, Guid suggestionId, Request.RejectSuggestionRequest request, CancellationToken cancellationToken = default);
Task<Response.ServiceDescriptionResponse> GenerateServiceDescriptionAsync(Guid expertId, Request.GenerateServiceDescriptionRequest request, CancellationToken cancellationToken = default);
```

Expected: service layer supports every required backend AI route.

- [ ] **Step 7: Commit PR 1 slice**

```powershell
git add Aivora.Services\AIJobAssistantService\Request.cs Aivora.Services\AIJobAssistantService\Response.cs Aivora.Services\AIJobAssistantService\IService.cs
git commit -m "feat(ai): expand assistant contracts"
```

### Task 3: Add Provider Models, Prompt Builders, And Parsers

**Files:**

- Create: `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\AIJobSuggestionDraft.cs`
- Create: `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\AIJobRefinementDraft.cs`
- Create: `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\AIServiceDescriptionDraft.cs`
- Create: provider interfaces under `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService`
- Create: prompt builders under `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\Prompting`
- Create: parsers under `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\Parsing`
- Create tests: `D:\projects\swp-2026\Backend\Aivora.Tests\Services\AIJobAssistantProviderTests.cs`

- [ ] **Step 1: Add draft models**

Create provider-facing draft models independent from EF entities:

```csharp
public class AIJobSuggestionDraft
{
    public string SuggestedTitle { get; set; } = null!;
    public string SuggestedDescription { get; set; } = null!;
    public string? BusinessDomain { get; set; }
    public string? ExpectedOutcome { get; set; }
    public BudgetType BudgetType { get; set; } = BudgetType.FIXED;
    public string Currency { get; set; } = "AICOIN";
    public decimal? SuggestedBudgetMin { get; set; }
    public decimal? SuggestedBudgetMax { get; set; }
    public int? SuggestedTimelineDays { get; set; }
    public SkillLevel? ExperienceLevel { get; set; }
    public List<string> SuggestedSkills { get; set; } = new();
    public List<Response.SuggestedMilestone> SuggestedMilestones { get; set; } = new();
    public List<string> ClarifyingQuestions { get; set; } = new();
    public List<string> ClarifyingAnswers { get; set; } = new();
    public List<string> RiskWarnings { get; set; } = new();
    public string AIModel { get; set; } = "Aivora-Mock";
}
```

Expected: provider output can be mapped into persistence without EF coupling.

- [ ] **Step 2: Add refinement model**

```csharp
public class AIJobRefinementDraft
{
    public AIJobSuggestionDraft Suggestion { get; set; } = null!;
    public string AIResponse { get; set; } = null!;
    public List<string> ChangedFields { get; set; } = new();
}
```

Expected: refine can return both the updated suggestion and chat response metadata.

- [ ] **Step 3: Add service-description model**

```csharp
public class AIServiceDescriptionDraft
{
    public string SuggestedTitle { get; set; } = null!;
    public string SuggestedDescription { get; set; } = null!;
    public List<Response.ServicePackageResponse> Packages { get; set; } = new();
    public List<Response.ServiceFaqResponse> Faqs { get; set; } = new();
    public string AIModel { get; set; } = "Aivora-Mock";
}
```

Expected: service-generator provider output stays separate from API response mapping.

- [ ] **Step 4: Add provider interfaces**

```csharp
public interface IAIJobSuggestionProvider
{
    Task<AIJobSuggestionDraft> GenerateSuggestionAsync(Request.GenerateSuggestionRequest request, CancellationToken cancellationToken = default);
}

public interface IAIJobRefinementProvider
{
    Task<AIJobRefinementDraft> RefineSuggestionAsync(Response.SuggestionResponse current, string message, CancellationToken cancellationToken = default);
}

public interface IAIServiceDescriptionProvider
{
    Task<AIServiceDescriptionDraft> GenerateServiceDescriptionAsync(Request.GenerateServiceDescriptionRequest request, CancellationToken cancellationToken = default);
}
```

Expected: generation, refinement, and service description generation can evolve independently.

- [ ] **Step 5: Add prompt builders**

Builder methods:

```csharp
public string Build(Request.GenerateSuggestionRequest request)
public string Build(Response.SuggestionResponse current, string message)
public string Build(Request.GenerateServiceDescriptionRequest request)
```

Expected: prompt text is not embedded directly in Gemini provider transport classes.

- [ ] **Step 6: Add parser methods**

Parser methods:

```csharp
public AIJobSuggestionDraft Parse(string providerText, Request.GenerateSuggestionRequest request)
public AIJobRefinementDraft Parse(string providerText, Response.SuggestionResponse current)
public AIServiceDescriptionDraft Parse(string providerText, Request.GenerateServiceDescriptionRequest request)
```

Expected parser behavior:

- Strip markdown code fences before JSON parsing.
- Accept a top-level JSON object.
- Default budget type to `FIXED`.
- Default currency to `AICOIN`.
- Default missing arrays to empty lists.
- Normalize service packages to exactly Basic, Standard, Premium.

- [ ] **Step 7: Add parser tests**

Create tests:

```csharp
[Fact]
public void SuggestionParser_DefaultsMissingCurrencyToAicoin()

[Fact]
public void SuggestionParser_StripsMarkdownJsonFence()

[Fact]
public void ServiceDescriptionParser_ReturnsThreePackages()

[Fact]
public void RefinementParser_ReturnsChangedFields()
```

Expected: parser behavior is proven without network credentials.

- [ ] **Step 8: Commit PR 1 slice**

```powershell
git add Aivora.Services\AIJobAssistantService\AIJobSuggestionDraft.cs Aivora.Services\AIJobAssistantService\AIJobRefinementDraft.cs Aivora.Services\AIJobAssistantService\AIServiceDescriptionDraft.cs Aivora.Services\AIJobAssistantService\IAIJobSuggestionProvider.cs Aivora.Services\AIJobAssistantService\IAIJobRefinementProvider.cs Aivora.Services\AIJobAssistantService\IAIServiceDescriptionProvider.cs Aivora.Services\AIJobAssistantService\Prompting Aivora.Services\AIJobAssistantService\Parsing Aivora.Tests\Services\AIJobAssistantProviderTests.cs
git commit -m "feat(ai): add provider prompt and parser boundaries"
```

### Task 4: Add Deterministic Mock Providers

**Files:**

- Create: `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\Providers\MockAIJobSuggestionProvider.cs`
- Create: `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\Providers\MockAIJobRefinementProvider.cs`
- Create: `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\Providers\MockAIServiceDescriptionProvider.cs`
- Modify tests: `D:\projects\swp-2026\Backend\Aivora.Tests\Services\AIJobAssistantProviderTests.cs`

- [ ] **Step 1: Move generation mock behavior into provider**

Mock generation rules:

- Title starts with `AI Enhanced:`.
- Description uses `request.RawInput`.
- Budget min uses `request.BudgetMin` or `500`.
- Budget max uses `request.BudgetMax` or `1500`.
- Timeline uses `request.TimelineDays` or `14`.
- Budget type uses `request.BudgetType` or `FIXED`.
- Currency uses trimmed `request.Currency` or `AICOIN`.
- Experience level uses `request.ExperienceLevel` or `INTERMEDIATE`.
- Clarifying answers contains one empty string per question.

Expected: output is stable across test runs.

- [ ] **Step 2: Implement local refinement rules**

Mock refinement required behavior:

- Advisory messages return no changed fields and preserve the current suggestion.
- Clarifying answer messages update `ClarifyingAnswers`.
- Budget messages update `SuggestedBudgetMin` and `SuggestedBudgetMax`.
- Timeline messages update `SuggestedTimelineDays`.
- Experience messages update `ExperienceLevel`.
- `add skill` style messages add a skill when not already present.
- `remove skill` style messages remove a matching skill.
- Hourly/fixed messages update `BudgetType`.
- Currency messages update `Currency`; keep `AICOIN` as platform default unless message explicitly requests another supported currency.

Expected: TypeScript chat-edit behavior is represented in deterministic C# rules.

- [ ] **Step 3: Implement mock service-description generation**

Mock service-generator rules:

- Title includes the first one or two skills.
- Description includes `RawInput`, tone, target client, and language.
- Packages are exactly Basic, Standard, Premium.
- Basic price equals `PriceFrom`.
- Standard price equals `PriceFrom * 2`.
- Premium price equals `PriceFrom * 4`.
- Basic delivery days equals half of `DeliveryDays`, minimum `1`.
- Standard delivery days equals `DeliveryDays`.
- Premium delivery days equals `DeliveryDays * 2`.
- FAQs contains at least one question and answer.

Expected: expert service generation works without credentials.

- [ ] **Step 4: Add mock provider tests**

Create tests:

```csharp
[Fact]
public async Task MockSuggestionProvider_UsesRequestHints_AndAicoinDefault()

[Fact]
public async Task MockRefinementProvider_UpdatesBudgetTypeCurrencyAndSkills()

[Fact]
public async Task MockServiceDescriptionProvider_ReturnsThreePackagesAndFaqs()
```

Expected: deterministic providers are covered by focused tests.

- [ ] **Step 5: Commit PR 1 slice**

```powershell
git add Aivora.Services\AIJobAssistantService\Providers\MockAIJobSuggestionProvider.cs Aivora.Services\AIJobAssistantService\Providers\MockAIJobRefinementProvider.cs Aivora.Services\AIJobAssistantService\Providers\MockAIServiceDescriptionProvider.cs Aivora.Tests\Services\AIJobAssistantProviderTests.cs
git commit -m "feat(ai): add deterministic mock providers"
```

### Task 5: Refactor AI Service Generation And Retrieval

**Files:**

- Modify: `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\Service.cs`
- Modify: `D:\projects\swp-2026\Backend\Aivora.Tests\Services\AIJobAssistantServiceTests.cs`

- [ ] **Step 1: Write failing generation test**

Add:

```csharp
[Fact]
public async Task GenerateSuggestionAsync_UsesProviderOutput_AndPersistsStructuredFields()
```

Assert:

- Provider is called once.
- Response uses provider title, description, budget type, currency, experience level, business domain, expected outcome, skills, milestones, clarifying questions, clarifying answers, risks, and model name.
- Database row persists the same structured fields.

Expected before implementation: test fails because `Service` still owns inline mock logic.

- [ ] **Step 2: Inject generation provider**

Constructor shape:

```csharp
public Service(
    AivoraDbContext dbContext,
    JobService.IService jobService,
    IAIJobSuggestionProvider suggestionProvider,
    IAIJobRefinementProvider refinementProvider,
    IAIServiceDescriptionProvider serviceDescriptionProvider)
```

Expected: `Service` delegates generation to provider interfaces.

- [ ] **Step 3: Persist provider output**

Generation must populate:

- `RawInput`
- `SuggestedTitle`
- `SuggestedDescription`
- `SuggestedBudgetType`
- `Currency`
- `SuggestedBudgetMin`
- `SuggestedBudgetMax`
- `SuggestedTimelineDays`
- `SuggestedExperienceLevel`
- `SuggestedBusinessDomain`
- `SuggestedExpectedOutcome`
- `SuggestedSkillsJson`
- `SuggestedMilestonesJson`
- `ClarifyingQuestionsJson`
- `ClarifyingAnswersJson`
- `RiskWarningsJson`
- `AIModel`
- `Status = GENERATED`

Expected: generated rows contain all structured provider fields.

- [ ] **Step 4: Map structured response**

`MapToResponse` must deserialize JSON arrays with empty-list fallbacks and return:

- `BudgetType`
- `Currency`
- `ExperienceLevel`
- `BusinessDomain`
- `ExpectedOutcome`
- `ClarifyingAnswers`
- `RejectionReason`

Expected: API response has the same information stored in the entity.

- [ ] **Step 5: Implement get**

`GetSuggestionAsync` rules:

- Query by `suggestionId` and `clientId`.
- Throw `NotFoundException` when missing or owned by another client.
- Return `MapToResponse`.

Expected: client ownership is enforced.

- [ ] **Step 6: Run focused tests**

```powershell
dotnet test D:\projects\swp-2026\Backend\Aivora.Tests\Aivora.Tests.csproj --filter "GenerateSuggestionAsync_UsesProviderOutput_AndPersistsStructuredFields|GetSuggestionAsync"
```

Expected: generation and get tests pass after SDK is available.

- [ ] **Step 7: Commit PR 2 slice**

```powershell
git add Aivora.Services\AIJobAssistantService\Service.cs Aivora.Tests\Services\AIJobAssistantServiceTests.cs
git commit -m "feat(ai): persist provider-backed job suggestions"
```

### Task 6: Add Patch And Refine Backend Logic

**Files:**

- Modify: `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\Service.cs`
- Modify: `D:\projects\swp-2026\Backend\Aivora.Tests\Services\AIJobAssistantServiceTests.cs`

- [ ] **Step 1: Write patch tests**

Add:

```csharp
[Fact]
public async Task PatchSuggestionAsync_UpdatesAllowedGeneratedFields()

[Fact]
public async Task PatchSuggestionAsync_RejectsAcceptedSuggestion()

[Fact]
public async Task PatchSuggestionAsync_RejectsNonOwner()
```

Expected before implementation: tests fail because patch is not implemented.

- [ ] **Step 2: Implement patch rules**

Patch only allows generated suggestions. It may update:

- `SuggestedTitle`
- `SuggestedDescription`
- `SuggestedBusinessDomain`
- `SuggestedExpectedOutcome`
- `SuggestedBudgetType`
- `Currency`
- `SuggestedBudgetMin`
- `SuggestedBudgetMax`
- `SuggestedTimelineDays`
- `SuggestedExperienceLevel`
- `SuggestedSkillsJson`
- `SuggestedMilestonesJson`
- `ClarifyingAnswersJson`

Expected: status, client id, job id, raw input, AI model, rejection reason, and risk warnings are not patchable.

- [ ] **Step 3: Write refine tests**

Add:

```csharp
[Fact]
public async Task RefineSuggestionAsync_AdvisoryMessageDoesNotMutateSuggestion()

[Fact]
public async Task RefineSuggestionAsync_UpdatesBudgetTimelineExperienceSkillBudgetTypeCurrencyAndClarifyingAnswers()

[Fact]
public async Task RefineSuggestionAsync_RejectsProcessedSuggestion()
```

Expected before implementation: tests fail because refine is not implemented.

- [ ] **Step 4: Implement refine rules**

Refine flow:

1. Load current suggestion by id and client id.
2. Require `Status = GENERATED`.
3. Map current entity to `Response.SuggestionResponse`.
4. Call `IAIJobRefinementProvider.RefineSuggestionAsync(current, request.Message, cancellationToken)`.
5. If `ChangedFields` is empty, do not update the entity.
6. If `ChangedFields` has values, persist the returned suggestion fields.
7. Return `Response.RefineSuggestionResponse`.

Expected: advisory chat responses are supported without accidental mutation.

- [ ] **Step 5: Run focused tests**

```powershell
dotnet test D:\projects\swp-2026\Backend\Aivora.Tests\Aivora.Tests.csproj --filter "PatchSuggestionAsync|RefineSuggestionAsync"
```

Expected: patch and refine tests pass after SDK is available.

- [ ] **Step 6: Commit PR 2 slice**

```powershell
git add Aivora.Services\AIJobAssistantService\Service.cs Aivora.Tests\Services\AIJobAssistantServiceTests.cs
git commit -m "feat(ai): add suggestion patch and refine"
```

### Task 7: Fix Accept And Reject Business Rules

**Files:**

- Modify: `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\Service.cs`
- Modify: `D:\projects\swp-2026\Backend\Aivora.Tests\Services\AIJobAssistantServiceTests.cs`

- [ ] **Step 1: Write accept tests**

Add:

```csharp
[Fact]
public async Task AcceptSuggestionAsync_RequiresValidCategoryId()

[Fact]
public async Task AcceptSuggestionAsync_MapsStructuredFieldsIntoDraftJobRequest()

[Fact]
public async Task AcceptSuggestionAsync_CreatesDraftJob_AndMarksSuggestionAccepted()
```

Expected before implementation: the first test fails because `Guid.Empty` fallback exists.

- [ ] **Step 2: Remove empty category fallback**

Accept must reject:

- `CategoryId == null`
- `CategoryId == Guid.Empty`

Expected exception: `ValidationException` with message `CategoryId is required to accept an AI suggestion.`

- [ ] **Step 3: Create draft job from structured fields**

`CreateJobRequest` mapping:

- `Title = suggestion.SuggestedTitle ?? "New Job from AI"`
- `OriginalDescription = suggestion.RawInput`
- `FinalDescription = suggestion.SuggestedDescription`
- `BusinessDomain = suggestion.SuggestedBusinessDomain`
- `ExpectedOutcome = suggestion.SuggestedExpectedOutcome`
- `CategoryId = request.CategoryId.Value`
- `BudgetType = suggestion.SuggestedBudgetType`
- `Currency = suggestion.Currency`
- `BudgetMin = suggestion.SuggestedBudgetMin`
- `BudgetMax = suggestion.SuggestedBudgetMax`
- `TimelineDays = suggestion.SuggestedTimelineDays`
- `ExperienceLevel = suggestion.SuggestedExperienceLevel`
- `Visibility = JobVisibility.PRIVATE`
- `SkillIds = request.SelectedSkillIds ?? new List<Guid>()`

Expected: accepted suggestions create draft jobs and never publish automatically.

- [ ] **Step 4: Write reject tests**

Add:

```csharp
[Fact]
public async Task RejectSuggestionAsync_StoresTrimmedReason()

[Fact]
public async Task RejectSuggestionAsync_RequiresReasonBetween3And500Characters()

[Fact]
public async Task RejectSuggestionAsync_BlocksAcceptedSuggestion()
```

Expected before implementation: tests fail because rejection reason is not persisted and accepted suggestions can be overwritten.

- [ ] **Step 5: Implement reject rules**

Reject rules:

- Query by id and client id.
- Throw `NotFoundException` when missing or non-owned.
- Require `Status = GENERATED`.
- Require trimmed reason length from 3 through 500.
- Store trimmed reason in `RejectionReason`.
- Set `Status = REJECTED`.
- Return mapped suggestion response.

Expected: rejection is auditable and invalid transitions are blocked.

- [ ] **Step 6: Run focused tests**

```powershell
dotnet test D:\projects\swp-2026\Backend\Aivora.Tests\Aivora.Tests.csproj --filter "AcceptSuggestionAsync|RejectSuggestionAsync"
```

Expected: accept and reject tests pass after SDK is available.

- [ ] **Step 7: Commit PR 2 slice**

```powershell
git add Aivora.Services\AIJobAssistantService\Service.cs Aivora.Tests\Services\AIJobAssistantServiceTests.cs
git commit -m "feat(ai): enforce accept and reject rules"
```

### Task 8: Expand Job Service Mapping

**Files:**

- Modify: `D:\projects\swp-2026\Backend\Aivora.Services\JobService\Request.cs`
- Modify: `D:\projects\swp-2026\Backend\Aivora.Services\JobService\Response.cs`
- Modify: `D:\projects\swp-2026\Backend\Aivora.Services\JobService\Service.cs`
- Modify tests in `D:\projects\swp-2026\Backend\Aivora.Tests\Services` when existing job tests cover create/update.

- [ ] **Step 1: Expand create request**

Add:

```csharp
public string? BusinessDomain { get; set; }
public string? ExpectedOutcome { get; set; }
public string? Currency { get; set; }
```

Expected: AI accept can pass structured fields already present on `JobPost`.

- [ ] **Step 2: Expand update request**

Add:

```csharp
public string? BusinessDomain { get; set; }
public string? ExpectedOutcome { get; set; }
public string? Currency { get; set; }
```

Expected: direct job editing does not drop AI-backed fields.

- [ ] **Step 3: Expand response**

Add:

```csharp
public string? BusinessDomain { get; set; }
public string? ExpectedOutcome { get; set; }
```

Expected: job responses expose the same fields accepted from AI suggestions.

- [ ] **Step 4: Map create and update**

Create mapping:

- `BusinessDomain = request.BusinessDomain`
- `ExpectedOutcome = request.ExpectedOutcome`
- `Currency = string.IsNullOrWhiteSpace(request.Currency) ? "AICOIN" : request.Currency.Trim().ToUpperInvariant()`

Update mapping:

- Update business domain when request property is supplied.
- Update expected outcome when request property is supplied.
- Update currency when request property is supplied and not blank.

Expected: platform default remains `AICOIN`.

- [ ] **Step 5: Map response**

Return:

- `BusinessDomain = job.BusinessDomain`
- `ExpectedOutcome = job.ExpectedOutcome`

Expected: accepted job drafts show structured AI context.

- [ ] **Step 6: Commit PR 2 slice**

```powershell
git add Aivora.Services\JobService\Request.cs Aivora.Services\JobService\Response.cs Aivora.Services\JobService\Service.cs Aivora.Tests\Services
git commit -m "feat(jobs): map AI structured job fields"
```

### Task 9: Add AI Controller Endpoints And Status Codes

**Files:**

- Modify: `D:\projects\swp-2026\Backend\Aivora.api\Controllers\AIController.cs`

- [ ] **Step 1: Update generate status code**

`POST /api/v1/ai/job-assistant` must return `CreatedAtAction` or `StatusCode(StatusCodes.Status201Created, ...)`.

Expected: HTTP `201 Created`.

- [ ] **Step 2: Add get endpoint**

```csharp
[HttpGet("job-assistant/{id}")]
[Authorize(Policy = JwtExtensions.ClientPolicy)]
public async Task<IActionResult> GetJobSuggestion(Guid id, CancellationToken cancellationToken)
```

Expected: returns client-owned suggestion.

- [ ] **Step 3: Add patch endpoint**

```csharp
[HttpPatch("job-assistant/{id}")]
[Authorize(Policy = JwtExtensions.ClientPolicy)]
public async Task<IActionResult> PatchJobSuggestion(Guid id, [FromBody] Request.PatchSuggestionRequest request, CancellationToken cancellationToken)
```

Expected: returns updated suggestion.

- [ ] **Step 4: Add refine endpoint**

```csharp
[HttpPost("job-assistant/{id}/refine")]
[Authorize(Policy = JwtExtensions.ClientPolicy)]
public async Task<IActionResult> RefineJobSuggestion(Guid id, [FromBody] Request.RefineSuggestionRequest request, CancellationToken cancellationToken)
```

Expected: returns suggestion, AI response, and changed fields.

- [ ] **Step 5: Update accept status code**

`POST /api/v1/ai/job-assistant/{id}/accept` must return `201 Created`.

Expected: accepted draft job creation uses created status.

- [ ] **Step 6: Update reject response**

`POST /api/v1/ai/job-assistant/{id}/reject` must return the rejected suggestion response as `data`.

Expected: clients receive id, status, and rejection reason.

- [ ] **Step 7: Add expert service-generator endpoint**

```csharp
[HttpPost("service-generator")]
[Authorize(Policy = JwtExtensions.ExpertPolicy)]
public async Task<IActionResult> GenerateServiceDescription([FromBody] Request.GenerateServiceDescriptionRequest request, CancellationToken cancellationToken)
```

Expected: expert-only endpoint returns `201 Created`.

- [ ] **Step 8: Commit PR 2 or PR 4 slice**

```powershell
git add Aivora.api\Controllers\AIController.cs
git commit -m "feat(ai): expose full assistant endpoints"
```

### Task 10: Add Expert Service Generator Logic

**Files:**

- Modify: `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\Service.cs`
- Create: `D:\projects\swp-2026\Backend\Aivora.Tests\Services\AIServiceGeneratorTests.cs`

- [ ] **Step 1: Write validation tests**

Add tests:

```csharp
[Theory]
[InlineData("")]
[InlineData("short")]
public async Task GenerateServiceDescriptionAsync_RejectsRawInputOutsideAllowedLength(string rawInput)

[Fact]
public async Task GenerateServiceDescriptionAsync_RejectsSkillCountOutsideOneToTwenty()

[Fact]
public async Task GenerateServiceDescriptionAsync_RejectsInvalidPriceAndDeliveryDays()

[Fact]
public async Task GenerateServiceDescriptionAsync_RejectsInvalidToneTargetClientAndLanguage()
```

Expected before implementation: validation tests fail.

- [ ] **Step 2: Implement validation**

Validation rules:

- `RawInput.Trim().Length` from 20 through 4000.
- `Skills.Count` from 1 through 20.
- Each skill trimmed and non-empty.
- `PriceFrom > 0` and `PriceFrom <= 100000`.
- `DeliveryDays >= 1` and `DeliveryDays <= 365`.
- `Tone` one of `professional`, `friendly`, `premium`, `technical`.
- `TargetClient` one of `startup`, `sme`, `enterprise`, `individual`.
- `Language` one of `vi`, `en`.

Expected: invalid requests throw `ValidationException`.

- [ ] **Step 3: Write provider usage test**

Add:

```csharp
[Fact]
public async Task GenerateServiceDescriptionAsync_UsesProviderOutput()
```

Assert:

- Provider is called once.
- Response contains provider title, description, Basic, Standard, Premium packages, and FAQs.

Expected before implementation: provider is not called.

- [ ] **Step 4: Implement provider-backed service generation**

Flow:

1. Normalize request strings.
2. Validate request.
3. Call `IAIServiceDescriptionProvider.GenerateServiceDescriptionAsync`.
4. Require exactly three package tiers named Basic, Standard, Premium.
5. Return `Response.ServiceDescriptionResponse`.

Expected: service-generator behavior matches the TypeScript reference backend.

- [ ] **Step 5: Run focused tests**

```powershell
dotnet test D:\projects\swp-2026\Backend\Aivora.Tests\Aivora.Tests.csproj --filter "GenerateServiceDescriptionAsync|ServiceGenerator"
```

Expected: service-generator tests pass after SDK is available.

- [ ] **Step 6: Commit PR 4 slice**

```powershell
git add Aivora.Services\AIJobAssistantService\Service.cs Aivora.Tests\Services\AIServiceGeneratorTests.cs
git commit -m "feat(ai): add expert service generator"
```

### Task 11: Add Gemini Provider Support

**Files:**

- Create: `D:\projects\swp-2026\Backend\Aivora.Services\Options\AIProviderOptions.cs`
- Create: `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\Providers\GeminiAIJobSuggestionProvider.cs`
- Create: `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\Providers\GeminiAIJobRefinementProvider.cs`
- Create: `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\Providers\GeminiAIServiceDescriptionProvider.cs`
- Modify: `D:\projects\swp-2026\Backend\Aivora.api\Program.cs`
- Modify: `D:\projects\swp-2026\Backend\Aivora.api\appsettings.json`
- Modify tests: `D:\projects\swp-2026\Backend\Aivora.Tests\Services\AIJobAssistantProviderTests.cs`

- [ ] **Step 1: Add options**

```csharp
public class AIProviderOptions
{
    public string Provider { get; set; } = "Mock";
    public string? ApiKey { get; set; }
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com";
    public string Model { get; set; } = "gemini-2.5-flash";
    public bool EnableFallback { get; set; } = true;
}
```

Expected: production can select Gemini through configuration and local development can use mock providers.

- [ ] **Step 2: Add appsettings defaults**

```json
"AIProvider": {
  "Provider": "Mock",
  "ApiKey": "",
  "BaseUrl": "https://generativelanguage.googleapis.com",
  "Model": "gemini-2.5-flash",
  "EnableFallback": true
}
```

Expected: no secret is committed. Real keys come from environment variables or user secrets.

- [ ] **Step 3: Implement Gemini providers**

Each Gemini provider must:

- Use a prompt builder.
- Call Gemini with `HttpClient`.
- Parse the provider response through a parser class.
- Use mock fallback when `EnableFallback = true` and API key is blank.
- Use mock fallback when provider call fails and `EnableFallback = true`.
- Throw `ValidationException` or a provider-specific exception when provider call fails and fallback is disabled.

Expected: generation, refinement, and service description generation all support Gemini.

- [ ] **Step 4: Register providers and options**

Registration rules:

- Configure `AIProviderOptions` from `AIProvider`.
- Register prompt builders and parsers.
- Register mock providers.
- Register Gemini providers with `HttpClient`.
- Register `IAIJobSuggestionProvider`, `IAIJobRefinementProvider`, and `IAIServiceDescriptionProvider` using a factory that chooses Gemini only when provider is `Gemini` and an API key is present.

Expected: local default remains deterministic mock behavior.

- [ ] **Step 5: Add fake HTTP tests**

Create tests:

```csharp
[Fact]
public async Task GeminiSuggestionProvider_ParsesValidJsonWithoutRealNetwork()

[Fact]
public async Task GeminiRefinementProvider_FallsBackWhenConfiguredAndHttpFails()

[Fact]
public async Task GeminiServiceDescriptionProvider_ThrowsWhenFallbackDisabledAndHttpFails()
```

Expected: Gemini coverage does not require real credentials.

- [ ] **Step 6: Commit PR 3 slice**

```powershell
git add Aivora.Services\Options\AIProviderOptions.cs Aivora.Services\AIJobAssistantService\Providers\GeminiAIJobSuggestionProvider.cs Aivora.Services\AIJobAssistantService\Providers\GeminiAIJobRefinementProvider.cs Aivora.Services\AIJobAssistantService\Providers\GeminiAIServiceDescriptionProvider.cs Aivora.api\Program.cs Aivora.api\appsettings.json Aivora.Tests\Services\AIJobAssistantProviderTests.cs
git commit -m "feat(ai): add Gemini provider support"
```

### Task 12: Upgrade Recommendation Scoring

**Files:**

- Modify: `D:\projects\swp-2026\Backend\Aivora.Services\RecommendationService\Service.cs`
- Modify: `D:\projects\swp-2026\Backend\Aivora.Services\RecommendationService\Response.cs`
- Create: `D:\projects\swp-2026\Backend\Aivora.Tests\Services\RecommendationServiceTests.cs`

- [ ] **Step 1: Write recommendation tests**

Add:

```csharp
[Fact]
public async Task GenerateRecommendationsAsync_UsesWeightedSkillLevels()

[Fact]
public async Task GenerateRecommendationsAsync_ScoresHourlyBudget()

[Fact]
public async Task GenerateRecommendationsAsync_ScoresFixedBudgetFromTimelineAndHourlyRate()

[Fact]
public async Task GenerateRecommendationsAsync_ScoresAvailabilityRatingAndCompletion()

[Fact]
public async Task GenerateRecommendationsAsync_PersistsAllScoreComponents()

[Fact]
public async Task GenerateRecommendationsAsync_KeepsClientOwnershipAndOpenJobRequirement()
```

Expected before implementation: tests fail because current service uses simple scores and leaves component fields at defaults.

- [ ] **Step 2: Fetch active experts and skills**

Query requirements:

- Keep current client ownership check.
- Keep current `JobStatus.OPEN` requirement.
- Include `JobSkills`.
- Include active expert users and their `ExpertProfile`.
- Include `ExpertSkills`.

Expected: scoring can consider all active experts and all their skills.

- [ ] **Step 3: Implement skill score**

Rules:

- If required skill count is zero, `SkillScore = 100`.
- `BEGINNER = 0.5`.
- `INTERMEDIATE = 0.75`.
- `ADVANCED = 0.9`.
- `EXPERT = 1.0`.
- `SkillScore = matchedPoints / requiredSkills.Count * 100`.

Expected: skill level quality changes the final ranking.

- [ ] **Step 4: Implement budget score**

Rules:

- Default hourly rate is `25` when missing.
- Default budget min is `0`.
- Default budget max is `999999`.
- For hourly jobs, compare hourly rate to budget range.
- For fixed jobs, estimate cost as `hourlyRate * timelineDays * 6`.
- Inside range gives `100`.
- Below min gives `95`.
- Above max subtracts `(excess / budgetMax) * 100`, floored at `0`.

Expected: both fixed and hourly jobs affect recommendations.

- [ ] **Step 5: Implement remaining score components**

Rules:

- `RatingScore = Rating * 20`.
- `AvailabilityScore = 100` when `AvailabilityStatus = AVAILABLE`, otherwise `50`.
- `CompletionScore = SuccessRate` when positive, otherwise `80`.
- `PortfolioScore = 0` until portfolio data exists in the current backend.
- `TotalScore = SkillScore * 0.40 + BudgetScore * 0.20 + RatingScore * 0.20 + AvailabilityScore * 0.10 + CompletionScore * 0.10`.

Expected: total score uses the TypeScript reference weighting baseline and existing C# columns are populated.

- [ ] **Step 6: Persist and return all score components**

Persist:

- `SkillScore`
- `BudgetScore`
- `RatingScore`
- `AvailabilityScore`
- `CompletionScore`
- `PortfolioScore`
- `TotalScore`
- `Explanation`

Return the same fields in `RecommendationResponse`.

Expected: database and API response match.

- [ ] **Step 7: Run focused tests**

```powershell
dotnet test D:\projects\swp-2026\Backend\Aivora.Tests\Aivora.Tests.csproj --filter "Recommendation"
```

Expected: recommendation tests pass after SDK is available.

- [ ] **Step 8: Commit PR 5 slice**

```powershell
git add Aivora.Services\RecommendationService\Service.cs Aivora.Services\RecommendationService\Response.cs Aivora.Tests\Services\RecommendationServiceTests.cs
git commit -m "feat(recommendations): upgrade weighted scoring"
```

### Task 13: Full Backend Migration Verification

**Files:**

- Read: `D:\projects\swp-2026\Backend\Aivora.sln`
- Read: `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\docs\api-contract.md`
- Read: `D:\projects\swp-2026\Backend\Aivora.Repositories\Data\Migrations`

- [ ] **Step 1: Build after SDK is available**

```powershell
dotnet build D:\projects\swp-2026\Backend\Aivora.sln --no-restore
```

Expected: build passes.

- [ ] **Step 2: Run AI assistant tests**

```powershell
dotnet test D:\projects\swp-2026\Backend\Aivora.Tests\Aivora.Tests.csproj --filter "AIJobAssistantServiceTests|AIJobAssistantProviderTests|AIServiceGeneratorTests"
```

Expected: AI assistant, provider, and service-generator tests pass.

- [ ] **Step 3: Run recommendation tests**

```powershell
dotnet test D:\projects\swp-2026\Backend\Aivora.Tests\Aivora.Tests.csproj --filter "Recommendation"
```

Expected: recommendation tests pass.

- [ ] **Step 4: Run full solution tests**

```powershell
dotnet test D:\projects\swp-2026\Backend\Aivora.sln
```

Expected: full solution tests pass.

- [ ] **Step 5: Verify idempotent migration script**

```powershell
dotnet ef migrations script --idempotent --project D:\projects\swp-2026\Backend\Aivora.Repositories --startup-project D:\projects\swp-2026\Backend\Aivora.api
```

Expected: script succeeds and contains safe defaults for required AI suggestion columns.

- [ ] **Step 6: Manually verify required endpoints with JWTs**

Client JWT:

```http
POST /api/v1/ai/job-assistant
GET /api/v1/ai/job-assistant/{suggestionId}
PATCH /api/v1/ai/job-assistant/{suggestionId}
POST /api/v1/ai/job-assistant/{suggestionId}/refine
POST /api/v1/ai/job-assistant/{suggestionId}/accept
POST /api/v1/ai/job-assistant/{suggestionId}/reject
POST /api/v1/jobs/{jobId}/recommendations/generate
GET /api/v1/jobs/{jobId}/recommendations
```

Expert JWT:

```http
POST /api/v1/ai/service-generator
```

Expected:

- Generate returns `201` and `status = GENERATED`.
- Get returns the same client-owned suggestion.
- Patch updates allowed fields only.
- Refine returns `aiResponse` and `changedFields`.
- Accept returns `201`, creates a `DRAFT` job, and marks suggestion `ACCEPTED`.
- Reject returns `200`, stores reason, and marks suggestion `REJECTED`.
- Service generator returns `201` with three package tiers and FAQs.
- Recommendation generation returns sorted weighted recommendations.

- [ ] **Step 7: Verify TypeScript reference tree is untouched**

```powershell
git diff --name-only -- Aivora.Services\AIJobAssistantService\reference
```

Expected: no output.

- [ ] **Step 8: Verify no old reduced-scope labels remain**

Review this document and confirm it does not describe required backend features as deferrable phases.

Expected: every AI assistant endpoint, the service generator, Gemini support, and recommendation scoring are listed as required backend migration work.

## Risk Register

- No local .NET SDK: build, tests, and EF migration commands cannot be completed until a compatible SDK is installed or selected.
- Currency mismatch: backend currently defaults to `AICOIN`; keep `AICOIN` in C# defaults unless the team makes a platform-wide currency change.
- Reference schema mismatch: TypeScript writes helper milestone rows, but the current C# job model does not expose a `JobMilestones` table for job drafts; keep suggested milestones in AI suggestion JSON for this migration.
- Gemini API drift: isolate provider transport, prompt building, and parsing so provider changes do not rewrite service logic.
- AI JSON variance: parser classes must normalize missing arrays, missing currency, missing budget type, and code-fenced JSON.
- Authorization drift: every client AI suggestion route must query by both suggestion id and client id; service generator must use expert policy.
- Data migration safety: required AI suggestion columns must have defaults so existing rows are not broken.
- Recommendation scoring drift: keep ownership and `OPEN` job checks while changing the score algorithm.

## Completion Audit Checklist

- [ ] The plan file contains no old reduced-scope label from the previous version.
- [ ] The plan file contains no phase label that marks required backend features as deferrable.
- [ ] Get, patch, refine, expert service generator, Gemini providers, and recommendation scoring are required tasks.
- [ ] Provider support covers suggestion generation, refinement, and service description generation.
- [ ] Prompt builder and parser responsibilities are separated from Gemini transport.
- [ ] EF default-value safety is specified for required new columns.
- [ ] `ClarifyingAnswersJson` storage guidance is specified.
- [ ] PR splitting strategy is included.
- [ ] `Guid.Empty` category fallback is removed during implementation.
- [ ] AI suggestions cannot auto-publish jobs.
- [ ] Rejection reason is persisted.
- [ ] Structured AI fields are persisted and returned.
- [ ] Accepted suggestions map structured fields into draft job creation.
- [ ] Deterministic mock providers work with no API key.
- [ ] Gemini providers are configurable and covered without real network credentials.
- [ ] Recommendation score components are calculated, persisted, and returned.
- [ ] EF migration exists and idempotent script generation succeeds.
- [ ] Focused AI, service-generator, and recommendation tests pass.
- [ ] Full solution tests pass.
- [ ] Reference TypeScript files are unchanged.

## Execution Handoff

Plan complete in `D:\projects\swp-2026\Backend\Aivora.Services\AIJobAssistantService\docs\ai-job-assistant-integration-plan.md`.

Recommended execution order:

1. Install or select a compatible .NET SDK.
2. Execute PR 1 tasks for persistence, DTOs, providers, prompts, parsers, and mock providers.
3. Execute PR 2 tasks for AI assistant service and controller behavior.
4. Execute PR 3 tasks for Gemini providers.
5. Execute PR 4 tasks for expert service generation.
6. Execute PR 5 tasks for recommendation scoring.
7. Run the full verification checklist.
