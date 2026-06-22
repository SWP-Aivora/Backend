using Microsoft.EntityFrameworkCore;

namespace Aivora.api.Extensions;

public static class DatabaseStartupExtensions
{
    public static async Task MigrateDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Program");
        var context = services.GetRequiredService<Aivora.Repositories.Data.AivoraDbContext>();

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
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Database migration failed. Startup aborted without deleting the configured database.");
            throw;
        }
    }
}
