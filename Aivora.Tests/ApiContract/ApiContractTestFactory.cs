using System;
using System.Collections.Generic;
using System.Linq;
using Aivora.Repositories.Data;
using Aivora.Repositories.Data.Interceptors;
using Aivora.Services.MediaService;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Aivora.Tests.ApiContract;

public class ApiContractTestFactory : WebApplicationFactory<Program>
{
    private const string TestConnectionString = "Host=localhost;Port=5432;Database=aivora_api_contract_tests;Username=postgres;Password=postgres";

    private static readonly Dictionary<string, string?> TestEnvironmentVariables = new()
    {
        ["ConnectionStrings__DefaultConnection"] = TestConnectionString,
        ["JwtSettings__Secret"] = "SuperSecretKeyForTestingJwtAuthentication123!",
        ["JwtSettings__Issuer"] = "Aivora",
        ["JwtSettings__Audience"] = "Aivora",
        ["JwtSettings__ExpiryInMinutes"] = "60",
        ["CloudinaryOptions__CloudName"] = "fake-cloudinary",
        ["CloudinaryOptions__ApiKey"] = "fake-api-key",
        ["CloudinaryOptions__ApiSecret"] = "fake-api-secret",
        ["AIProvider__Provider"] = "Mock",
        ["RateLimit__Strict__PermitLimit"] = "1000",
        ["RateLimit__AI__PermitLimit"] = "1000",
        ["RateLimit__General__PermitLimit"] = "1000"
    };

    private readonly Dictionary<string, string?> _originalEnvironmentVariables = new();
    private readonly ServiceProvider _inMemoryDatabaseProvider = new ServiceCollection()
        .AddEntityFrameworkInMemoryDatabase()
        .BuildServiceProvider();

    public string DatabaseName { get; } = Guid.NewGuid().ToString();

    public ApiContractTestFactory()
    {
        foreach (var (key, value) in TestEnvironmentVariables)
        {
            _originalEnvironmentVariables[key] = Environment.GetEnvironmentVariable(key);
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((context, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = TestConnectionString,
                ["JwtSettings:Secret"] = "SuperSecretKeyForTestingJwtAuthentication123!",
                ["JwtSettings:Issuer"] = "Aivora",
                ["JwtSettings:Audience"] = "Aivora",
                ["JwtSettings:ExpiryInMinutes"] = "60",
                ["CloudinaryOptions:CloudName"] = "fake-cloudinary",
                ["CloudinaryOptions:ApiKey"] = "fake-api-key",
                ["CloudinaryOptions:ApiSecret"] = "fake-api-secret",
                ["AIProvider:Provider"] = "Mock",
                ["RateLimit:Strict:PermitLimit"] = "1000",
                ["RateLimit:Strict:WindowInMinutes"] = "1",
                ["RateLimit:AI:PermitLimit"] = "1000",
                ["RateLimit:AI:WindowInMinutes"] = "1",
                ["RateLimit:General:PermitLimit"] = "1000",
                ["RateLimit:General:WindowInMinutes"] = "1",
            });
        });

        builder.ConfigureServices(services =>
        {
            var dbContextDescriptors = services
                .Where(d =>
                    d.ServiceType == typeof(AivoraDbContext)
                    || d.ServiceType == typeof(DbContextOptions<AivoraDbContext>)
                    || d.ServiceType == typeof(DbContextOptions))
                .ToList();

            foreach (var descriptor in dbContextDescriptors)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<AivoraDbContext>((sp, options) =>
            {
                var interceptor = sp.GetRequiredService<AuditableEntityInterceptor>();
                options.UseInMemoryDatabase(DatabaseName)
                    .UseInternalServiceProvider(_inMemoryDatabaseProvider)
                    .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                    .AddInterceptors(interceptor);
            });

            var mediaServiceDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IService));
            if (mediaServiceDescriptor != null)
            {
                services.Remove(mediaServiceDescriptor);
            }
            services.AddScoped<IService, FakeMediaService>();
        });
    }

    public void SeedDatabase()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AivoraDbContext>();
        ApiContractTestData.Seed(db);
    }

    protected override void Dispose(bool disposing)
    {
        foreach (var (key, value) in _originalEnvironmentVariables)
        {
            Environment.SetEnvironmentVariable(key, value);
        }

        base.Dispose(disposing);
        _inMemoryDatabaseProvider.Dispose();
    }
}
