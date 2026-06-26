using System.Net.Http.Headers;
using Aivora.Repositories.Data;
using Aivora.Repositories.Data.Interceptors;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.JwtService;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aivora.Tests.ApiContract;

/// <summary>
///   WebApplicationFactory dùng cho API contract tests.
///   Dùng InMemory database (với dedicated service provider), FakeMediaService, và env-var test config.
/// </summary>
public class ApiContractTestFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();
    private bool _seeded;
    public readonly ApiVerificationTracker Tracker = new();

    private readonly ServiceProvider _inMemoryProvider = new ServiceCollection()
        .AddEntityFrameworkInMemoryDatabase()
        .BuildServiceProvider();

    // ── Config ────────────────────────────────────────────────────
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Use "Test" environment so the host loads appsettings.Test.json
        // which overrides placeholder values from appsettings.json.
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            // Enable console logging for test debugging
            services.AddLogging(logging => logging.AddConsole().SetMinimumLevel(LogLevel.Error));

            // Remove all DbContext registrations (PostgreSQL-based)
            var descriptors = services
                .Where(d => d.ServiceType == typeof(AivoraDbContext)
                         || d.ServiceType == typeof(DbContextOptions<AivoraDbContext>)
                         || d.ServiceType == typeof(DbContextOptions))
                .ToList();
            foreach (var d in descriptors) services.Remove(d);

            // Add InMemory DbContext with dedicated service provider + interceptor
            services.AddDbContext<AivoraDbContext>((sp, options) =>
            {
                var interceptor = sp.GetRequiredService<AuditableEntityInterceptor>();
                options.UseInMemoryDatabase(_dbName)
                    .UseInternalServiceProvider(_inMemoryProvider)
                    .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                    .AddInterceptors(interceptor);
            });

            // Disable default DataSeeder (test data controls its own seed)
            var seederDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(Aivora.Repositories.IAivoraDataSeeder));
            if (seederDescriptor is not null) services.Remove(seederDescriptor);

            // Replace MediaService
            var mediaDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(Aivora.Services.MediaService.IService));
            if (mediaDescriptor is not null) services.Remove(mediaDescriptor);
            services.AddScoped<Aivora.Services.MediaService.IService, FakeMediaService>();
        });
    }

    // ── Seed ──────────────────────────────────────────────────────
    private void EnsureSeeded()
    {
        if (_seeded) return;

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AivoraDbContext>();
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();
        ApiContractTestData.Seed(db);
        _seeded = true;
    }

    public void SeedDatabase() => EnsureSeeded();

    // ── Public API ────────────────────────────────────────────────
    public HttpClient CreateAuthenticatedClient(UserRole role)
    {
        EnsureSeeded();
        var client = CreateClient();
        var token = GenerateToken(role);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public HttpClient CreateUnauthenticatedClient()
    {
        EnsureSeeded();
        return CreateClient();
    }

    public AivoraDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AivoraDbContext>()
            .UseInMemoryDatabase(_dbName)
            .UseInternalServiceProvider(_inMemoryProvider)
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AivoraDbContext(options);
    }

    public string GenerateToken(UserRole role)
    {
        using var scope = Services.CreateScope();
        var jwt = scope.ServiceProvider.GetRequiredService<IJwtService>();
        var db = scope.ServiceProvider.GetRequiredService<AivoraDbContext>();

        var user = db.Users.FirstOrDefault(u => u.Role == role)
            ?? throw new InvalidOperationException($"No seeded user found for role {role}.");

        return jwt.GenerateAccessToken(user, role.ToString());
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _inMemoryProvider.Dispose();
        base.Dispose(disposing);
    }
}
