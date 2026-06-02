namespace Aivora.Services.AIJobAssistantService.Parsing;

public class AIServiceDescriptionParser
{
    private static readonly string[] PackageNames = { "Basic", "Standard", "Premium" };

    public AIServiceDescriptionDraft Parse(string providerText, Request.GenerateServiceDescriptionRequest request)
    {
        using var document = AIJsonParser.ParseObject(providerText);
        var root = document.RootElement;

        return new AIServiceDescriptionDraft
        {
            SuggestedTitle = AIJsonParser.GetString(root, "suggestedTitle") ?? BuildFallbackTitle(request),
            SuggestedDescription = AIJsonParser.GetString(root, "suggestedDescription") ?? BuildFallbackDescription(request),
            Packages = ParsePackages(root, request),
            Faqs = ParseFaqs(root)
        };
    }

    private static List<Response.ServicePackageResponse> ParsePackages(System.Text.Json.JsonElement root, Request.GenerateServiceDescriptionRequest request)
    {
        var packages = new List<Response.ServicePackageResponse>();
        if (AIJsonParser.TryGetProperty(root, "packages", out var packagesElement)
            && packagesElement.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var item in packagesElement.EnumerateArray())
            {
                if (item.ValueKind != System.Text.Json.JsonValueKind.Object)
                {
                    continue;
                }

                packages.Add(new Response.ServicePackageResponse
                {
                    Name = AIJsonParser.GetString(item, "name") ?? string.Empty,
                    Title = AIJsonParser.GetString(item, "title"),
                    Description = AIJsonParser.GetString(item, "description") ?? "Service package",
                    Price = AIJsonParser.GetDecimal(item, "price") ?? request.PriceFrom,
                    DeliveryDays = AIJsonParser.GetInt(item, "deliveryDays") ?? request.DeliveryDays,
                    Features = AIJsonParser.ReadStringList(item, "features")
                });
            }
        }

        while (packages.Count < 3)
        {
            packages.Add(BuildFallbackPackage(packages.Count, request));
        }

        packages = packages.Take(3).ToList();
        for (var i = 0; i < PackageNames.Length; i++)
        {
            packages[i].Name = PackageNames[i];
            packages[i].Title ??= $"{PackageNames[i]} Package";
            if (packages[i].Features.Count == 0)
            {
                packages[i].Features.Add($"Core {request.Skills.FirstOrDefault() ?? "service"} delivery");
            }
        }

        return packages;
    }

    private static List<Response.ServiceFaqResponse> ParseFaqs(System.Text.Json.JsonElement root)
    {
        var faqs = new List<Response.ServiceFaqResponse>();
        if (AIJsonParser.TryGetProperty(root, "faqs", out var faqsElement)
            && faqsElement.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var item in faqsElement.EnumerateArray())
            {
                if (item.ValueKind != System.Text.Json.JsonValueKind.Object)
                {
                    continue;
                }

                var question = AIJsonParser.GetString(item, "question");
                var answer = AIJsonParser.GetString(item, "answer");
                if (!string.IsNullOrWhiteSpace(question) && !string.IsNullOrWhiteSpace(answer))
                {
                    faqs.Add(new Response.ServiceFaqResponse { Question = question, Answer = answer });
                }
            }
        }

        if (faqs.Count == 0)
        {
            faqs.Add(new Response.ServiceFaqResponse
            {
                Question = "What information is needed to get started?",
                Answer = "Please provide requirements, examples, preferred technology, and any constraints before kickoff."
            });
        }

        return faqs;
    }

    private static Response.ServicePackageResponse BuildFallbackPackage(int index, Request.GenerateServiceDescriptionRequest request)
    {
        var name = PackageNames[index];
        var priceMultiplier = index switch { 0 => 1, 1 => 2, _ => 4 };
        var deliveryDays = index switch
        {
            0 => Math.Max(1, (int)Math.Round(request.DeliveryDays * 0.5m)),
            1 => request.DeliveryDays,
            _ => Math.Max(2, (int)Math.Round(request.DeliveryDays * 1.5m))
        };

        return new Response.ServicePackageResponse
        {
            Name = name,
            Title = $"{name} Package",
            Description = $"{name} tier for {request.Skills.FirstOrDefault() ?? "service"} delivery.",
            Price = request.PriceFrom * priceMultiplier,
            DeliveryDays = deliveryDays,
            Features = new List<string> { "Requirements review", "Implementation", "Delivery support" }
        };
    }

    private static string BuildFallbackTitle(Request.GenerateServiceDescriptionRequest request)
    {
        return $"Professional {string.Join(" & ", request.Skills.Take(2))} Service";
    }

    private static string BuildFallbackDescription(Request.GenerateServiceDescriptionRequest request)
    {
        return $"Professional delivery for {string.Join(", ", request.Skills)}. {request.RawInput}";
    }
}
