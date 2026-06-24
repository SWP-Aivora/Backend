using Aivora.Repositories;
using Aivora.Repositories.Data;

namespace Aivora.api.Extensions;

public static class SeedingServiceExtensions
{
    public static async Task SeedDataAsync(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IAivoraDataSeeder>();
        await seeder.SeedAsync();
    }

    public static IServiceCollection AddAivoraDataSeeder(this IServiceCollection services)
    {
        services.AddScoped<IAivoraDataSeeder, AivoraDataSeeder>();
        return services;
    }
}