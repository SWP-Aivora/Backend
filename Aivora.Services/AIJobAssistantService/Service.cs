using System.Text.Json;
using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Aivora.Services.JobService;
using Microsoft.EntityFrameworkCore;

namespace Aivora.Services.AIJobAssistantService;

public class Service : IService
{
    private readonly AivoraDbContext _dbContext;
    private readonly JobService.IService _jobService;

    public Service(AivoraDbContext dbContext, JobService.IService jobService)
    {
        _dbContext = dbContext;
        _jobService = jobService;
    }

    public async Task<Response.SuggestionResponse> GenerateSuggestionAsync(Guid clientId, Request.GenerateSuggestionRequest request)
    {
        // MOCK AI LOGIC - Later replace with OpenAI/Gemini call
        var suggestion = new AIJobSuggestion
        {
            ClientId = clientId,
            RawInput = request.RawInput,
            SuggestedTitle = $"AI Enhanced: {request.RawInput.Substring(0, Math.Min(30, request.RawInput.Length))}...",
            SuggestedDescription = $"This is an AI enhanced description for: {request.RawInput}",
            SuggestedBudgetMin = request.BudgetMin ?? 500,
            SuggestedBudgetMax = request.BudgetMax ?? 1500,
            SuggestedTimelineDays = request.TimelineDays ?? 14,
            AIModel = "Aivora-Mock-GPT-4",
            Status = AIJobSuggestionStatus.GENERATED,
            SuggestedSkillsJson = JsonSerializer.Serialize(new List<string> { "AI Chatbot", "Prompt Engineering" }),
            SuggestedMilestonesJson = JsonSerializer.Serialize(new List<Response.SuggestedMilestone>
            {
                new Response.SuggestedMilestone { Title = "Initial Design", Amount = 200, DueDays = 3, Description = "Design the AI flow" },
                new Response.SuggestedMilestone { Title = "Implementation", Amount = 800, DueDays = 11, Description = "Full development" }
            }),
            ClarifyingQuestionsJson = JsonSerializer.Serialize(new List<string> { "Do you have specific API preferences?", "What is the expected load?" }),
            RiskWarningsJson = JsonSerializer.Serialize(new List<string> { "API costs may vary", "Complexity of UI might affect timeline" })
        };

        _dbContext.AIJobSuggestions.Add(suggestion);
        await _dbContext.SaveChangesAsync();

        return MapToResponse(suggestion);
    }

    public async Task<Response.AcceptResultResponse> AcceptSuggestionAsync(Guid clientId, Guid suggestionId, Request.AcceptSuggestionRequest request)
    {
        var suggestion = await _dbContext.AIJobSuggestions.FirstOrDefaultAsync(s => s.Id == suggestionId && s.ClientId == clientId);
        if (suggestion == null) throw new NotFoundException("AI Suggestion not found.");
        if (suggestion.Status != AIJobSuggestionStatus.GENERATED) throw new ValidationException("Suggestion is already processed.");

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            // Create a draft job
            var createJobRequest = new JobService.Request.CreateJobRequest
            {
                Title = suggestion.SuggestedTitle ?? "New Job from AI",
                OriginalDescription = suggestion.RawInput,
                FinalDescription = suggestion.SuggestedDescription,
                CategoryId = request.CategoryId ?? Guid.Empty, // User should provide or AI suggest (future)
                BudgetType = BudgetType.FIXED,
                BudgetMin = suggestion.SuggestedBudgetMin,
                BudgetMax = suggestion.SuggestedBudgetMax,
                TimelineDays = suggestion.SuggestedTimelineDays,
                Visibility = JobVisibility.PUBLIC,
                SkillIds = request.SelectedSkillIds ?? new List<Guid>()
            };

            var jobResponse = await _jobService.CreateJobAsync(clientId, createJobRequest);

            suggestion.Status = AIJobSuggestionStatus.ACCEPTED;
            suggestion.JobId = jobResponse.Id;

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            return new Response.AcceptResultResponse { Job = jobResponse };
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> RejectSuggestionAsync(Guid clientId, Guid suggestionId, Request.RejectSuggestionRequest request)
    {
        var suggestion = await _dbContext.AIJobSuggestions.FirstOrDefaultAsync(s => s.Id == suggestionId && s.ClientId == clientId);
        if (suggestion == null) throw new NotFoundException("AI Suggestion not found.");
        
        suggestion.Status = AIJobSuggestionStatus.REJECTED;
        // Optionally store reason if we add a field to entity later
        
        await _dbContext.SaveChangesAsync();
        return true;
    }

    private static Response.SuggestionResponse MapToResponse(AIJobSuggestion s)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return new Response.SuggestionResponse
        {
            Id = s.Id,
            JobId = s.JobId,
            ClientId = s.ClientId,
            RawInput = s.RawInput,
            SuggestedTitle = s.SuggestedTitle,
            SuggestedDescription = s.SuggestedDescription,
            SuggestedBudgetMin = s.SuggestedBudgetMin,
            SuggestedBudgetMax = s.SuggestedBudgetMax,
            SuggestedTimelineDays = s.SuggestedTimelineDays,
            SuggestedSkills = string.IsNullOrEmpty(s.SuggestedSkillsJson) ? new() : JsonSerializer.Deserialize<List<string>>(s.SuggestedSkillsJson, options)!,
            SuggestedMilestones = string.IsNullOrEmpty(s.SuggestedMilestonesJson) ? new() : JsonSerializer.Deserialize<List<Response.SuggestedMilestone>>(s.SuggestedMilestonesJson, options)!,
            ClarifyingQuestions = string.IsNullOrEmpty(s.ClarifyingQuestionsJson) ? new() : JsonSerializer.Deserialize<List<string>>(s.ClarifyingQuestionsJson, options)!,
            RiskWarnings = string.IsNullOrEmpty(s.RiskWarningsJson) ? new() : JsonSerializer.Deserialize<List<string>>(s.RiskWarningsJson, options)!,
            AIModel = s.AIModel,
            Status = s.Status.ToString(),
            CreatedAt = s.CreatedAt
        };
    }
}
