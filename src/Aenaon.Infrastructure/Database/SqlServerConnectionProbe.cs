using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Aenaon.Infrastructure.Database;

/// <summary>Ελέγχει τη σύνδεση με ένα σταθερό ερώτημα ανάγνωσης και περιορισμένο χρόνο εκτέλεσης.</summary>
public sealed class SqlServerConnectionProbe(string? connectionString, bool allowUntrustedCertificate,
    ILogger<SqlServerConnectionProbe> logger) : IDatabaseConnectionProbe
{
    public async Task<DatabaseConnectionResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return new(DatabaseConnectionStatus.NotConfigured);
        }

        SqlConnectionStringBuilder settings;
        try
        {
            settings = SqlConnectionPolicy.CreateBuilder(connectionString, allowUntrustedCertificate);
        }
        catch (ArgumentException)
        {
            logger.LogWarning("Database connection configuration rejected");
            return new(DatabaseConnectionStatus.InvalidConfiguration);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        try
        {
            await using var connection = new SqlConnection(settings.ConnectionString);
            await connection.OpenAsync(timeout.Token);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT CONVERT(int, compatibility_level) FROM sys.databases WHERE database_id = DB_ID();";
            command.CommandTimeout = 3;
            object? result = await command.ExecuteScalarAsync(timeout.Token);
            if (result is not int level)
            {
                logger.LogWarning("Database connectivity probe returned no metadata");
                return new(DatabaseConnectionStatus.Unavailable);
            }

            logger.LogInformation("Database connectivity verified at compatibility level {CompatibilityLevel}", level);
            return new(DatabaseConnectionStatus.Connected, level);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Database connectivity probe timed out");
            return new(DatabaseConnectionStatus.TimedOut);
        }
        catch (SqlException error)
        {
            logger.LogWarning("Database connectivity failed with SQL error {SqlErrorNumber}", error.Number);
            return new(DatabaseConnectionStatus.Unavailable, SqlErrorNumber: error.Number);
        }
        catch (InvalidOperationException)
        {
            logger.LogWarning("Database connection could not be opened");
            return new(DatabaseConnectionStatus.Unavailable);
        }
        catch (ArgumentException)
        {
            logger.LogWarning("Database configuration rejected by the driver");
            return new(DatabaseConnectionStatus.InvalidConfiguration);
        }
    }
}
