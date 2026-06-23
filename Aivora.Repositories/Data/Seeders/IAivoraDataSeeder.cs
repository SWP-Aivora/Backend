using System.Threading.Tasks;

namespace Aivora.Repositories;

public interface IAivoraDataSeeder
{
    Task SeedAsync();
}