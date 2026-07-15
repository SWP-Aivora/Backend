using System.Text.Json;
using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.AIJobAssistantService.Parsing;
using Aivora.Services.Exceptions;
using Aivora.Services.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aivora.Services.AIJobAssistantService;

/// <summary>
/// AI Job Assistant service for generating job suggestions, refinements, and service descriptions
/// </summary>
public class Service : IService
{
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly HashSet<string> AllowedTones = new(StringComparer.OrdinalIgnoreCase) { "professional", "friendly", "premium", "technical" };
    private static readonly HashSet<string> AllowedTargetClients = new(StringComparer.OrdinalIgnoreCase) { "startup", "sme", "enterprise", "individual" };
    private static readonly HashSet<string> AllowedLanguages = new(StringComparer.OrdinalIgnoreCase) { "vi", "en" };

    private readonly AivoraDbContext _dbContext;
    private readonly JobService.IService _jobService;
    private readonly IAIJobSuggestionProvider _suggestionProvider;
    private readonly IAIJobRefinementProvider _refinementProvider;
    private readonly IAIServiceDescriptionProvider _serviceDescriptionProvider;
    private readonly CategoryService.IService _categoryService;
    private readonly Microsoft.Extensions.Logging.ILogger<Service> _logger;
    private readonly IOptions<ExchangeRateOptions> _exchangeRates;

    /// <summary>
    /// Initializes a new instance of the AI Job Assistant service
    /// </summary>
    /// <param name="dbContext">Database context</param>
    /// <param name="jobService">Job service for job operations</param>
    /// <param name="suggestionProvider">AI provider for job suggestions</param>
    /// <param name="refinementProvider">AI provider for job refinements</param>
    /// <param name="serviceDescriptionProvider">AI provider for service descriptions</param>
    /// <param name="exchangeRates">Configured currency-to-AICOIN exchange rates</param>
    public Service(
        AivoraDbContext dbContext,
        JobService.IService jobService,
        IAIJobSuggestionProvider suggestionProvider,
        IAIJobRefinementProvider refinementProvider,
        IAIServiceDescriptionProvider serviceDescriptionProvider,
        CategoryService.IService categoryService,
        Microsoft.Extensions.Logging.ILogger<Service> logger,
        Microsoft.Extensions.Options.IOptions<ExchangeRateOptions> exchangeRates)
    {
        _dbContext = dbContext;
        _jobService = jobService;
        _suggestionProvider = suggestionProvider;
        _refinementProvider = refinementProvider;
        _serviceDescriptionProvider = serviceDescriptionProvider;
        _categoryService = categoryService;
        _logger = logger;
        _exchangeRates = exchangeRates;
    }

    /// <summary>
    /// Generate AI-powered job suggestions based on client requirements
    /// </summary>
    /// <param name="clientId">Client user ID</param>
    /// <param name="request">Job suggestion generation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Job suggestion response with AI-generated options</returns>
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
        ValidateBudgetAndTimeline(request.BudgetMin, request.BudgetMax, request.TimelineDays);

        request.CategoriesContext = await GetCategoriesContextAsync(cancellationToken);

        var draft = await _suggestionProvider.GenerateSuggestionAsync(request, cancellationToken);

        var mappedCategoryId = await ValidateAndNormalizeCategoryNameAsync(draft.CategoryName, "generation", cancellationToken);

        var suggestion = new AIJobSuggestion
        {
            ClientId = clientId,
            RawInput = request.RawInput,
            Status = AIJobSuggestionStatus.GENERATED
        };

        ApplyDraft(suggestion, draft, updateRiskWarnings: true, _exchangeRates.Value.ToAicoin);
        suggestion.SuggestedCategoryId = mappedCategoryId;
        ValidateSuggestionShape(suggestion);
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
        if (request.SuggestedMilestones is not null) suggestion.SuggestedMilestonesJson = SerializeList(NormalizeMilestones(request.SuggestedMilestones));
        if (request.ClarifyingAnswers is not null) suggestion.ClarifyingAnswersJson = SerializeList(request.ClarifyingAnswers);

        ValidateSuggestionShape(suggestion);
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

        request.CategoriesContext = await GetCategoriesContextAsync(cancellationToken);

        var refinement = await _refinementProvider.RefineSuggestionAsync(current, request, cancellationToken);
        Guid? mappedCategoryId = null;

        if (refinement.ChangedFields.Count > 0)
        {
            mappedCategoryId = await ValidateAndNormalizeCategoryNameAsync(refinement.Suggestion.CategoryName, "refinement", cancellationToken);

            ApplyDraft(suggestion, refinement.Suggestion, updateRiskWarnings: true, _exchangeRates.Value.ToAicoin);
            if (mappedCategoryId != null)
            {
                suggestion.SuggestedCategoryId = mappedCategoryId;
            }
            ValidateSuggestionShape(suggestion);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var response = MapToResponse(suggestion);

        return new Response.RefineSuggestionResponse
        {
            Suggestion = response,
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
        ValidateSuggestionShape(suggestion);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
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
                SkillIds = NormalizeGuidList(request.SelectedSkillIds),
                Milestones = DeserializeList<Response.SuggestedMilestone>(suggestion.SuggestedMilestonesJson)
                    .Select((m, index) => new JobService.Request.CreateJobMilestoneRequest
                    {
                        Title = m.Title,
                        Description = m.Description,
                        Amount = m.Amount,
                        DueDays = m.DueDays,
                        AcceptanceCriteria = m.AcceptanceCriteria,
                        OrderIndex = index
                    }).ToList()
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

    private static void ApplyDraft(AIJobSuggestion suggestion, AIJobSuggestionDraft draft, bool updateRiskWarnings, IReadOnlyDictionary<string, decimal> ratesToAicoin)
    {
        var (currency, budgetMin, budgetMax, milestones) = CurrencyConverter.ConvertToAicoin(
            draft.Currency, draft.SuggestedBudgetMin, draft.SuggestedBudgetMax, draft.SuggestedMilestones, ratesToAicoin);

        suggestion.SuggestedTitle = NormalizeRequiredLimited(draft.SuggestedTitle, "New Job from AI", 255);
        suggestion.SuggestedDescription = draft.SuggestedDescription;
        suggestion.SuggestedBusinessDomain = NormalizeLimited(draft.BusinessDomain, 255);
        suggestion.SuggestedExpectedOutcome = NormalizeLimited(draft.ExpectedOutcome, 1000);
        suggestion.SuggestedBudgetType = draft.BudgetType;
        suggestion.Currency = currency;
        suggestion.SuggestedBudgetMin = budgetMin;
        suggestion.SuggestedBudgetMax = budgetMax;
        suggestion.SuggestedTimelineDays = draft.SuggestedTimelineDays;
        suggestion.SuggestedExperienceLevel = draft.ExperienceLevel;
        suggestion.SuggestedSkillsJson = SerializeList(draft.SuggestedSkills);
        suggestion.SuggestedMilestonesJson = SerializeList(NormalizeMilestones(milestones));
        suggestion.ClarifyingQuestionsJson = SerializeList(draft.ClarifyingQuestions);
        suggestion.ClarifyingAnswersJson = SerializeList(draft.ClarifyingAnswers);
        suggestion.AIModel = draft.AIModel;
        if (updateRiskWarnings)
        {
            suggestion.RiskWarningsJson = SerializeList(draft.RiskWarnings);
        }
    }

    private async Task<string> GetCategoriesContextAsync(CancellationToken cancellationToken)
    {
        var data = await _categoryService.GetCachedCategoryDictionaryAsync(cancellationToken);
        var categories = data.Values.ToList();
        return JsonSerializer.Serialize(categories, _jsonSerializerOptions);
    }

    private async Task<Guid?> ValidateAndNormalizeCategoryNameAsync(string? categoryName, string operation, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(categoryName)) return null;
        var normalizedInput = categoryName.Trim();
        var data = await _categoryService.GetCachedCategoryDictionaryAsync(cancellationToken);
        var match = data.FirstOrDefault(x => string.Equals(x.Value?.Trim(), normalizedInput, StringComparison.OrdinalIgnoreCase));
        
        if (match.Key != Guid.Empty)
        {
            return match.Key;
        }

        _logger.LogWarning("AI returned an invalid CategoryName '{CategoryName}' during {Operation} operation. It could not be mapped to any existing category.", categoryName, operation);
        return null;
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
            CategoryId = s.SuggestedCategoryId,
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
        return JsonSerializer.Serialize(values, _jsonSerializerOptions);
    }

    private static List<T> DeserializeList<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<T>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<T>>(json, _jsonSerializerOptions) ?? new List<T>();
        }
        catch (JsonException)
        {
            return new List<T>();
        }
    }

    private static void ValidateSuggestionShape(AIJobSuggestion suggestion)
    {
        if (string.IsNullOrWhiteSpace(suggestion.SuggestedTitle))
        {
            throw new ValidationException("SuggestedTitle is required.");
        }

        ValidateBudgetAndTimeline(suggestion.SuggestedBudgetMin, suggestion.SuggestedBudgetMax, suggestion.SuggestedTimelineDays);
        var milestones = NormalizeMilestones(DeserializeList<Response.SuggestedMilestone>(suggestion.SuggestedMilestonesJson));
        suggestion.SuggestedMilestonesJson = SerializeList(milestones);
    }

    private static void ValidateBudgetAndTimeline(decimal? budgetMin, decimal? budgetMax, int? timelineDays)
    {
        if (budgetMin.HasValue && budgetMin.Value <= 0)
        {
            throw new ValidationException("SuggestedBudgetMin must be greater than 0.");
        }

        if (budgetMax.HasValue && budgetMax.Value <= 0)
        {
            throw new ValidationException("SuggestedBudgetMax must be greater than 0.");
        }

        if (budgetMin.HasValue && budgetMax.HasValue && budgetMin.Value > budgetMax.Value)
        {
            throw new ValidationException("SuggestedBudgetMin must be less than or equal to SuggestedBudgetMax.");
        }

        if (timelineDays.HasValue && (timelineDays.Value < 1 || timelineDays.Value > 3650))
        {
            throw new ValidationException("SuggestedTimelineDays must be between 1 and 3650.");
        }
    }

    private static List<Response.SuggestedMilestone> NormalizeMilestones(IEnumerable<Response.SuggestedMilestone>? milestones)
    {
        return (milestones ?? Enumerable.Empty<Response.SuggestedMilestone>())
            .Select((milestone, index) => new Response.SuggestedMilestone
            {
                Title = NormalizeRequiredLimited(milestone.Title, $"Milestone {index + 1}", 255),
                Description = NormalizeLimited(milestone.Description, 2000),
                Amount = Math.Max(milestone.Amount, 1),
                DueDays = Math.Clamp(milestone.DueDays, 1, 3650),
                AcceptanceCriteria = NormalizeLimited(milestone.AcceptanceCriteria, 2000)
            })
            .ToList();
    }

    private static List<Guid> NormalizeGuidList(IEnumerable<Guid>? values)
    {
        return (values ?? Enumerable.Empty<Guid>())
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
    }

}
