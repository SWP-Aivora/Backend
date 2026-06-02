namespace Aivora.Services.AIJobAssistantService.Providers;

public class MockAIServiceDescriptionProvider : IAIServiceDescriptionProvider
{
    public Task<AIServiceDescriptionDraft> GenerateServiceDescriptionAsync(Request.GenerateServiceDescriptionRequest request, CancellationToken cancellationToken = default)
    {
        var primarySkill = request.Skills.FirstOrDefault() ?? "AI";
        var title = request.Language.Equals("en", StringComparison.OrdinalIgnoreCase)
            ? $"Professional Service: Build {primarySkill} Solutions"
            : $"Dich vu chuyen nghiep: Thiet ke va lap trinh {primarySkill}";

        var draft = new AIServiceDescriptionDraft
        {
            SuggestedTitle = title,
            SuggestedDescription = request.Language.Equals("en", StringComparison.OrdinalIgnoreCase)
                ? $"I deliver high-quality services for {string.Join(", ", request.Skills)}. {request.RawInput}"
                : $"Toi cung cap dich vu chat luong cao ve {string.Join(", ", request.Skills)}. {request.RawInput}",
            Packages = new List<Response.ServicePackageResponse>
            {
                new()
                {
                    Name = "Basic",
                    Title = "Basic Package",
                    Description = "Core setup and first delivery.",
                    Price = request.PriceFrom,
                    DeliveryDays = Math.Max(1, (int)Math.Round(request.DeliveryDays * 0.5m)),
                    Features = new List<string> { $"Basic {primarySkill} setup", "Setup documentation", "3 days support" }
                },
                new()
                {
                    Name = "Standard",
                    Title = "Standard Full Solution",
                    Description = "Complete delivery for common production needs.",
                    Price = request.PriceFrom * 2,
                    DeliveryDays = request.DeliveryDays,
                    Features = new List<string> { $"Implementation with {string.Join(", ", request.Skills)}", "Testing", "API and database integration", "7 days support" }
                },
                new()
                {
                    Name = "Premium",
                    Title = "Premium Enterprise Solution",
                    Description = "Advanced delivery with scalability and extended support.",
                    Price = request.PriceFrom * 4,
                    DeliveryDays = Math.Max(2, (int)Math.Round(request.DeliveryDays * 1.5m)),
                    Features = new List<string> { "All Standard features", "Scalable architecture", "30 days warranty", "Handoff call" }
                }
            },
            Faqs = new List<Response.ServiceFaqResponse>
            {
                new()
                {
                    Question = "What do I need to prepare to get started?",
                    Answer = "Please prepare your requirements, examples, preferred stack, and any constraints."
                },
                new()
                {
                    Question = "Is support included after delivery?",
                    Answer = "Yes, support is included based on the selected package tier."
                }
            },
            AIModel = "Aivora-Mock"
        };

        return Task.FromResult(draft);
    }
}
