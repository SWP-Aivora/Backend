using System.Threading.Tasks;

namespace Aivora.Repositories;

public interface IAivoraDataSeeder
{
    /// <summary>
    /// Ensures the system platform user + its wallet exist. Must run in every
    /// environment (including Production) — Treasury's milestone payout path
    /// hard-depends on this wallet existing.
    /// </summary>
    Task EnsureSystemUserAsync();

    /// <summary>Seeds demo data (admin/client/expert accounts, sample jobs, etc).</summary>
    Task SeedAsync();
}