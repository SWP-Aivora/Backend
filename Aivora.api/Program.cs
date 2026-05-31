using Aivora.api.Extensions;
using Aivora.api.Middlewares;
using Aivora.Repositories.Data;
using Aivora.Repositories.Data.Interceptors;
using Aivora.Services.Options;
using Aivora.Services.JwtService;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSingleton<AuditableEntityInterceptor>();

// builder.Services.AddDbContext<AivoraDbContext>((sp, options) => {
//     var interceptor = sp.GetRequiredService<AuditableEntityInterceptor>();
//     options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
//            .AddInterceptors(interceptor);
// });

// ── Validate required configuration ────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Required configuration 'ConnectionStrings:DefaultConnection' is missing. " +
        "Set it via CONNECTION_STRING env var in .env file.");
}

var jwtSecret = builder.Configuration["JwtSettings:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret == "JWT_SECRET_PLACEHOLDER")
{
    throw new InvalidOperationException(
        "Required configuration 'JwtSettings:Secret' is missing. " +
        "Set it via JWT_SECRET env var in .env file.");
}

var jwtIssuer = builder.Configuration["JwtSettings:Issuer"];
if (string.IsNullOrWhiteSpace(jwtIssuer) || jwtIssuer == "JWT_ISSUER_PLACEHOLDER")
{
    throw new InvalidOperationException(
        "Required configuration 'JwtSettings:Issuer' is missing. " +
        "Set it via JWT_ISSUER env var in .env file.");
}

var jwtAudience = builder.Configuration["JwtSettings:Audience"];
if (string.IsNullOrWhiteSpace(jwtAudience) || jwtAudience == "JWT_AUDIENCE_PLACEHOLDER")
{
    throw new InvalidOperationException(
        "Required configuration 'JwtSettings:Audience' is missing. " +
        "Set it via JWT_AUDIENCE env var in .env file.");
}

var jwtExpiry = builder.Configuration["JwtSettings:ExpiryInMinutes"];
if (string.IsNullOrWhiteSpace(jwtExpiry) || jwtExpiry == "0" || !int.TryParse(jwtExpiry, out var expiryMin) || expiryMin <= 0)
{
    throw new InvalidOperationException(
        "Required configuration 'JwtSettings:ExpiryInMinutes' is missing or invalid. " +
        "Set it via JWT_EXPIRY_IN_MINUTES env var in .env file.");
}

var cloudinaryCloudName = builder.Configuration["CloudinaryOptions:CloudName"];
if (string.IsNullOrWhiteSpace(cloudinaryCloudName) || cloudinaryCloudName == "CLOUDINARY_CLOUD_NAME_PLACEHOLDER")
{
    throw new InvalidOperationException(
        "Required configuration 'CloudinaryOptions:CloudName' is missing. " +
        "Set it via CLOUDINARY_CLOUD_NAME env var in .env file.");
}

var cloudinaryApiKey = builder.Configuration["CloudinaryOptions:ApiKey"];
if (string.IsNullOrWhiteSpace(cloudinaryApiKey) || cloudinaryApiKey == "CLOUDINARY_API_KEY_PLACEHOLDER")
{
    throw new InvalidOperationException(
        "Required configuration 'CloudinaryOptions:ApiKey' is missing. " +
        "Set it via CLOUDINARY_API_KEY env var in .env file.");
}

var cloudinaryApiSecret = builder.Configuration["CloudinaryOptions:ApiSecret"];
if (string.IsNullOrWhiteSpace(cloudinaryApiSecret) || cloudinaryApiSecret == "CLOUDINARY_API_SECRET_PLACEHOLDER")
{
    throw new InvalidOperationException(
        "Required configuration 'CloudinaryOptions:ApiSecret' is missing. " +
        "Set it via CLOUDINARY_API_SECRET env var in .env file.");
}

// ── Register services ──────────────────────────────────────────
Console.WriteLine("===== DB CONNECTION STRING DEBUG =====");
Console.WriteLine(connectionString ?? "NULL");
Console.WriteLine("======================================");

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
builder.Services.AddScoped<Aivora.Services.HiringWorkflowService.IService, Aivora.Services.HiringWorkflowService.Service>();
builder.Services.AddScoped<Aivora.Services.AIJobAssistantService.IService, Aivora.Services.AIJobAssistantService.Service>();
builder.Services.AddScoped<Aivora.Services.RecommendationService.IService, Aivora.Services.RecommendationService.Service>();
builder.Services.AddScoped<Aivora.Services.ProjectService.IService, Aivora.Services.ProjectService.Service>();
builder.Services.AddScoped<Aivora.Services.MilestoneService.IService, Aivora.Services.MilestoneService.Service>();
builder.Services.AddScoped<Aivora.Services.DeliverableService.IService, Aivora.Services.DeliverableService.Service>();
builder.Services.AddScoped<Aivora.Services.WalletService.IService, Aivora.Services.WalletService.Service>();
builder.Services.AddScoped<Aivora.Services.ReviewService.IService, Aivora.Services.ReviewService.Service>();
builder.Services.AddScoped<Aivora.Services.MessageService.IService, Aivora.Services.MessageService.Service>();
builder.Services.AddScoped<Aivora.Services.DisputeService.IService, Aivora.Services.DisputeService.Service>();
builder.Services.AddScoped<Aivora.Services.FinancialLedger.IFinancialLedger, Aivora.Services.FinancialLedger.FinancialLedger>();

builder.Services.AddSignalR();
builder.Services.AddControllers();

// Native OpenAPI Configuration (.NET 10)
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(); // Accessible at /scalar/v1
}

app.UseHttpsRedirection();

app.UseCors("AllowSpecificOrigin");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<Aivora.api.Hubs.ChatHub>("/api/v1/chat");

// Seed the database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AivoraDbContext>();
        context.Database.Migrate(); // Apply pending migrations
        await Aivora.Repositories.Data.SeedData.Initialize(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred seeding the DB.");
    }
}

app.Run();