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
                await EnsureMigrationHistoryTableAsync(context, logger);
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

    /// <summary>
    /// Ensures the __EFMigrationsHistory table exists and is populated with records
    /// for migrations whose schema changes are already present in the database.
    /// This handles the case where the database was initially created via
    /// EnsureCreatedAsync() (which does not create the migrations history table).
    /// </summary>
    private static async Task EnsureMigrationHistoryTableAsync(
        Aivora.Repositories.Data.AivoraDbContext context,
        ILogger logger)
    {
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();

        try
        {
            // Check if __EFMigrationsHistory table exists
            var historyExists = await TableExistsAsync(connection, "__EFMigrationsHistory");

            if (historyExists)
            {
                logger.LogInformation("Migrations history table already exists — skipping seeding");
                return;
            }

            logger.LogWarning(
                "__EFMigrationsHistory table not found. Database was likely created via " +
                "EnsureCreatedAsync(). Seeding migration history from existing schema...");

            await SeedMigrationHistoryFromSchemaAsync(connection, logger);
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    /// <summary>
    /// Inspects the current database schema to determine which migrations have
    /// already been effectively applied, then inserts corresponding records into
    /// __EFMigrationsHistory so that MigrateAsync() only runs pending migrations.
    /// </summary>
    private static async Task SeedMigrationHistoryFromSchemaAsync(
        System.Data.Common.DbConnection connection,
        ILogger logger)
    {
        // Migration IDs and the corresponding schema check.
        // Each entry: (MigrationId, ProductVersion, check_sql, description)
        var migrationChecks = new (string Id, string Version, string CheckSql, string Description)[]
        {
            // 1. Initial migration — creates Users, Categories, Skills, JobPosts, etc.
            ("20260531075908_Initial", "10.0.8",
             "SELECT 1 FROM information_schema.tables WHERE table_name = 'Users'",
             "Initial schema (Users table)"),

            // 2. AddAIJobAssistantStructuredFields — adds SuggestedBudgetType, etc.
            ("20260602000000_AddAIJobAssistantStructuredFields", "10.0.8",
             "SELECT 1 FROM information_schema.columns WHERE table_name = 'AIJobSuggestions' AND column_name = 'SuggestedBudgetType'",
             "AIJobSuggestions structured fields (SuggestedBudgetType)"),

            // 3. AddNotificationsTable — creates Notifications table
            ("20260602152032_AddNotificationsTable", "10.0.8",
             "SELECT 1 FROM information_schema.tables WHERE table_name = 'Notifications'",
             "Notifications table"),

            // 4. AddJobPostMilestonesTable — creates JobPostMilestones table
            ("20260610075847_AddJobPostMilestonesTable", "10.0.8",
             "SELECT 1 FROM information_schema.tables WHERE table_name = 'JobPostMilestones'",
             "JobPostMilestones table"),

            // 5. AddDueDaysAndAcceptanceCriteriaToJobPostMilestones — adds DueDays, AcceptanceCriteria
            ("20260611101158_AddDueDaysAndAcceptanceCriteriaToJobPostMilestones", "10.0.8",
             "SELECT 1 FROM information_schema.columns WHERE table_name = 'JobPostMilestones' AND column_name = 'DueDays'",
             "JobPostMilestones DueDays column"),
        };

        foreach (var (migrationId, version, checkSql, description) in migrationChecks)
        {
            var alreadyApplied = await CheckExistsAsync(connection, checkSql);

            if (alreadyApplied)
            {
                await InsertMigrationRecordAsync(connection, migrationId, version);
                logger.LogInformation(
                    "Migration '{MigrationId}' ({Description}) marked as applied (schema already present)",
                    migrationId, description);
            }
            else
            {
                logger.LogWarning(
                    "Migration '{MigrationId}' ({Description}) schema NOT detected — will be applied by MigrateAsync()",
                    migrationId, description);
            }
        }
    }

    private static async Task<bool> TableExistsAsync(
        System.Data.Common.DbConnection connection, string tableName)
    {
        const string sql =
            "SELECT 1 FROM information_schema.tables WHERE table_name = @tableName";
        return await CheckExistsAsync(connection, sql, ("@tableName", tableName));
    }

    private static async Task<bool> CheckExistsAsync(
        System.Data.Common.DbConnection connection,
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            var param = cmd.CreateParameter();
            param.ParameterName = name;
            param.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(param);
        }
        var result = await cmd.ExecuteScalarAsync();
        return result is not null && result != DBNull.Value;
    }

    private static async Task InsertMigrationRecordAsync(
        System.Data.Common.DbConnection connection,
        string migrationId,
        string productVersion)
    {
        const string sql = @"
            INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
            VALUES (@migrationId, @productVersion)
            ON CONFLICT DO NOTHING";

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        var p1 = cmd.CreateParameter();
        p1.ParameterName = "@migrationId";
        p1.Value = migrationId;
        cmd.Parameters.Add(p1);
        var p2 = cmd.CreateParameter();
        p2.ParameterName = "@productVersion";
        p2.Value = productVersion;
        cmd.Parameters.Add(p2);
        await cmd.ExecuteNonQueryAsync();
    }
}
