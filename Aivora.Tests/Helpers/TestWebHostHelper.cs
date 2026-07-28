using Aivora.Repositories.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aivora.Tests.Helpers;

/// <summary>
/// Helper methods để cấu hình WebApplicationFactory cho integration tests.
/// Giải quyết 2 vấn đề: placeholder config validation và InMemory database swap.
/// Lý do phải set env var: DotNetEnv.Env.Load() chạy trước WebApplication.CreateBuilder()
/// và set OS-level environment variables — không thể override bằng ConfigureAppConfiguration.
/// </summary>
public static class TestWebHostHelper
{
    private static readonly Dictionary<string, string> TestEnvVars = new()
    {
        ["ConnectionStrings__DefaultConnection"] = "Host=test;Database=test;Username=test;Password=test",
        ["JwtSettings__Secret"] = "this-is-a-test-secret-key-at-least-32-chars-long!",
        ["JwtSettings__Issuer"] = "test-issuer",
        ["JwtSettings__Audience"] = "test-audience",
        ["JwtSettings__ExpiryInMinutes"] = "60",
        ["CloudinaryOptions__CloudName"] = "test-cloud",
        ["CloudinaryOptions__ApiKey"] = "test-api-key",
        ["CloudinaryOptions__ApiSecret"] = "test-api-secret",
        ["AIProvider__Provider"] = "Mock",
        ["VNPay__BaseUrl"] = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
    };

    /// <summary>
    /// Set test environment variables (override .env placeholders).
    /// Gọi method này TRƯỚC khi tạo WebApplicationFactory hoặc CreateClient.
    /// </summary>
    public static void SetupTestEnvironment()
    {
        foreach (var (key, value) in TestEnvVars)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    /// <summary>
    /// Cấu hình WebHostBuilder: swap Npgsql → InMemory database.
    /// Yêu cầu đã gọi SetupTestEnvironment() trước đó.
    /// Dùng direct registration để tránh xung đột dual-provider (Npgsql + InMemory).
    /// Suppress TransactionIgnoredWarning vì InMemory không hỗ trợ transaction.
    /// </summary>
    public static void ConfigureTestHost(IWebHostBuilder builder, string dbName)
    {
        builder.ConfigureServices(services =>
        {
            // Remove ALL AivoraDbContext and DbContextOptions registrations
            var descriptorsToRemove = services
                .Where(s => s.ServiceType == typeof(AivoraDbContext)
                         || s.ServiceType == typeof(DbContextOptions<AivoraDbContext>)
                         || s.ServiceType == typeof(DbContextOptions))
                .ToList();

            foreach (var descriptor in descriptorsToRemove)
            {
                services.Remove(descriptor);
            }

            // Remove AuditableEntityInterceptor (coupled to Npgsql via UseNpgsql)
            services.RemoveAll<Aivora.Repositories.Data.Interceptors.AuditableEntityInterceptor>();

            // Build options with InMemory, suppressing transaction warnings
            var options = new DbContextOptionsBuilder<AivoraDbContext>()
                .UseInMemoryDatabase(dbName)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            services.AddScoped(_ => options);
            services.AddScoped<AivoraDbContext>(_ => new AivoraDbContext(options));
        });
    }
}
