namespace Aivora.Services.RecommendationService;

public class ExpertRecommendationDraft
{
    public List<RankedExpert> Ranked { get; set; } = new();
    public string AIModel { get; set; } = "Aivora-Mock";
}

// Thu tu trong list = thu tu AI xep hang, tot nhat truoc.
public class RankedExpert
{
    public Guid ExpertId { get; set; }
    public string Reasoning { get; set; } = null!;
}
