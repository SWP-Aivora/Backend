using Aivora.Repositories;
using Aivora.Repositories.Data;

namespace Aivora.api.Extensions;

public static class SeedingServiceExtensions
{
    public static Task EnsureSystemSeedAsync(this IApplicationBuilder app)
    {
        return WithSeederAsync(app, seeder => seeder.EnsureSystemUserAsync());
    }

    public static Task SeedDataAsync(this IApplicationBuilder app)
    {
        return WithSeederAsync(app, seeder => seeder.SeedAsync());
    }

    private static async Task WithSeederAsync(IApplicationBuilder app, Func<IAivoraDataSeeder, Task> action)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IAivoraDataSeeder>();
        await action(seeder);
    }

    public static IServiceCollection AddAivoraDataSeeder(this IServiceCollection services)
    {
        services.AddScoped<IAivoraDataSeeder, AivoraDataSeeder>();
        return services;
    }
}