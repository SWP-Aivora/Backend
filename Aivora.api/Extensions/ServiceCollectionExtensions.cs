using System.Threading.RateLimiting;
using Aivora.Repositories.Data;
using Aivora.Repositories.Data.Interceptors;
using Aivora.Services.JwtService;
using Aivora.Services.Options;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Aivora.api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAivoraPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<AuditableEntityInterceptor>();

        var connectionString = configuration.GetConnectionString("DefaultConnection")!;
        services.AddDbContext<AivoraDbContext>((sp, options) =>
        {
            var interceptor = sp.GetRequiredService<AuditableEntityInterceptor>();
            options.UseNpgsql(connectionString)
                .AddInterceptors(interceptor);
        });

        return services;
    }

    public static IServiceCollection AddAivoraCors(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("AllowSpecificOrigin",
                policy =>
                {
                    var origins = configuration.GetSection("CorsSettings:Origins").Get<string[]>();

                    if (origins is not { Length: > 0 })
                    {
                        origins = ["http://localhost:5173"];
                    }

                    policy.WithOrigins(origins)
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials();
                });
        });

        return services;
    }

    public static IServiceCollection AddAivoraOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection("JwtSettings"));
        services.Configure<CloudinaryOptions>(configuration.GetSection("CloudinaryOptions"));
        services.Configure<AIProviderOptions>(configuration.GetSection("AIProvider"));
        services.Configure<RateLimitOptions>(configuration.GetSection(RateLimitOptions.SectionName));
        services.Configure<CommissionOptions>(configuration.GetSection("Commission"));
        services.AddOptions<RecommendationOptions>()
            .Bind(configuration.GetSection("Recommendation"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }

    public static IServiceCollection AddAivoraRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
                {
                    await context.HttpContext.Response.WriteAsJsonAsync(
                        new { message = $"Too many requests. Please try again after {retryAfter.TotalSeconds} second(s)." },
                        token);
                }
                else
                {
                    await context.HttpContext.Response.WriteAsJsonAsync(
                        new { message = "Too many requests. Please try again later." },
                        token);
                }
            };

            var rateLimitOptions = configuration.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>() ?? new RateLimitOptions();

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

        return services;
    }

    public static IServiceCollection AddAivoraApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddJwtServices(configuration);
        services.AddMemoryCache();

        services.AddScoped<IJwtService, Aivora.Services.JwtService.Service>();
        services.AddScoped<Aivora.Services.MediaService.IService, Aivora.Services.MediaService.Service>();
        services.AddScoped<Aivora.Services.IdentityService.IService, Aivora.Services.IdentityService.Service>();
        services.AddScoped<Aivora.Services.CategoryService.IService, Aivora.Services.CategoryService.Service>();
        services.AddScoped<Aivora.Services.SkillService.IService, Aivora.Services.SkillService.Service>();
        services.AddScoped<Aivora.Services.ProfileService.IService, Aivora.Services.ProfileService.Service>();
        services.AddScoped<Aivora.Services.JobService.IService, Aivora.Services.JobService.Service>();
        services.AddScoped<Aivora.Services.ProposalService.IService, Aivora.Services.ProposalService.Service>();
        services.AddScoped<Aivora.Services.HiringService.IHiringService, Aivora.Services.HiringService.HiringService>();
        services.AddScoped<Aivora.Services.AIJobAssistantService.Prompting.AIJobSuggestionPromptBuilder>();
        services.AddScoped<Aivora.Services.AIJobAssistantService.Prompting.AIJobRefinementPromptBuilder>();
        services.AddScoped<Aivora.Services.AIJobAssistantService.Prompting.AIServiceDescriptionPromptBuilder>();
        services.AddScoped<Aivora.Services.AIJobAssistantService.Parsing.AIJobSuggestionParser>();
        services.AddScoped<Aivora.Services.AIJobAssistantService.Parsing.AIJobRefinementParser>();
        services.AddScoped<Aivora.Services.AIJobAssistantService.Parsing.AIServiceDescriptionParser>();
        services.AddScoped<Aivora.Services.AIJobAssistantService.Providers.MockAIJobSuggestionProvider>();
        services.AddScoped<Aivora.Services.AIJobAssistantService.Providers.MockAIJobRefinementProvider>();
        services.AddScoped<Aivora.Services.AIJobAssistantService.Providers.MockAIServiceDescriptionProvider>();
        services.AddHttpClient<Aivora.Services.AIJobAssistantService.Providers.GeminiProviderClient>();
        services.AddScoped<Aivora.Services.AIJobAssistantService.Providers.GeminiAIJobSuggestionProvider>();
        services.AddScoped<Aivora.Services.AIJobAssistantService.Providers.GeminiAIJobRefinementProvider>();
        services.AddScoped<Aivora.Services.AIJobAssistantService.Providers.GeminiAIServiceDescriptionProvider>();
        services.AddScoped<Aivora.Services.AIJobAssistantService.IAIJobSuggestionProvider>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AIProviderOptions>>().Value;
            return string.Equals(options.Provider, "Gemini", StringComparison.OrdinalIgnoreCase)
                   && !string.IsNullOrWhiteSpace(options.ApiKey)
                ? sp.GetRequiredService<Aivora.Services.AIJobAssistantService.Providers.GeminiAIJobSuggestionProvider>()
                : sp.GetRequiredService<Aivora.Services.AIJobAssistantService.Providers.MockAIJobSuggestionProvider>();
        });
        services.AddScoped<Aivora.Services.AIJobAssistantService.IAIJobRefinementProvider>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AIProviderOptions>>().Value;
            return string.Equals(options.Provider, "Gemini", StringComparison.OrdinalIgnoreCase)
                   && !string.IsNullOrWhiteSpace(options.ApiKey)
                ? sp.GetRequiredService<Aivora.Services.AIJobAssistantService.Providers.GeminiAIJobRefinementProvider>()
                : sp.GetRequiredService<Aivora.Services.AIJobAssistantService.Providers.MockAIJobRefinementProvider>();
        });
        services.AddScoped<Aivora.Services.AIJobAssistantService.IAIServiceDescriptionProvider>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AIProviderOptions>>().Value;
            return string.Equals(options.Provider, "Gemini", StringComparison.OrdinalIgnoreCase)
                   && !string.IsNullOrWhiteSpace(options.ApiKey)
                ? sp.GetRequiredService<Aivora.Services.AIJobAssistantService.Providers.GeminiAIServiceDescriptionProvider>()
                : sp.GetRequiredService<Aivora.Services.AIJobAssistantService.Providers.MockAIServiceDescriptionProvider>();
        });
        services.AddScoped<Aivora.Services.AIJobAssistantService.IService, Aivora.Services.AIJobAssistantService.Service>();

        // AIJobRefinementService
        services.AddScoped<Aivora.Services.AIJobRefinementService.Prompting.AIJobRefinementPromptBuilder>();
        services.AddScoped<Aivora.Services.AIJobRefinementService.Parsing.AIJobRefinementParser>();
        services.AddScoped<Aivora.Services.AIJobRefinementService.Providers.MockAIJobRefinementProvider>();
        services.AddScoped<Aivora.Services.AIJobRefinementService.Providers.GeminiAIJobRefinementProvider>();
        services.AddScoped<Aivora.Services.AIJobRefinementService.IAIJobRefinementProvider>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AIProviderOptions>>().Value;
            return string.Equals(options.Provider, "Gemini", StringComparison.OrdinalIgnoreCase)
                   && !string.IsNullOrWhiteSpace(options.ApiKey)
                ? sp.GetRequiredService<Aivora.Services.AIJobRefinementService.Providers.GeminiAIJobRefinementProvider>()
                : sp.GetRequiredService<Aivora.Services.AIJobRefinementService.Providers.MockAIJobRefinementProvider>();
        });
        services.AddScoped<Aivora.Services.AIJobRefinementService.IService, Aivora.Services.AIJobRefinementService.Service>();

        services.AddScoped<Aivora.Services.AIMilestoneStepAssistantService.Prompting.AIMilestoneStepSuggestionPromptBuilder>();
        services.AddScoped<Aivora.Services.AIMilestoneStepAssistantService.Parsing.AIMilestoneStepSuggestionParser>();
        services.AddScoped<Aivora.Services.AIMilestoneStepAssistantService.Providers.MockAIMilestoneStepSuggestionProvider>();
        services.AddScoped<Aivora.Services.AIMilestoneStepAssistantService.Providers.GeminiAIMilestoneStepSuggestionProvider>();
        services.AddScoped<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AIProviderOptions>>().Value;
            return string.Equals(options.Provider, "Gemini", StringComparison.OrdinalIgnoreCase)
                   && !string.IsNullOrWhiteSpace(options.ApiKey)
                ? sp.GetRequiredService<Aivora.Services.AIMilestoneStepAssistantService.Providers.GeminiAIMilestoneStepSuggestionProvider>()
                : sp.GetRequiredService<Aivora.Services.AIMilestoneStepAssistantService.Providers.MockAIMilestoneStepSuggestionProvider>();
        });

        services.AddScoped<Aivora.Services.RecommendationService.IService, Aivora.Services.RecommendationService.Service>();
        services.AddScoped<Aivora.Services.ProjectService.IService, Aivora.Services.ProjectService.Service>();
        services.AddScoped<Aivora.Services.MilestoneService.IService, Aivora.Services.MilestoneService.Service>();
        services.AddScoped<Aivora.Services.DeliverableService.IService, Aivora.Services.DeliverableService.Service>();
        services.AddScoped<Aivora.Services.WalletService.IVNPayService, Aivora.Services.WalletService.VNPayService>();
        services.AddScoped<Aivora.Services.WalletService.IService, Aivora.Services.WalletService.Service>();
        services.AddScoped<Aivora.Services.ReviewService.IService, Aivora.Services.ReviewService.Service>();
        services.AddScoped<Aivora.Services.MessageService.IService, Aivora.Services.MessageService.Service>();
        services.AddScoped<Aivora.Services.DisputeService.IService, Aivora.Services.DisputeService.Service>();
        services.AddScoped<Aivora.Services.NotificationService.IService, Aivora.Services.NotificationService.Service>();
        services.AddScoped<Aivora.Services.AdminService.IAdminService, Aivora.Services.AdminService.AdminService>();
        services.AddScoped<Aivora.Services.Treasury.ICommissionCalculator, Aivora.Services.Treasury.CommissionCalculator>();
        services.AddScoped<Aivora.Services.Treasury.ITreasury, Aivora.Services.Treasury.Treasury>();
        services.AddScoped<Aivora.Services.RealtimeService.IService, Aivora.Services.RealtimeService.Service>();

        // ExpertVerificationService
        services.AddScoped<Aivora.Services.ExpertVerificationService.Prompting.AIExpertVerificationPromptBuilder>();
        services.AddScoped<Aivora.Services.ExpertVerificationService.Parsing.AIExpertVerificationParser>();
        services.AddScoped<Aivora.Services.ExpertVerificationService.Providers.MockAIExpertVerificationProvider>();
        services.AddScoped<Aivora.Services.ExpertVerificationService.Providers.GeminiAIExpertVerificationProvider>();
        services.AddScoped<Aivora.Services.ExpertVerificationService.Providers.IAIExpertVerificationProvider>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AIProviderOptions>>().Value;
            return string.Equals(options.Provider, "Gemini", StringComparison.OrdinalIgnoreCase)
                   && !string.IsNullOrWhiteSpace(options.ApiKey)
                ? sp.GetRequiredService<Aivora.Services.ExpertVerificationService.Providers.GeminiAIExpertVerificationProvider>()
                : sp.GetRequiredService<Aivora.Services.ExpertVerificationService.Providers.MockAIExpertVerificationProvider>();
        });
        services.AddScoped<Aivora.Services.ExpertVerificationService.IService, Aivora.Services.ExpertVerificationService.Service>();

        return services;
    }

    public static IServiceCollection AddAivoraRealtime(this IServiceCollection services)
    {
        services.AddSignalR();
        return services;
    }

    public static IServiceCollection AddAivoraControllers(this IServiceCollection services)
    {
        services.AddControllers().AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        });

        return services;
    }
}
