using Microsoft.EntityFrameworkCore;
using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Microsoft.Extensions.Configuration;

// Test reproduction script for duplicate email issue
class Program
{
    static async Task Main(string[] args)
    {
        // Setup configuration similar to the real app
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        // Setup in-memory DB (like test environment)
        var options = new DbContextOptionsBuilder<AivoraDbContext>()
            .UseInMemoryDatabase(databaseName: "TestDb")
            .Options;

        using var context = new AivoraDbContext(options);

        Console.WriteLine("=== Test 1: Run seeding with forceReset=true ===");
        try
        {
            await SeedData.Initialize(context, forceReset: true);
            var userCount1 = await context.Users.CountAsync();
            Console.WriteLine($"Users after seed 1: {userCount1}");

            Console.WriteLine("\n=== Test 2: Run seeding again with forceReset=true ===");
            await SeedData.Initialize(context, forceReset: true);
            var userCount2 = await context.Users.CountAsync();
            Console.WriteLine($"Users after seed 2: {userCount2}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error occurred: {ex.Message}");
            Console.WriteLine($"Inner exception: {ex.InnerException?.Message}");
        }

        Console.WriteLine("\n=== Test 3: Check existing users ===");
        var userCount3 = await context.Users.CountAsync();
        Console.WriteLine($"Current user count: {userCount3}");

        // Show existing users
        Console.WriteLine("\n=== Existing emails ===");
        var emails = await context.Users.Select(u => u.Email).ToListAsync();
        foreach (var email in emails)
        {
            Console.WriteLine($"- {email}");
        }
    }
}