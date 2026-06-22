using Aivora.Repositories.Data;
using Microsoft.EntityFrameworkCore;

namespace Aivora.api.Extensions;

public static class DatabaseStartupExtensions
{
    public static async Task MigrateAndSeedDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Program");
        var context = services.GetRequiredService<AivoraDbContext>();
        var forceReset = app.Configuration.GetValue<bool>("SeedForceReset");

        // Khi SeedForceReset=true, luôn xóa database và seed lại đầy đủ
        if (forceReset)
        {
            Console.WriteLine("WARNING: SeedForceReset=true - Database will be reset and fully reseeded!");
        }

        try
        {
            if (context.Database.IsRelational())
            {
                await context.Database.MigrateAsync();
            }
            else
            {
                await context.Database.EnsureCreatedAsync();
            }

            if (forceReset)
            {
                logger.LogWarning("SeedForceReset=true; seed-managed data will be reset in Development.");
            }

            await SeedData.Initialize(context, forceReset);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Database migration or seed failed. Startup aborted without deleting the configured database.");
            throw;
        }
    }
}
