using System.Globalization;
using System.Text.Json;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Aivora.Services.ExpertVerificationService.Providers;

namespace Aivora.Services.ExpertVerificationService.Parsing;

public class AIExpertVerificationParser
{
    public AIVerificationResult Parse(string providerText)
    {
        if (string.IsNullOrWhiteSpace(providerText))
        {
            throw new ValidationException("AI provider returned an empty response.");
        }

        var trimmed = providerText.Trim();
        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            throw new ValidationException("AI provider response did not contain a JSON object.");
        }

        using var document = JsonDocument.Parse(trimmed[start..(end + 1)]);
        var root = document.RootElement;

        var outcome = ParseOutcome(GetString(root, "outcome"));
        var confidence = GetDecimal(root, "confidenceScore") ?? 0;
        var reasoning = GetString(root, "reasoning");

        if (string.IsNullOrWhiteSpace(reasoning))
        {
            throw new ValidationException("AI provider response did not include reasoning.");
        }

        return new AIVerificationResult
        {
            Outcome = outcome,
            ConfidenceScore = Math.Clamp(confidence, 0, 100),
            Reasoning = reasoning
        };
    }

    private static ExpertVerificationStatus ParseOutcome(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException("AI provider response did not include an outcome.");
        }

        return value.Trim().ToUpperInvariant() switch
        {
            "APPROVED" => ExpertVerificationStatus.APPROVED,
            "REJECTED" => ExpertVerificationStatus.REJECTED,
            "NEEDS_REVIEW" => ExpertVerificationStatus.NEEDS_REVIEW,
            _ => throw new ValidationException($"AI provider returned an unrecognized outcome: {value}")
        };
    }

    private static string? GetString(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var value = property.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static decimal? GetDecimal(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out var number))
        {
            return number;
        }

        if (property.ValueKind == JsonValueKind.String
            && decimal.TryParse(property.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement property)
    {
        foreach (var jsonProperty in element.EnumerateObject())
        {
            if (string.Equals(jsonProperty.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                property = jsonProperty.Value;
                return true;
            }
        }

        property = default;
        return false;
    }
}
