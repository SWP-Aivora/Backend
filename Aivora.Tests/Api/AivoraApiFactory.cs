using Aivora.Repositories.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aivora.Tests.Api;

public sealed class AivoraApiFactory : WebApplicationFactory<Program>
{
    private readonly string _environment;
    private readonly Dictionary<string, string?> _configuration;
    private readonly Dictionary<string, string?> _previousEnvironmentValues = new();
    private readonly string _databaseName = $"aivora-api-tests-{Guid.NewGuid()}";

    public AivoraApiFactory(
        string environment = "Development",
        IDictionary<string, string?>? configurationOverrides = null)
    {
        _environment = environment;
        _configuration = DefaultConfiguration();

        if (configurationOverrides is not null)
        {
            foreach (var (key, value) in configurationOverrides)
            {
                _configuration[key] = value;
            }
        }

        ApplyEnvironmentConfiguration();
    }

    public static AivoraApiFactory Production(IDictionary<string, string?>? configurationOverrides = null)
    {
        var productionConfiguration = new Dictionary<string, string?>
        {
            ["AIProvider:Provider"] = "Gemini",
            ["AIProvider:ApiKey"] = "test-gemini-api-key",
            ["AIProvider:EnableFallback"] = "false"
        };

        if (configurationOverrides is not null)
        {
            foreach (var (key, value) in configurationOverrides)
            {
                productionConfiguration[key] = value;
            }
        }

        return new AivoraApiFactory("Production", productionConfiguration);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environment);
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(_configuration);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<AivoraDbContext>();
            services.RemoveAll<DbContextOptions<AivoraDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AivoraDbContext>>();
            services.AddDbContext<AivoraDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName)
                    .ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)));
        });
    }

    protected override void Dispose(bool disposing)
    {
        foreach (var (key, value) in _previousEnvironmentValues)
        {
            Environment.SetEnvironmentVariable(key, value);
        }

        base.Dispose(disposing);
    }

    private void ApplyEnvironmentConfiguration()
    {
        SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", _environment);

        foreach (var (key, value) in _configuration)
        {
            SetEnvironmentVariable(key.Replace(':', '_').Replace("_", "__"), value);
        }
    }

    private void SetEnvironmentVariable(string key, string? value)
    {
        _previousEnvironmentValues.TryAdd(key, Environment.GetEnvironmentVariable(key));
        Environment.SetEnvironmentVariable(key, value);
    }

    private static Dictionary<string, string?> DefaultConfiguration()
    {
        return new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Port=5432;Database=aivora_tests;Username=postgres;Password=test-password",
            ["JwtSettings:Secret"] = "test-secret-key-with-at-least-32-characters",
            ["JwtSettings:Issuer"] = "AivoraApiTests",
            ["JwtSettings:Audience"] = "AivoraApiTests",
            ["JwtSettings:ExpiryInMinutes"] = "60",
            ["CloudinaryOptions:CloudName"] = "test-cloud",
            ["CloudinaryOptions:ApiKey"] = "test-api-key",
            ["CloudinaryOptions:ApiSecret"] = "test-api-secret",
            ["AIProvider:Provider"] = "Mock",
            ["AIProvider:ApiKey"] = "",
            ["AIProvider:BaseUrl"] = "https://generativelanguage.googleapis.com",
            ["AIProvider:Model"] = "gemini-2.5-flash",
            ["AIProvider:EnableFallback"] = "true",
            ["RateLimit:Strict:PermitLimit"] = "1000",
            ["RateLimit:Strict:WindowInMinutes"] = "1",
            ["RateLimit:AI:PermitLimit"] = "1000",
            ["RateLimit:AI:WindowInMinutes"] = "1",
            ["RateLimit:General:PermitLimit"] = "1000",
            ["RateLimit:General:WindowInMinutes"] = "1"
        };
    }
}
