using Aivora.Repositories.Data;
using Aivora.Repositories.Data.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Aivora.api;

public class AivoraDbContextFactory : IDesignTimeDbContextFactory<AivoraDbContext>
{
    public AivoraDbContext CreateDbContext(string[] args)
    {
        // Load .env file for EF CLI tools (migrations, etc.)
        DotNetEnv.Env.Load();

        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        var builder = new DbContextOptionsBuilder<AivoraDbContext>();
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'DefaultConnection' is missing or empty.");
        }

        builder.UseNpgsql(connectionString);

        return new AivoraDbContext(builder.Options);
    }
}
