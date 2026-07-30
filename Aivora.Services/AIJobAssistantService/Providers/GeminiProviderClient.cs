using System.Text;
using System.Text.Json;
using Aivora.Services.Exceptions;
using Aivora.Services.Options;
using Microsoft.Extensions.Options;

namespace Aivora.Services.AIJobAssistantService.Providers;

public class GeminiProviderClient
{
    private readonly HttpClient _httpClient;
    private readonly AIProviderOptions _options;

    public GeminiProviderClient(HttpClient httpClient, IOptions<AIProviderOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public bool HasApiKey => !string.IsNullOrWhiteSpace(_options.ApiKey);

    public Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
    {
        return GenerateAsync(prompt, Array.Empty<(string MimeType, byte[] Data)>(), cancellationToken);
    }

    public async Task<string> GenerateAsync(
        string prompt,
        IReadOnlyList<(string MimeType, byte[] Data)> attachments,
        CancellationToken cancellationToken = default,
        double? temperature = null,
        object? responseSchema = null)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new ValidationException("AI provider API key is missing.");
        }

        var baseUrl = string.IsNullOrWhiteSpace(_options.BaseUrl)
            ? "https://generativelanguage.googleapis.com"
            : _options.BaseUrl.TrimEnd('/');
        var model = string.IsNullOrWhiteSpace(_options.Model) ? "gemini-2.5-flash" : _options.Model.Trim();
        var requestUri = $"{baseUrl}/v1beta/models/{Uri.EscapeDataString(model)}:generateContent";

        var requestParts = new List<object> { new { text = prompt } };
        foreach (var attachment in attachments)
        {
            requestParts.Add(new
            {
                inline_data = new
                {
                    mime_type = attachment.MimeType,
                    data = Convert.ToBase64String(attachment.Data)
                }
            });
        }

        // Default to temperature 0: an AI-refinement JSON response feeding a diff/apply pipeline
        // needs to be as reproducible as possible, not creatively varied.
        var generationConfig = new Dictionary<string, object>
        {
            ["responseMimeType"] = "application/json",
            ["temperature"] = temperature ?? 0
        };
        if (responseSchema is not null)
        {
            generationConfig["responseSchema"] = responseSchema;
        }

        var payload = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = requestParts.ToArray()
                }
            },
            generationConfig
        };

        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = content
        };
        request.Headers.TryAddWithoutValidation("x-goog-api-key", _options.ApiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new ValidationException($"AI provider request failed with HTTP {(int)response.StatusCode}.");
        }

        using var document = JsonDocument.Parse(responseText);
        var root = document.RootElement;
        if (!root.TryGetProperty("candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array)
        {
            throw new ValidationException("AI provider response did not contain candidates.");
        }

        foreach (var candidate in candidates.EnumerateArray())
        {
            if (!candidate.TryGetProperty("content", out var contentElement)
                || !contentElement.TryGetProperty("parts", out var parts)
                || parts.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var textElement) && textElement.ValueKind == JsonValueKind.String)
                {
                    var text = textElement.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text;
                    }
                }
            }
        }

        throw new ValidationException("AI provider response did not include text content.");
    }
}
