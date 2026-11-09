using Aenaon.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aenaon.Infrastructure.Database;

/// <summary>Ελέγχει και εφαρμόζει μόνο προς τα εμπρός migrations σε υπάρχουσα βάση ανάπτυξης.</summary>
public sealed class DatabaseSchemaManager(string? connectionString, bool isDevelopment,
    ILogger<DatabaseSchemaManager> logger) : IDatabaseSchemaManager
{
    public async Task<DatabaseSchemaResult> ExecuteAsync(bool apply, string? confirmedDatabase,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return new(DatabaseSchemaStatus.NotConfigured);
        }

        SqlConnectionStringBuilder settings;
        try
        {
            settings = SqlConnectionPolicy.CreateBuilder(connectionString, isDevelopment);
        }
        catch (ArgumentException)
        {
            logger.LogWarning("Database schema configuration rejected");
            return new(DatabaseSchemaStatus.InvalidConfiguration);
        }
        if (!isDevelopment || (apply && !string.Equals(settings.InitialCatalog, confirmedDatabase, StringComparison.Ordinal)))
        {
            logger.LogWarning("Database migration environment or confirmation rejected");
            return new(DatabaseSchemaStatus.InvalidConfiguration);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        try
        {
            await using var context = new AenaonDbContext(AenaonDbContextOptions.Create(settings.ConnectionString, isDevelopment));
            // Η σύνδεση ανοίγει πρώτα στην ήδη υπάρχουσα βάση: δεν επιτρέπεται αυτόματη δημιουργία βάσης.
            await context.Database.OpenConnectionAsync(timeout.Token);
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandTimeout = 5;
            command.CommandText = "SELECT CONVERT(int, compatibility_level) FROM sys.databases WHERE database_id = DB_ID();";
            if (await command.ExecuteScalarAsync(timeout.Token) is not int compatibility || compatibility != 160)
            {
                logger.LogWarning("Database schema requires compatibility level 160");
                return new(DatabaseSchemaStatus.UnsafeDatabase);
            }

            string[] known = context.Database.GetMigrations().ToArray();
            string[] applied = (await context.Database.GetAppliedMigrationsAsync(timeout.Token)).ToArray();
            if (known.Length == 0 || !applied.SequenceEqual(known.Take(applied.Length), StringComparer.Ordinal))
            {
                logger.LogWarning("Database migration history is incompatible with this application version");
                return new(DatabaseSchemaStatus.UnsafeDatabase);
            }

            if (applied.Length == 0)
            {
                command.CommandText = """
                    SELECT COUNT_BIG(*) FROM sys.objects AS o
                    JOIN sys.schemas AS s ON s.schema_id = o.schema_id
                    WHERE o.is_ms_shipped = 0 AND o.type IN ('U', 'V', 'P', 'FN', 'IF', 'TF', 'SO')
                      AND NOT (s.name = N'dbo' AND o.name = N'__EFMigrationsHistory');
                    """;
                if (await command.ExecuteScalarAsync(timeout.Token) is not long count || count != 0)
                {
                    logger.LogWarning("Initial migration refused because the database contains untracked objects");
                    return new(DatabaseSchemaStatus.UnsafeDatabase);
                }
            }

            string[] pending = known.Skip(applied.Length).ToArray();
            logger.LogInformation("Database schema inspected: {AppliedCount} applied and {PendingCount} pending migrations",
                applied.Length, pending.Length);
            if (!apply || pending.Length == 0)
            {
                return new(DatabaseSchemaStatus.Ready, settings.InitialCatalog, applied, pending);
            }

            foreach (string migration in pending)
            {
                logger.LogInformation("Preparing database migration {MigrationId}", migration);
            }
            await context.Database.MigrateAsync(timeout.Token);
            applied = (await context.Database.GetAppliedMigrationsAsync(timeout.Token)).ToArray();
            logger.LogInformation("Database migrations completed: {MigrationCount}", pending.Length);
            return new(DatabaseSchemaStatus.Updated, settings.InitialCatalog, applied, []);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Database schema operation timed out");
            return new(DatabaseSchemaStatus.TimedOut);
        }
        catch (SqlException error)
        {
            logger.LogWarning("Database schema operation failed with SQL error {SqlErrorNumber}", error.Number);
            return new(DatabaseSchemaStatus.Unavailable, SqlErrorNumber: error.Number);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        {
            logger.LogWarning("Database migration configuration rejected by EF Core");
            return new(DatabaseSchemaStatus.InvalidConfiguration);
        }
    }
}
