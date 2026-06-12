using System.Threading.RateLimiting;
using Aivora.Repositories.Abstractions;
using Aivora.Repositories.Data;
using Aivora.Repositories.Data.Interceptors;
using Aivora.Repositories.Repositories.Jobs;
using Aivora.Repositories.Repositories.Milestones;
using Aivora.Repositories.Repositories.Proposals;
using Aivora.Repositories.Repositories.Projects;
using Aivora.Repositories.Repositories.Treasury;
using Aivora.Services.JwtService;
using Aivora.Services.Models;
using Aivora.Services.Options;
using Microsoft.AspNetCore.Mvc;
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

    public static IServiceCollection AddAivoraCors(this IServiceCollection services)
    {
        services.AddCors(options =>
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

        return services;
    }

    public static IServiceCollection AddAivoraOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection("JwtSettings"));
        services.Configure<CloudinaryOptions>(configuration.GetSection("CloudinaryOptions"));
        services.Configure<AIProviderOptions>(configuration.GetSection("AIProvider"));
        services.Configure<RateLimitOptions>(configuration.GetSection(RateLimitOptions.SectionName));

        return services;
    }

    public static IServiceCollection AddAivoraRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddRateLimiter(options =>
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

        services.AddScoped<IJwtService, Aivora.Services.JwtService.JwtTokenService>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IJobRepository, JobRepository>();
        services.AddScoped<IMilestoneRepository, MilestoneRepository>();
        services.AddScoped<IProposalRepository, ProposalRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<ITreasuryRepository, TreasuryRepository>();
        services.AddScoped<Aivora.Services.MediaService.IService, Aivora.Services.MediaService.MediaApplicationService>();
        services.AddScoped<Aivora.Services.IdentityService.IService, Aivora.Services.IdentityService.IdentityApplicationService>();
        services.AddScoped<Aivora.Services.CategoryService.IService, Aivora.Services.CategoryService.CategoryApplicationService>();
        services.AddScoped<Aivora.Services.SkillService.IService, Aivora.Services.SkillService.SkillApplicationService>();
        services.AddScoped<Aivora.Services.ProfileService.IService, Aivora.Services.ProfileService.ProfileApplicationService>();
        services.AddScoped<Aivora.Services.JobService.IService, Aivora.Services.JobService.JobApplicationService>();
        services.AddScoped<Aivora.Services.ProposalService.IService, Aivora.Services.ProposalService.ProposalApplicationService>();
        services.AddScoped<Aivora.Services.HiringService.IHiringService, Aivora.Services.HiringService.HiringService>();
        services.AddScoped<Aivora.Services.RecommendationService.IService, Aivora.Services.RecommendationService.RecommendationApplicationService>();
        services.AddScoped<Aivora.Services.ProjectService.IService, Aivora.Services.ProjectService.ProjectApplicationService>();
        services.AddScoped<Aivora.Services.MilestoneService.IService, Aivora.Services.MilestoneService.MilestoneApplicationService>();
        services.AddScoped<Aivora.Services.DeliverableService.IService, Aivora.Services.DeliverableService.DeliverableApplicationService>();
        services.AddScoped<Aivora.Services.WalletService.IService, Aivora.Services.WalletService.WalletApplicationService>();
        services.AddScoped<Aivora.Services.ReviewService.IService, Aivora.Services.ReviewService.ReviewApplicationService>();
        services.AddScoped<Aivora.Services.MessageService.IService, Aivora.Services.MessageService.MessageApplicationService>();
        services.AddScoped<Aivora.Services.DisputeService.IService, Aivora.Services.DisputeService.DisputeApplicationService>();
        services.AddScoped<Aivora.Services.NotificationService.IService, Aivora.Services.NotificationService.NotificationApplicationService>();
        services.AddScoped<Aivora.Services.AdminService.IAdminService, Aivora.Services.AdminService.AdminService>();
        services.AddScoped<Aivora.Services.Treasury.ITreasury, Aivora.Services.Treasury.Treasury>();

        services.AddAIJobAssistantServices();

        return services;
    }

    public static IServiceCollection AddAivoraRealtime(this IServiceCollection services)
    {
        services.AddSignalR();
        return services;
    }

    public static IServiceCollection AddAivoraControllers(this IServiceCollection services)
    {
        services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
            });

        services.Configure<ApiBehaviorOptions>(options =>
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

        return services;
    }

    private static void AddAIJobAssistantServices(this IServiceCollection services)
    {
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
        services.AddScoped<Aivora.Services.AIJobAssistantService.IAIJobSuggestionProvider>(ResolveSuggestionProvider);
        services.AddScoped<Aivora.Services.AIJobAssistantService.IAIJobRefinementProvider>(ResolveRefinementProvider);
        services.AddScoped<Aivora.Services.AIJobAssistantService.IAIServiceDescriptionProvider>(ResolveServiceDescriptionProvider);
        services.AddScoped<Aivora.Services.AIJobAssistantService.IService, Aivora.Services.AIJobAssistantService.AIJobAssistantApplicationService>();
    }

    private static Aivora.Services.AIJobAssistantService.IAIJobSuggestionProvider ResolveSuggestionProvider(IServiceProvider sp)
    {
        var options = sp.GetRequiredService<IOptions<AIProviderOptions>>().Value;
        if (ConfigurationValidationExtensions.UseGemini(options))
        {
            return sp.GetRequiredService<Aivora.Services.AIJobAssistantService.Providers.GeminiAIJobSuggestionProvider>();
        }

        EnsureMockAllowed(sp, options);
        LogMockProvider(sp, options, "job suggestion");
        return sp.GetRequiredService<Aivora.Services.AIJobAssistantService.Providers.MockAIJobSuggestionProvider>();
    }

    private static Aivora.Services.AIJobAssistantService.IAIJobRefinementProvider ResolveRefinementProvider(IServiceProvider sp)
    {
        var options = sp.GetRequiredService<IOptions<AIProviderOptions>>().Value;
        if (ConfigurationValidationExtensions.UseGemini(options))
        {
            return sp.GetRequiredService<Aivora.Services.AIJobAssistantService.Providers.GeminiAIJobRefinementProvider>();
        }

        EnsureMockAllowed(sp, options);
        LogMockProvider(sp, options, "job refinement");
        return sp.GetRequiredService<Aivora.Services.AIJobAssistantService.Providers.MockAIJobRefinementProvider>();
    }

    private static Aivora.Services.AIJobAssistantService.IAIServiceDescriptionProvider ResolveServiceDescriptionProvider(IServiceProvider sp)
    {
        var options = sp.GetRequiredService<IOptions<AIProviderOptions>>().Value;
        if (ConfigurationValidationExtensions.UseGemini(options))
        {
            return sp.GetRequiredService<Aivora.Services.AIJobAssistantService.Providers.GeminiAIServiceDescriptionProvider>();
        }

        EnsureMockAllowed(sp, options);
        LogMockProvider(sp, options, "service description");
        return sp.GetRequiredService<Aivora.Services.AIJobAssistantService.Providers.MockAIServiceDescriptionProvider>();
    }

    private static void EnsureMockAllowed(IServiceProvider sp, AIProviderOptions options)
    {
        var environment = sp.GetRequiredService<IWebHostEnvironment>();
        if (environment.IsProduction())
        {
            throw new InvalidOperationException("AIProvider__Provider=Gemini and AIProvider__ApiKey are required in Production.");
        }
    }

    private static void LogMockProvider(IServiceProvider sp, AIProviderOptions options, string providerPurpose)
    {
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("AIProvider");
        logger.LogWarning("Using mock AI {ProviderPurpose} provider because provider {Provider} is not fully configured.", providerPurpose, options.Provider);
    }
}
