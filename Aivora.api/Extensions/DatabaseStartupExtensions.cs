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

        // TODO: Tạm thời cho phép SeedForceReset trong Production để fix lỗi seeding
        // Sẽ revert lại khi seeding production ổn định
        if (forceReset && !app.Environment.IsDevelopment())
        {
            // throw new InvalidOperationException("SeedForceReset can only be used in Development.");
            Console.WriteLine("WARNING: SeedForceReset is being used in Production!");
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
