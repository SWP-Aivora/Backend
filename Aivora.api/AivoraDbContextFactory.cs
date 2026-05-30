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
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        var builder = new DbContextOptionsBuilder<AivoraDbContext>();
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        builder.UseNpgsql(connectionString);

        // We don't necessarily need the interceptor for migrations, but we can add it if needed
        return new AivoraDbContext(builder.Options);
    }
}
