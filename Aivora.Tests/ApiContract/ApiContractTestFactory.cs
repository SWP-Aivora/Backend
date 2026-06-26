using System.Net.Http.Headers;
using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.JwtService;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aivora.Tests.ApiContract;

/// <summary>
///   WebApplicationFactory dùng cho API contract tests.
///   Dùng InMemory database, FakeMediaService, và JWT test config.
/// </summary>
public class ApiContractTestFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();
    private bool _seeded;

    // ── Config ────────────────────────────────────────────────────
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Secret"] = "super-secret-test-key-that-is-long-enough-32chars!",
                ["JwtSettings:Issuer"] = "aivora-test",
                ["JwtSettings:Audience"] = "aivora-test-api",
                ["JwtSettings:AccessTokenExpiryMinutes"] = "60",
                ["ConnectionStrings:DefaultConnection"] = "Host=test-contract-inmemory;Database=test-contract;Username=test;Password=test"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Thay DbContext (PostgreSQL → InMemory)
            var dbDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AivoraDbContext>));
            if (dbDescriptor is not null) services.Remove(dbDescriptor);

            services.AddDbContext<AivoraDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));

            // Thay MediaService thật → FakeMediaService
            var mediaDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(Aivora.Services.MediaService.IService));
            if (mediaDescriptor is not null) services.Remove(mediaDescriptor);

            services.AddScoped<Aivora.Services.MediaService.IService, FakeMediaService>();
        });
    }

    // ── Seed helpers ──────────────────────────────────────────────
    private void EnsureSeeded()
    {
        if (_seeded) return;

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AivoraDbContext>();
        db.Database.EnsureCreated();
        ApiContractTestData.Seed(db);
        _seeded = true;
    }

    // ── Public API ────────────────────────────────────────────────
    /// <summary>Tạo HttpClient đã gắn JWT token cho role tương ứng.</summary>
    public HttpClient CreateAuthenticatedClient(UserRole role)
    {
        EnsureSeeded();

        var client = CreateClient();
        var token = GenerateToken(role);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Tạo HttpClient không có auth (anonymous).</summary>
    public HttpClient CreateUnauthenticatedClient()
    {
        EnsureSeeded();
        return CreateClient();
    }

    /// <summary>Tạo một DbContext riêng để kiểm tra dữ liệu trực tiếp.</summary>
    public AivoraDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AivoraDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;
        return new AivoraDbContext(options);
    }

    /// <summary>Sinh JWT token cho role, dùng user đã seed.</summary>
    public string GenerateToken(UserRole role)
    {
        using var scope = Services.CreateScope();
        var jwt = scope.ServiceProvider.GetRequiredService<IJwtService>();
        var db = scope.ServiceProvider.GetRequiredService<AivoraDbContext>();

        var user = db.Users.FirstOrDefault(u => u.Role == role)
            ?? throw new InvalidOperationException($"No seeded user found for role {role}.");

        return jwt.GenerateAccessToken(user, role.ToString());
    }
}
