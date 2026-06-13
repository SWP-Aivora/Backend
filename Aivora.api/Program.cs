using Aivora.api.Extensions;
using Aivora.api.Middlewares;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.ValidateRequiredConfiguration();
builder.Configuration.ValidateAIProviderConfiguration(builder.Environment);

builder.Services.AddAivoraPersistence(builder.Configuration);
builder.Services.AddAivoraCors();
builder.Services.AddAivoraOptions(builder.Configuration);
builder.Services.AddAivoraRateLimiting(builder.Configuration);
builder.Services.AddAivoraApplicationServices(builder.Configuration);
builder.Services.AddAivoraRealtime();
builder.Services.AddAivoraControllers();
builder.Services.AddOpenApiServices();

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

await app.MigrateAndSeedDatabaseAsync();

app.Run();

public partial class Program { }
