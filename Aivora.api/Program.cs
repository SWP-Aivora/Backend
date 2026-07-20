using Aivora.api.Extensions;
using Aivora.api.Middlewares;
using Microsoft.AspNetCore.HttpOverrides;

// Load .env file only in development, not in production
if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")?.ToLower() != "production")
{
    DotNetEnv.Env.Load();
}

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.ValidateRequiredConfiguration();
builder.Configuration.ValidateAIProviderConfiguration(builder.Environment);

builder.Services.AddAivoraPersistence(builder.Configuration);
builder.Services.AddAivoraCors(builder.Configuration);
builder.Services.AddAivoraOptions(builder.Configuration);
builder.Services.AddAivoraRateLimiting(builder.Configuration);
builder.Services.AddAivoraApplicationServices(builder.Configuration);
builder.Services.AddAivoraRealtime();
builder.Services.AddAivoraControllers();
builder.Services.AddOpenApiServices();
builder.Services.AddAivoraDataSeeder();

var app = builder.Build();

app.UseMiddleware<ExceptionMiddleware>();

var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedHeadersOptions.KnownIPNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

app.UseOpenApiUI();
app.UseHttpsRedirection();
app.UseCors("AllowSpecificOrigin");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<Aivora.api.Hubs.ChatHub>("/api/v1/chat");

// Run migrations and seed data on startup
try
{
    await app.MigrateDatabaseAsync();
    await app.EnsureSystemSeedAsync();
    if (!app.Environment.IsProduction())
    {
        await app.SeedDataAsync();
    }
}
catch (Exception ex)
{
    // Log the error but don't crash the application
    Console.WriteLine($"Error during data seeding: {ex.Message}");
}

app.Run();

public partial class Program { }
