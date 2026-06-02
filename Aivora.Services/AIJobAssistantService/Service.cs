using System.Text.Json;
using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.AIJobAssistantService.Parsing;
using Aivora.Services.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Aivora.Services.AIJobAssistantService;

public class Service : IService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly HashSet<string> AllowedTones = new(StringComparer.OrdinalIgnoreCase) { "professional", "friendly", "premium", "technical" };
    private static readonly HashSet<string> AllowedTargetClients = new(StringComparer.OrdinalIgnoreCase) { "startup", "sme", "enterprise", "individual" };
    private static readonly HashSet<string> AllowedLanguages = new(StringComparer.OrdinalIgnoreCase) { "vi", "en" };

    private readonly AivoraDbContext _dbContext;
    private readonly JobService.IService _jobService;
    private readonly IAIJobSuggestionProvider _suggestionProvider;
    private readonly IAIJobRefinementProvider _refinementProvider;
    private readonly IAIServiceDescriptionProvider _serviceDescriptionProvider;

    public Service(
        AivoraDbContext dbContext,
        JobService.IService jobService,
        IAIJobSuggestionProvider suggestionProvider,
        IAIJobRefinementProvider refinementProvider,
        IAIServiceDescriptionProvider serviceDescriptionProvider)
    {
        _dbContext = dbContext;
        _jobService = jobService;
        _suggestionProvider = suggestionProvider;
        _refinementProvider = refinementProvider;
        _serviceDescriptionProvider = serviceDescriptionProvider;
    }

    public async Task<Response.SuggestionResponse> GenerateSuggestionAsync(Guid clientId, Request.GenerateSuggestionRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null) throw new ValidationException("Request body is required.");
        if (string.IsNullOrWhiteSpace(request.RawInput) || request.RawInput.Trim().Length < 5)
        {
            throw new ValidationException("RawInput must be at least 5 characters long.");
        }

        request.RawInput = request.RawInput.Trim();
        request.BusinessDomain = NormalizeLimited(request.BusinessDomain, 255);
        request.ExpectedOutcome = NormalizeLimited(request.ExpectedOutcome, 1000);
        request.Currency = AIJsonParser.NormalizeCurrency(request.Currency);

        var draft = await _suggestionProvider.GenerateSuggestionAsync(request, cancellationToken);
        var suggestion = new AIJobSuggestion
        {
            ClientId = clientId,
            RawInput = request.RawInput,
            Status = AIJobSuggestionStatus.GENERATED
        };

        ApplyDraft(suggestion, draft, updateRiskWarnings: true);
        _dbContext.AIJobSuggestions.Add(suggestion);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToResponse(suggestion);
    }

    public async Task<Response.SuggestionResponse> GetSuggestionAsync(Guid clientId, Guid suggestionId, CancellationToken cancellationToken = default)
    {
        var suggestion = await LoadSuggestionAsync(clientId, suggestionId, cancellationToken);
        return MapToResponse(suggestion);
    }

    public async Task<Response.SuggestionResponse> PatchSuggestionAsync(Guid clientId, Guid suggestionId, Request.PatchSuggestionRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null) throw new ValidationException("Request body is required.");

        var suggestion = await LoadGeneratedSuggestionAsync(clientId, suggestionId, cancellationToken);

        if (request.SuggestedTitle is not null) suggestion.SuggestedTitle = NormalizeRequiredLimited(request.SuggestedTitle, suggestion.SuggestedTitle ?? "New Job from AI", 255);
        if (request.SuggestedDescription is not null) suggestion.SuggestedDescription = request.SuggestedDescription.Trim();
        if (request.BusinessDomain is not null) suggestion.SuggestedBusinessDomain = NormalizeLimited(request.BusinessDomain, 255);
        if (request.ExpectedOutcome is not null) suggestion.SuggestedExpectedOutcome = NormalizeLimited(request.ExpectedOutcome, 1000);
        if (request.BudgetType.HasValue) suggestion.SuggestedBudgetType = request.BudgetType.Value;
        if (request.Currency is not null) suggestion.Currency = AIJsonParser.NormalizeCurrency(request.Currency);
        if (request.SuggestedBudgetMin.HasValue) suggestion.SuggestedBudgetMin = request.SuggestedBudgetMin.Value;
        if (request.SuggestedBudgetMax.HasValue) suggestion.SuggestedBudgetMax = request.SuggestedBudgetMax.Value;
        if (request.SuggestedTimelineDays.HasValue) suggestion.SuggestedTimelineDays = request.SuggestedTimelineDays.Value;
        if (request.ExperienceLevel.HasValue) suggestion.SuggestedExperienceLevel = request.ExperienceLevel.Value;
        if (request.SuggestedSkills is not null) suggestion.SuggestedSkillsJson = SerializeList(NormalizeStringList(request.SuggestedSkills));
        if (request.SuggestedMilestones is not null) suggestion.SuggestedMilestonesJson = SerializeList(request.SuggestedMilestones);
        if (request.ClarifyingAnswers is not null) suggestion.ClarifyingAnswersJson = SerializeList(request.ClarifyingAnswers);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapToResponse(suggestion);
    }

    public async Task<Response.RefineSuggestionResponse> RefineSuggestionAsync(Guid clientId, Guid suggestionId, Request.RefineSuggestionRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null) throw new ValidationException("Request body is required.");
        if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Trim().Length < 3)
        {
            throw new ValidationException("Message must be at least 3 characters long.");
        }

        var suggestion = await LoadGeneratedSuggestionAsync(clientId, suggestionId, cancellationToken);
        var current = MapToResponse(suggestion);
        var refinement = await _refinementProvider.RefineSuggestionAsync(current, request.Message.Trim(), cancellationToken);

        if (refinement.ChangedFields.Count > 0)
        {
            ApplyDraft(suggestion, refinement.Suggestion, updateRiskWarnings: true);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return new Response.RefineSuggestionResponse
        {
            Suggestion = MapToResponse(suggestion),
            AIResponse = refinement.AIResponse,
            ChangedFields = refinement.ChangedFields
        };
    }

    public async Task<Response.AcceptResultResponse> AcceptSuggestionAsync(Guid clientId, Guid suggestionId, Request.AcceptSuggestionRequest request, CancellationToken cancellationToken = default)
    {
        if (request?.CategoryId is null || request.CategoryId.Value == Guid.Empty)
        {
            throw new ValidationException("CategoryId is required to accept an AI suggestion.");
        }

        var suggestion = await LoadGeneratedSuggestionAsync(clientId, suggestionId, cancellationToken);

        using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var createJobRequest = new JobService.Request.CreateJobRequest
            {
                Title = suggestion.SuggestedTitle ?? "New Job from AI",
                OriginalDescription = suggestion.RawInput,
                FinalDescription = suggestion.SuggestedDescription,
                BusinessDomain = suggestion.SuggestedBusinessDomain,
                ExpectedOutcome = suggestion.SuggestedExpectedOutcome,
                CategoryId = request.CategoryId.Value,
                BudgetType = suggestion.SuggestedBudgetType,
                Currency = suggestion.Currency,
                BudgetMin = suggestion.SuggestedBudgetMin,
                BudgetMax = suggestion.SuggestedBudgetMax,
                TimelineDays = suggestion.SuggestedTimelineDays,
                ExperienceLevel = suggestion.SuggestedExperienceLevel,
                Visibility = JobVisibility.PRIVATE,
                SkillIds = request.SelectedSkillIds ?? new List<Guid>()
            };

            var jobResponse = await _jobService.CreateJobAsync(clientId, createJobRequest);

            suggestion.Status = AIJobSuggestionStatus.ACCEPTED;
            suggestion.JobId = jobResponse.Id;

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new Response.AcceptResultResponse { Job = jobResponse };
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<Response.SuggestionResponse> RejectSuggestionAsync(Guid clientId, Guid suggestionId, Request.RejectSuggestionRequest request, CancellationToken cancellationToken = default)
    {
        var suggestion = await LoadGeneratedSuggestionAsync(clientId, suggestionId, cancellationToken);
        var reason = request?.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length < 3 || reason.Length > 500)
        {
            throw new ValidationException("Rejection reason must be between 3 and 500 characters.");
        }

        suggestion.Status = AIJobSuggestionStatus.REJECTED;
        suggestion.RejectionReason = reason;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapToResponse(suggestion);
    }

    public async Task<Response.ServiceDescriptionResponse> GenerateServiceDescriptionAsync(Guid expertId, Request.GenerateServiceDescriptionRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null) throw new ValidationException("Request body is required.");
        NormalizeAndValidateServiceDescriptionRequest(request);

        var draft = await _serviceDescriptionProvider.GenerateServiceDescriptionAsync(request, cancellationToken);
        var packages = NormalizePackages(draft.Packages);

        return new Response.ServiceDescriptionResponse
        {
            SuggestedTitle = draft.SuggestedTitle,
            SuggestedDescription = draft.SuggestedDescription,
            Packages = packages,
            Faqs = draft.Faqs
        };
    }

    private async Task<AIJobSuggestion> LoadSuggestionAsync(Guid clientId, Guid suggestionId, CancellationToken cancellationToken)
    {
        var suggestion = await _dbContext.AIJobSuggestions
            .FirstOrDefaultAsync(s => s.Id == suggestionId && s.ClientId == clientId, cancellationToken);

        return suggestion ?? throw new NotFoundException("AI Suggestion not found.");
    }

    private async Task<AIJobSuggestion> LoadGeneratedSuggestionAsync(Guid clientId, Guid suggestionId, CancellationToken cancellationToken)
    {
        var suggestion = await LoadSuggestionAsync(clientId, suggestionId, cancellationToken);
        if (suggestion.Status != AIJobSuggestionStatus.GENERATED)
        {
            throw new ValidationException("Suggestion is already processed.");
        }

        return suggestion;
    }

    private static void ApplyDraft(AIJobSuggestion suggestion, AIJobSuggestionDraft draft, bool updateRiskWarnings)
    {
        suggestion.SuggestedTitle = NormalizeRequiredLimited(draft.SuggestedTitle, "New Job from AI", 255);
        suggestion.SuggestedDescription = draft.SuggestedDescription;
        suggestion.SuggestedBusinessDomain = NormalizeLimited(draft.BusinessDomain, 255);
        suggestion.SuggestedExpectedOutcome = NormalizeLimited(draft.ExpectedOutcome, 1000);
        suggestion.SuggestedBudgetType = draft.BudgetType;
        suggestion.Currency = AIJsonParser.NormalizeCurrency(draft.Currency);
        suggestion.SuggestedBudgetMin = draft.SuggestedBudgetMin;
        suggestion.SuggestedBudgetMax = draft.SuggestedBudgetMax;
        suggestion.SuggestedTimelineDays = draft.SuggestedTimelineDays;
        suggestion.SuggestedExperienceLevel = draft.ExperienceLevel;
        suggestion.SuggestedSkillsJson = SerializeList(draft.SuggestedSkills);
        suggestion.SuggestedMilestonesJson = SerializeList(draft.SuggestedMilestones);
        suggestion.ClarifyingQuestionsJson = SerializeList(draft.ClarifyingQuestions);
        suggestion.ClarifyingAnswersJson = SerializeList(draft.ClarifyingAnswers);
        suggestion.AIModel = draft.AIModel;
        if (updateRiskWarnings)
        {
            suggestion.RiskWarningsJson = SerializeList(draft.RiskWarnings);
        }
    }

    private static Response.SuggestionResponse MapToResponse(AIJobSuggestion s)
    {
        return new Response.SuggestionResponse
        {
            Id = s.Id,
            JobId = s.JobId,
            ClientId = s.ClientId,
            RawInput = s.RawInput,
            SuggestedTitle = s.SuggestedTitle,
            SuggestedDescription = s.SuggestedDescription,
            BusinessDomain = s.SuggestedBusinessDomain,
            ExpectedOutcome = s.SuggestedExpectedOutcome,
            BudgetType = s.SuggestedBudgetType,
            Currency = s.Currency,
            SuggestedBudgetMin = s.SuggestedBudgetMin,
            SuggestedBudgetMax = s.SuggestedBudgetMax,
            SuggestedTimelineDays = s.SuggestedTimelineDays,
            ExperienceLevel = s.SuggestedExperienceLevel,
            SuggestedSkills = DeserializeList<string>(s.SuggestedSkillsJson),
            SuggestedMilestones = DeserializeList<Response.SuggestedMilestone>(s.SuggestedMilestonesJson),
            ClarifyingQuestions = DeserializeList<string>(s.ClarifyingQuestionsJson),
            ClarifyingAnswers = DeserializeList<string>(s.ClarifyingAnswersJson),
            RiskWarnings = DeserializeList<string>(s.RiskWarningsJson),
            AIModel = s.AIModel,
            Status = s.Status.ToString(),
            RejectionReason = s.RejectionReason,
            CreatedAt = s.CreatedAt
        };
    }

    private static void NormalizeAndValidateServiceDescriptionRequest(Request.GenerateServiceDescriptionRequest request)
    {
        request.RawInput = request.RawInput?.Trim() ?? string.Empty;
        if (request.RawInput.Length < 20 || request.RawInput.Length > 4000)
        {
            throw new ValidationException("RawInput must be between 20 and 4000 characters.");
        }

        request.Skills = NormalizeStringList(request.Skills);
        if (request.Skills.Count < 1 || request.Skills.Count > 20)
        {
            throw new ValidationException("Skills must contain between 1 and 20 items.");
        }

        if (request.PriceFrom <= 0 || request.PriceFrom > 100000)
        {
            throw new ValidationException("PriceFrom must be greater than 0 and less than or equal to 100000.");
        }

        if (request.DeliveryDays < 1 || request.DeliveryDays > 365)
        {
            throw new ValidationException("DeliveryDays must be between 1 and 365.");
        }

        request.Tone = NormalizeChoice(request.Tone, "professional");
        request.TargetClient = NormalizeChoice(request.TargetClient, "startup");
        request.Language = NormalizeChoice(request.Language, "vi");

        if (!AllowedTones.Contains(request.Tone)) throw new ValidationException("Tone is invalid.");
        if (!AllowedTargetClients.Contains(request.TargetClient)) throw new ValidationException("TargetClient is invalid.");
        if (!AllowedLanguages.Contains(request.Language)) throw new ValidationException("Language is invalid.");
    }

    private static List<Response.ServicePackageResponse> NormalizePackages(List<Response.ServicePackageResponse> packages)
    {
        var requiredNames = new[] { "Basic", "Standard", "Premium" };
        if (packages.Count != 3)
        {
            throw new ValidationException("Service description must include exactly three package tiers.");
        }

        for (var i = 0; i < requiredNames.Length; i++)
        {
            packages[i].Name = requiredNames[i];
            packages[i].Title ??= $"{requiredNames[i]} Package";
            if (string.IsNullOrWhiteSpace(packages[i].Description))
            {
                throw new ValidationException("Each service package must include a description.");
            }
        }

        return packages;
    }

    private static List<string> NormalizeStringList(IEnumerable<string?>? values)
    {
        return (values ?? Enumerable.Empty<string?>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? NormalizeLimited(string? value, int maxLength)
    {
        var normalized = NormalizeNullable(value);
        if (normalized is null)
        {
            return null;
        }

        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string NormalizeRequiredLimited(string? value, string fallback, int maxLength)
    {
        var normalized = NormalizeNullable(value) ?? fallback;
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string NormalizeChoice(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToLowerInvariant();
    }

    private static string SerializeList<T>(IEnumerable<T> values)
    {
        return JsonSerializer.Serialize(values, JsonOptions);
    }

    private static List<T> DeserializeList<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<T>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? new List<T>();
        }
        catch (JsonException)
        {
            return new List<T>();
        }
    }
}
