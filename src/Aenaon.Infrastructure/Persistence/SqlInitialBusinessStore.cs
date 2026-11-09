using System.Data;
using Aenaon.Application.Tenancy;
using Aenaon.Domain.Tenancy;
using Aenaon.Infrastructure.Database;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Aenaon.Infrastructure.Persistence;

public sealed class SqlInitialBusinessStore(string? connectionString, bool isDevelopment,
    IDatabaseSchemaManager schemaManager, ILogger<SqlInitialBusinessStore> logger) : IInitialBusinessStore
{
    public async Task<InitialBusinessResult> ExecuteAsync(Tenant tenant, bool apply, string? confirmedDatabase,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return new(InitialBusinessStatus.NotConfigured);
        }
        SqlConnectionStringBuilder settings;
        try
        {
            settings = SqlConnectionPolicy.CreateBuilder(connectionString, isDevelopment);
        }
        catch (ArgumentException)
        {
            logger.LogWarning("Initial business database configuration rejected");
            return new(InitialBusinessStatus.InvalidConfiguration);
        }
        if (!isDevelopment || (apply && !string.Equals(settings.InitialCatalog, confirmedDatabase, StringComparison.Ordinal)) ||
            tenant.Stores.Count != 1)
        {
            logger.LogWarning("Initial business environment, confirmation or aggregate rejected");
            return new(InitialBusinessStatus.InvalidConfiguration);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            var schema = await schemaManager.ExecuteAsync(false, null, timeout.Token);
            if (schema.Status != DatabaseSchemaStatus.Ready || schema.PendingMigrations?.Count != 0)
            {
                return schema.Status switch
                {
                    DatabaseSchemaStatus.Unavailable => new(InitialBusinessStatus.Unavailable, SqlErrorNumber: schema.SqlErrorNumber),
                    DatabaseSchemaStatus.TimedOut => new(InitialBusinessStatus.TimedOut),
                    _ => new(InitialBusinessStatus.SchemaNotReady)
                };
            }

            await using var context = new AenaonDbContext(AenaonDbContextOptions.Create(settings.ConnectionString, true));
            await context.Database.OpenConnectionAsync(timeout.Token);
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, timeout.Token);
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.Transaction = transaction.GetDbTransaction();
            command.CommandTimeout = 8;
            command.CommandText = """
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock
                    @Resource = N'AenaonERP.InitialBusiness', @LockMode = 'Exclusive',
                    @LockOwner = 'Transaction', @LockTimeout = 5000;
                SELECT @result;
                """;
            if (await command.ExecuteScalarAsync(timeout.Token) is not int lockResult || lockResult < 0)
            {
                logger.LogWarning("Initial business lock could not be acquired");
                return new(InitialBusinessStatus.Busy);
            }

            if (await context.Tenants.AnyAsync(timeout.Token) || await context.Stores.AnyAsync(timeout.Token))
            {
                logger.LogWarning("Initial business refused because business data already exists");
                return new(InitialBusinessStatus.ExistingData);
            }
            if (!apply)
            {
                logger.LogInformation("Initial business preview completed without data changes");
                return new(InitialBusinessStatus.Ready, DatabaseName: settings.InitialCatalog);
            }

            logger.LogInformation("Initial business creation started");
            context.Tenants.Add(tenant);
            await context.SaveChangesAsync(timeout.Token);
            await transaction.CommitAsync(timeout.Token);
            logger.LogInformation("Initial business created: {TenantId}, {StoreId}", tenant.Id, tenant.Stores[0].Id);
            return new(InitialBusinessStatus.Created, tenant.Id, tenant.Stores[0].Id, settings.InitialCatalog);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Initial business operation timed out");
            return new(InitialBusinessStatus.TimedOut);
        }
        catch (SqlException error)
        {
            return SqlFailure(error.Number);
        }
        catch (DbUpdateException error) when (error.InnerException is SqlException sqlError)
        {
            return SqlFailure(sqlError.Number);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        {
            logger.LogWarning("Initial business persistence configuration rejected");
            return new(InitialBusinessStatus.InvalidConfiguration);
        }
    }

    private InitialBusinessResult SqlFailure(int number)
    {
        logger.LogWarning("Initial business operation failed with SQL error {SqlErrorNumber}", number);
        return new(InitialBusinessStatus.Unavailable, SqlErrorNumber: number);
    }
}
