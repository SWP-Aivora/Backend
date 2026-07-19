namespace Aivora.Services.AIJobAssistantService;

public class AIServiceDescriptionDraft
{
    public string SuggestedTitle { get; set; } = null!;
    public string SuggestedDescription { get; set; } = null!;
    public List<Response.ServicePackageResponse> Packages { get; set; } = new();
    public List<Response.ServiceFaqResponse> Faqs { get; set; } = new();
    public string AIModel { get; set; } = "Aivora-Mock";
    public string Provider { get; set; } = "mock";
}
