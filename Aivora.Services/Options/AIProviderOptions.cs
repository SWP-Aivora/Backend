namespace Aivora.Services.Options;

public class AIProviderOptions
{
    public string Provider { get; set; } = "Gemini";
    public string? ApiKey { get; set; }
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com";
    public string Model { get; set; } = "gemini-2.5-flash";
    public bool EnableFallback { get; set; } = true;
}
