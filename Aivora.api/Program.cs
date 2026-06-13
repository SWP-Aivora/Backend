using Aivora.api.Extensions;
using Aivora.api.Middlewares;
using Aivora.Repositories.Data;
using Aivora.Repositories.Data.Interceptors;
using Aivora.Services.Options;
using Aivora.Services.JwtService;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSingleton<AuditableEntityInterceptor>();

// ── Validate required configuration ────────────────────────────
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

var missing = new List<string>();
var placeholders = new List<string>();

foreach (var (configKey, envVar) in requiredConfig)
{
    var value = builder.Configuration[configKey];

    if (string.IsNullOrWhiteSpace(value))
    {
        missing.Add($"  • {configKey}  —  set env var '{envVar}'");
    }
    else if (HasPlaceholder(value))
    {
        placeholders.Add($"  • {configKey}  —  current value is a placeholder, set real value via '{envVar}'");
    }
}

if (missing.Count > 0 || placeholders.Count > 0)
{
    var allErrors = new List<string>();
    allErrors.AddRange(missing);
    allErrors.AddRange(placeholders);
    var message = $"Missing {missing.Count} and {placeholders.Count} placeholder configuration value(s):\n"
        + string.Join("\n", allErrors);

    throw new InvalidOperationException(message);
}

static bool HasPlaceholder(string? value)
{
    if (string.IsNullOrWhiteSpace(value)) return true;

    return value.Contains("__SET", StringComparison.OrdinalIgnoreCase)
           || value.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase)
           || value.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase);
}

// ── Register services ──────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;

builder.Services.AddDbContext<AivoraDbContext>((sp, options) =>
{
    var interceptor = sp.GetRequiredService<AuditableEntityInterceptor>();
    options.UseNpgsql(connectionString)
        .AddInterceptors(interceptor);
});

// CORS Configuration
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

// Configure Options
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("JwtSettings"));
builder.Services.Configure<CloudinaryOptions>(builder.Configuration.GetSection("CloudinaryOptions"));
builder.Services.Configure<AIProviderOptions>(builder.Configuration.GetSection("AIProvider"));
builder.Services.Configure<RateLimitOptions>(builder.Configuration.GetSection(RateLimitOptions.SectionName));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
        {
            await context.HttpContext.Response.WriteAsJsonAsync(new { message = $"Too many requests. Please try again after {retryAfter.TotalSeconds} second(s)." }, token);
        }
        else
        {
            await context.HttpContext.Response.WriteAsJsonAsync(new { message = "Too many requests. Please try again later." }, token);
        }
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

// Register Services
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
    return string.Equals(options.Provider, "Gemini", StringComparison.OrdinalIgnoreCase)
           && !string.IsNullOrWhiteSpace(options.ApiKey)
        ? sp.GetRequiredService<Aivora.Services.AIJobAssistantService.Providers.GeminiAIJobSuggestionProvider>()
        : sp.GetRequiredService<Aivora.Services.AIJobAssistantService.Providers.MockAIJobSuggestionProvider>();
});
builder.Services.AddScoped<Aivora.Services.AIJobAssistantService.IAIJobRefinementProvider>(sp =>
{
    var options = sp.GetRequiredService<IOptions<AIProviderOptions>>().Value;
    return string.Equals(options.Provider, "Gemini", StringComparison.OrdinalIgnoreCase)
           && !string.IsNullOrWhiteSpace(options.ApiKey)
        ? sp.GetRequiredService<Aivora.Services.AIJobAssistantService.Providers.GeminiAIJobRefinementProvider>()
        : sp.GetRequiredService<Aivora.Services.AIJobAssistantService.Providers.MockAIJobRefinementProvider>();
});
builder.Services.AddScoped<Aivora.Services.AIJobAssistantService.IAIServiceDescriptionProvider>(sp =>
{
    var options = sp.GetRequiredService<IOptions<AIProviderOptions>>().Value;
    return string.Equals(options.Provider, "Gemini", StringComparison.OrdinalIgnoreCase)
           && !string.IsNullOrWhiteSpace(options.ApiKey)
        ? sp.GetRequiredService<Aivora.Services.AIJobAssistantService.Providers.GeminiAIServiceDescriptionProvider>()
        : sp.GetRequiredService<Aivora.Services.AIJobAssistantService.Providers.MockAIServiceDescriptionProvider>();
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
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

// Native OpenAPI Configuration (.NET 10)
builder.Services.AddOpenApiServices();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseMiddleware<ExceptionMiddleware>();

app.UseHttpsRedirection();

app.UseOpenApiUI();

app.UseCors("AllowSpecificOrigin");

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<Aivora.api.Hubs.ChatHub>("/api/v1/chat");

// Seed the database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var context = services.GetRequiredService<AivoraDbContext>();

    try
    {
        // Thử migrate bình thường
        context.Database.Migrate();

        var forceReset = builder.Configuration.GetValue<bool>("SeedForceReset");
        if (forceReset)
        {
            logger.LogWarning("⚠️ SeedForceReset=true — sẽ xóa hết data cũ và seed lại!");
        }
        await Aivora.Repositories.Data.SeedData.Initialize(context, forceReset);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "❌ Lỗi khi Migrate hoặc Seed DB. Thử khôi phục bằng cách Reset DB...");

        try
        {
            // Nếu lỗi nặng (xung đột schema), xóa sạch và làm lại từ đầu
            await context.Database.EnsureDeletedAsync();
            logger.LogWarning("♻️ Đã xóa Database cũ.");

            await context.Database.MigrateAsync();
            logger.LogInformation("✅ Đã khởi tạo lại Schema mới.");

            await Aivora.Repositories.Data.SeedData.Initialize(context, forceReset: true);
            logger.LogInformation("✅ Đã Seed lại dữ liệu mặc định.");
        }
        catch (Exception criticalEx)
        {
            logger.LogCritical(criticalEx, "🔥 KHÔNG THỂ KHÔI PHỤC DATABASE! Ứng dụng có thể hoạt động không ổn định.");
        }
    }
}

app.Run();
