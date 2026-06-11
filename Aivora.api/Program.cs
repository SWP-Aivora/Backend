using System.Threading.RateLimiting;
using Aivora.api.Extensions;
using Aivora.api.Middlewares;
using Aivora.Repositories.Data;
using Aivora.Repositories.Data.Interceptors;
using Aivora.Services.JwtService;
using Aivora.Services.Models;
using Aivora.Services.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<AuditableEntityInterceptor>();

ValidateRequiredConfiguration(builder.Configuration);
ValidateAIProviderConfiguration(builder.Configuration, builder.Environment);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;

builder.Services.AddDbContext<AivoraDbContext>((sp, options) =>
{
    var interceptor = sp.GetRequiredService<AuditableEntityInterceptor>();
    options.UseNpgsql(connectionString)
        .AddInterceptors(interceptor);
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin",
        policy =>
        {
            policy.WithOrigins("http://localhost:5173", "https://aivora-pi.vercel.app")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
});

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("JwtSettings"));
builder.Services.Configure<CloudinaryOptions>(builder.Configuration.GetSection("CloudinaryOptions"));
builder.Services.Configure<AIProviderOptions>(builder.Configuration.GetSection("AIProvider"));
builder.Services.Configure<RateLimitOptions>(builder.Configuration.GetSection(RateLimitOptions.SectionName));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        var retryAfterSeconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter)
            ? (int)Math.Ceiling(retryAfter.TotalSeconds)
            : (int?)null;
        var message = retryAfterSeconds.HasValue
            ? $"Too many requests. Please try again after {retryAfterSeconds.Value} second(s)."
            : "Too many requests. Please try again later.";

        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(
            ApiResponseFactory.ErrorResponse(
                message,
                new { code = "rate_limit_exceeded", retryAfterSeconds },
                context.HttpContext.TraceIdentifier),
            token);
    };

    var rateLimitOptions = builder.Configuration.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>() ?? new RateLimitOptions();

    options.AddPolicy("Strict", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? context.Request.Headers.Host.ToString(),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimitOptions.Strict.PermitLimit,
                Window = TimeSpan.FromMinutes(rateLimitOptions.Strict.WindowInMinutes)
            }));

    options.AddPolicy("AI", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? context.Request.Headers.Host.ToString(),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimitOptions.AI.PermitLimit,
                Window = TimeSpan.FromMinutes(rateLimitOptions.AI.WindowInMinutes)
            }));

    options.AddPolicy("General", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? context.Request.Headers.Host.ToString(),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimitOptions.General.PermitLimit,
                Window = TimeSpan.FromMinutes(rateLimitOptions.General.WindowInMinutes)
            }));
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddJwtServices(builder.Configuration);

builder.Services.AddScoped<IJwtService, Service>();
builder.Services.AddScoped<Aivora.Services.MediaService.IService, Aivora.Services.MediaService.Service>();
builder.Services.AddScoped<Aivora.Services.IdentityService.IService, Aivora.Services.IdentityService.Service>();
builder.Services.AddScoped<Aivora.Services.CategoryService.IService, Aivora.Services.CategoryService.Service>();
builder.Services.AddScoped<Aivora.Services.SkillService.IService, Aivora.Services.SkillService.Service>();
builder.Services.AddScoped<Aivora.Services.ProfileService.IService, Aivora.Services.ProfileService.Service>();
builder.Services.AddScoped<Aivora.Services.JobService.IService, Aivora.Services.JobService.Service>();
builder.Services.AddScoped<Aivora.Services.ProposalService.IService, Aivora.Services.ProposalService.Service>();
builder.Services.AddScoped<Aivora.Services.HiringService.IHiringService, Aivora.Services.HiringService.HiringService>();
builder.Services.AddScoped<Aivora.Services.AIJobAssistantService.Prompting.AIJobSuggestionPromptBuilder>();
builder.Services.AddScoped<Aivora.Services.AIJobAssistantService.Prompting.AIJobRefinementPromptBuilder>();
builder.Services.AddScoped<Aivora.Services.AIJobAssistantService.Prompting.AIServiceDescriptionPromptBuilder>();
builder.Services.AddScoped<Aivora.Services.AIJobAssistantService.Parsing.AIJobSuggestionParser>();
builder.Services.AddScoped<Aivora.Services.AIJobAssistantService.Parsing.AIJobRefinementParser>();
builder.Services.AddScoped<Aivora.Services.AIJobAssistantService.Parsing.AIServiceDescriptionParser>();
builder.Services.AddScoped<Aivora.Services.AIJobAssistantService.Providers.MockAIJobSuggestionProvider>();
builder.Services.AddScoped<Aivora.Services.AIJobAssistantService.Providers.MockAIJobRefinementProvider>();
builder.Services.AddScoped<Aivora.Services.AIJobAssistantService.Providers.MockAIServiceDescriptionProvider>();
builder.Services.AddHttpClient<Aivora.Services.AIJobAssistantService.Providers.GeminiProviderClient>();
builder.Services.AddScoped<Aivora.Services.AIJobAssistantService.Providers.GeminiAIJobSuggestionProvider>();
builder.Services.AddScoped<Aivora.Services.AIJobAssistantService.Providers.GeminiAIJobRefinementProvider>();
builder.Services.AddScoped<Aivora.Services.AIJobAssistantService.Providers.GeminiAIServiceDescriptionProvider>();
builder.Services.AddScoped<Aivora.Services.AIJobAssistantService.IAIJobSuggestionProvider>(sp =>
{
    var options = sp.GetRequiredService<IOptions<AIProviderOptions>>().Value;
    if (UseGemini(options))
    {
        return sp.GetRequiredService<Aivora.Services.AIJobAssistantService.Providers.GeminiAIJobSuggestionProvider>();
    }

    var environment = sp.GetRequiredService<IWebHostEnvironment>();
    if (environment.IsProduction())
    {
        throw new InvalidOperationException("AIProvider__Provider=Gemini and AIProvider__ApiKey are required in Production.");
    }

    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("AIProvider");
    logger.LogWarning("Using mock AI job suggestion provider because provider {Provider} is not fully configured.", options.Provider);
    return sp.GetRequiredService<Aivora.Services.AIJobAssistantService.Providers.MockAIJobSuggestionProvider>();
});
builder.Services.AddScoped<Aivora.Services.AIJobAssistantService.IAIJobRefinementProvider>(sp =>
{
    var options = sp.GetRequiredService<IOptions<AIProviderOptions>>().Value;
    if (UseGemini(options))
    {
        return sp.GetRequiredService<Aivora.Services.AIJobAssistantService.Providers.GeminiAIJobRefinementProvider>();
    }

    var environment = sp.GetRequiredService<IWebHostEnvironment>();
    if (environment.IsProduction())
    {
        throw new InvalidOperationException("AIProvider__Provider=Gemini and AIProvider__ApiKey are required in Production.");
    }

    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("AIProvider");
    logger.LogWarning("Using mock AI job refinement provider because provider {Provider} is not fully configured.", options.Provider);
    return sp.GetRequiredService<Aivora.Services.AIJobAssistantService.Providers.MockAIJobRefinementProvider>();
});
builder.Services.AddScoped<Aivora.Services.AIJobAssistantService.IAIServiceDescriptionProvider>(sp =>
{
    var options = sp.GetRequiredService<IOptions<AIProviderOptions>>().Value;
    if (UseGemini(options))
    {
        return sp.GetRequiredService<Aivora.Services.AIJobAssistantService.Providers.GeminiAIServiceDescriptionProvider>();
    }

    var environment = sp.GetRequiredService<IWebHostEnvironment>();
    if (environment.IsProduction())
    {
        throw new InvalidOperationException("AIProvider__Provider=Gemini and AIProvider__ApiKey are required in Production.");
    }

    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("AIProvider");
    logger.LogWarning("Using mock AI service description provider because provider {Provider} is not fully configured.", options.Provider);
    return sp.GetRequiredService<Aivora.Services.AIJobAssistantService.Providers.MockAIServiceDescriptionProvider>();
});
builder.Services.AddScoped<Aivora.Services.AIJobAssistantService.IService, Aivora.Services.AIJobAssistantService.Service>();
builder.Services.AddScoped<Aivora.Services.RecommendationService.IService, Aivora.Services.RecommendationService.Service>();
builder.Services.AddScoped<Aivora.Services.ProjectService.IService, Aivora.Services.ProjectService.Service>();
builder.Services.AddScoped<Aivora.Services.MilestoneService.IService, Aivora.Services.MilestoneService.Service>();
builder.Services.AddScoped<Aivora.Services.DeliverableService.IService, Aivora.Services.DeliverableService.Service>();
builder.Services.AddScoped<Aivora.Services.WalletService.IService, Aivora.Services.WalletService.Service>();
builder.Services.AddScoped<Aivora.Services.ReviewService.IService, Aivora.Services.ReviewService.Service>();
builder.Services.AddScoped<Aivora.Services.MessageService.IService, Aivora.Services.MessageService.Service>();
builder.Services.AddScoped<Aivora.Services.DisputeService.IService, Aivora.Services.DisputeService.Service>();
builder.Services.AddScoped<Aivora.Services.NotificationService.IService, Aivora.Services.NotificationService.Service>();
builder.Services.AddScoped<Aivora.Services.AdminService.IAdminService, Aivora.Services.AdminService.AdminService>();
builder.Services.AddScoped<Aivora.Services.Treasury.ITreasury, Aivora.Services.Treasury.Treasury>();

builder.Services.AddSignalR();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .ToDictionary(
                entry => entry.Key,
                entry => entry.Value!.Errors
                    .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? "The input was invalid."
                        : error.ErrorMessage)
                    .ToArray());

        return new BadRequestObjectResult(ApiResponseFactory.ErrorResponse(
            "Validation failed.",
            new { code = "validation_error", fields = errors },
            context.HttpContext.TraceIdentifier));
    };
});

builder.Services.AddOpenApiServices();

var app = builder.Build();

app.UseMiddleware<ExceptionMiddleware>();

app.UseOpenApiUI();

app.UseHttpsRedirection();

app.UseCors("AllowSpecificOrigin");

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<Aivora.api.Hubs.ChatHub>("/api/v1/chat");

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var context = services.GetRequiredService<AivoraDbContext>();
    var forceReset = app.Configuration.GetValue<bool>("SeedForceReset");

    if (forceReset && !app.Environment.IsDevelopment())
    {
        throw new InvalidOperationException("SeedForceReset can only be used in Development.");
    }

    try
    {
        await context.Database.MigrateAsync();

        if (forceReset)
        {
            logger.LogWarning("SeedForceReset=true; seed-managed data will be reset in Development.");
        }

        await Aivora.Repositories.Data.SeedData.Initialize(context, forceReset);
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Database migration or seed failed. Startup aborted without deleting the configured database.");
        throw;
    }
}

app.Run();

static void ValidateRequiredConfiguration(IConfiguration configuration)
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

static void ValidateAIProviderConfiguration(IConfiguration configuration, IWebHostEnvironment environment)
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

static bool HasPlaceholder(string? value)
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

static bool UseGemini(AIProviderOptions options)
{
    return string.Equals(options.Provider, "Gemini", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(options.ApiKey)
        && !HasPlaceholder(options.ApiKey);
}
