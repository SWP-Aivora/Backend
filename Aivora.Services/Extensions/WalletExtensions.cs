using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Services.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Aivora.Services.Extensions;

public static class WalletExtensions
{
    public static async Task<Wallet> GetWalletForUpdateAsync(this AivoraDbContext dbContext, Guid userId)
    {
        Wallet? wallet = null;
        var provider = dbContext.Database.ProviderName;
        if (provider == "Microsoft.EntityFrameworkCore.InMemory" || provider == null || provider.Contains("InMemory"))
        {
            wallet = await dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
        }
        else if (provider.Contains("Sqlite") || provider.Contains("SQLite"))
        {
            wallet = await dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
        }
        else if (provider.Contains("SqlServer") || provider.Contains("Microsoft.Data.SqlClient"))
        {
            wallet = await dbContext.Wallets.FromSqlRaw("SELECT * FROM \"Wallets\" WITH (UPDLOCK, ROWLOCK) WHERE \"UserId\" = {0}", userId).FirstOrDefaultAsync();
        }
        else // default to PostgreSQL syntax
        {
            wallet = await dbContext.Wallets.FromSqlRaw("SELECT * FROM \"Wallets\" WHERE \"UserId\" = {0} FOR UPDATE", userId).FirstOrDefaultAsync();
        }
        return wallet ?? throw new NotFoundException($"Wallet for user {userId} not found.");
    }
}
