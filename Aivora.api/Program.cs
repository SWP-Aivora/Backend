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

builder.Services.AddDbContext<AivoraDbContext>((sp, options) => {
    var interceptor = sp.GetRequiredService<AuditableEntityInterceptor>();
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
           .AddInterceptors(interceptor);
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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<Aivora.api.Hubs.ChatHub>("/api/v1/chat");

app.Run();