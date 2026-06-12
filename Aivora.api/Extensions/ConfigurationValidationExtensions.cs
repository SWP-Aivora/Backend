using Aivora.Services.Options;

namespace Aivora.api.Extensions;

public static class ConfigurationValidationExtensions
{
    public static void ValidateRequiredConfiguration(this IConfiguration configuration)
    {
        var requiredConfig = new Dictionary<string, string>
        {
            ["ConnectionStrings:DefaultConnection"] = "ConnectionStrings__DefaultConnection",
            ["JwtSettings:Secret"] = "JwtSettings__Secret",
            ["JwtSettings:Issuer"] = "JwtSettings__Issuer",
            ["JwtSettings:Audience"] = "JwtSettings__Audience",
            ["JwtSettings:ExpiryInMinutes"] = "JwtSettings__ExpiryInMinutes",
            ["CloudinaryOptions:CloudName"] = "CloudinaryOptions__CloudName",
            ["CloudinaryOptions:ApiKey"] = "CloudinaryOptions__ApiKey",
            ["CloudinaryOptions:ApiSecret"] = "CloudinaryOptions__ApiSecret",
        };

        var errors = new List<string>();
        foreach (var (configKey, envVar) in requiredConfig)
        {
            var value = configuration[configKey];
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"{configKey} is missing; set {envVar}.");
            }
            else if (HasPlaceholder(value))
            {
                errors.Add($"{configKey} is a placeholder; set {envVar}.");
            }
        }

        var jwtSecret = configuration["JwtSettings:Secret"];
        if (!string.IsNullOrWhiteSpace(jwtSecret) && jwtSecret.Length < 32)
        {
            errors.Add("JwtSettings:Secret must be at least 32 characters.");
        }

        if (!int.TryParse(configuration["JwtSettings:ExpiryInMinutes"], out var expiryInMinutes) || expiryInMinutes <= 0)
        {
            errors.Add("JwtSettings:ExpiryInMinutes must be a positive integer.");
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException("Invalid configuration:\n" + string.Join("\n", errors.Select(error => "- " + error)));
        }
    }

    public static void ValidateAIProviderConfiguration(this IConfiguration configuration, IWebHostEnvironment environment)
    {
        var options = configuration.GetSection("AIProvider").Get<AIProviderOptions>() ?? new AIProviderOptions();
        var provider = options.Provider?.Trim();
        var providerIsGemini = string.Equals(provider, "Gemini", StringComparison.OrdinalIgnoreCase);
        var providerIsMock = string.Equals(provider, "Mock", StringComparison.OrdinalIgnoreCase);
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(provider) || (!providerIsGemini && !providerIsMock))
        {
            errors.Add("AIProvider:Provider must be either Gemini or Mock.");
        }

        if (environment.IsProduction())
        {
            if (!providerIsGemini)
            {
                errors.Add("AIProvider:Provider must be Gemini in Production.");
            }

            if (string.IsNullOrWhiteSpace(options.ApiKey) || HasPlaceholder(options.ApiKey))
            {
                errors.Add("AIProvider:ApiKey is required in Production; set AIProvider__ApiKey.");
            }

            if (options.EnableFallback)
            {
                errors.Add("AIProvider:EnableFallback must be false in Production.");
            }
        }
        else if (providerIsGemini && string.IsNullOrWhiteSpace(options.ApiKey) && !options.EnableFallback)
        {
            errors.Add("AIProvider:ApiKey is required when Gemini is selected and fallback is disabled.");
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException("Invalid AI provider configuration:\n" + string.Join("\n", errors.Select(error => "- " + error)));
        }
    }

    internal static bool HasPlaceholder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return value.Contains("__SET", StringComparison.OrdinalIgnoreCase)
            || value.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase)
            || value.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase)
            || value.Contains("YOUR_", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool UseGemini(AIProviderOptions options)
    {
        return string.Equals(options.Provider, "Gemini", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(options.ApiKey)
            && !HasPlaceholder(options.ApiKey);
    }
}
