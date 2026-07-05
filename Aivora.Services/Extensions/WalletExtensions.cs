using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Services.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Aivora.Services.Extensions;

public static class WalletExtensions
{
    public static async Task<Wallet> GetWalletForUpdateAsync(this AivoraDbContext dbContext, Guid userId)
    {
        Wallet? wallet = null;
        var provider = dbContext.Database.ProviderName;

        if (provider == "Microsoft.EntityFrameworkCore.InMemory" || provider == null || provider.Contains("InMemory") || provider.Contains("Sqlite") || provider.Contains("SQLite"))
        {
            wallet = await dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
        }
        else
        {
            // Get table name and schema dynamically from EF Core Metadata
            var entityType = dbContext.Model.FindEntityType(typeof(Wallet));
            var tableName = entityType?.GetTableName() ?? "Wallets";
            var schema = entityType?.GetSchema();
            
            // Format table identifier (e.g. "dbo"."Wallets" or just "Wallets")
            var fullTableName = string.IsNullOrEmpty(schema) ? $"\"{tableName}\"" : $"\"{schema}\".\"{tableName}\"";

            // Get column name for UserId dynamically
            var storeObject = StoreObjectIdentifier.Table(tableName, schema);
            var userIdProperty = entityType?.FindProperty(nameof(Wallet.UserId));
            var userIdColumnName = userIdProperty?.GetColumnName(storeObject) ?? "UserId";
            var escapedColumnName = $"\"{userIdColumnName}\"";

            if (provider.Contains("SqlServer") || provider.Contains("Microsoft.Data.SqlClient"))
            {
                var query = $"SELECT * FROM {fullTableName} WITH (UPDLOCK, ROWLOCK) WHERE {escapedColumnName} = {{0}}";
                wallet = await dbContext.Wallets.FromSqlRaw(query, userId).FirstOrDefaultAsync();
            }
            else // default to PostgreSQL syntax
            {
                var query = $"SELECT * FROM {fullTableName} WHERE {escapedColumnName} = {{0}} FOR UPDATE";
                wallet = await dbContext.Wallets.FromSqlRaw(query, userId).FirstOrDefaultAsync();
            }
        }

        return wallet ?? throw new NotFoundException($"Wallet for user {userId} not found.");
    }
}
