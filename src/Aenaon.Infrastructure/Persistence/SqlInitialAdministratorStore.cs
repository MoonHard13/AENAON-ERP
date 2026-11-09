using System.Data;
using Aenaon.Application.Identity;
using Aenaon.Domain.Tenancy;
using Aenaon.Infrastructure.Database;
using Aenaon.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aenaon.Infrastructure.Persistence;

/// <summary>Δημιουργεί τον πρώτο λογαριασμό και την ιδιοκτησία επιχείρησης στην ίδια συναλλαγή.</summary>
public sealed class SqlInitialAdministratorStore(
    string? connectionString,
    bool isDevelopment,
    IDatabaseSchemaManager schemaManager,
    IServiceScopeFactory scopeFactory,
    ILogger<SqlInitialAdministratorStore> logger) : IInitialAdministratorStore
{
    public async Task<InitialAdministratorResult> ExecuteAsync(Guid tenantId, string userName, string email,
        string? password, bool apply, string? confirmedDatabase, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return new(InitialAdministratorStatus.NotConfigured);
        }

        SqlConnectionStringBuilder settings;
        try
        {
            settings = SqlConnectionPolicy.CreateBuilder(connectionString, isDevelopment);
        }
        catch (ArgumentException)
        {
            logger.LogWarning("Initial administrator connection configuration rejected");
            return new(InitialAdministratorStatus.InvalidConfiguration);
        }

        if (!isDevelopment || (apply &&
            !string.Equals(settings.InitialCatalog, confirmedDatabase, StringComparison.Ordinal)))
        {
            logger.LogWarning("Initial administrator environment or database confirmation rejected");
            return new(InitialAdministratorStatus.InvalidConfiguration);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        try
        {
            var schema = await schemaManager.ExecuteAsync(false, null, timeout.Token);
            if (schema.Status != DatabaseSchemaStatus.Ready || schema.PendingMigrations?.Count != 0)
            {
                return schema.Status switch
                {
                    DatabaseSchemaStatus.Unavailable => new(InitialAdministratorStatus.Unavailable,
                        SqlErrorNumber: schema.SqlErrorNumber),
                    DatabaseSchemaStatus.TimedOut => new(InitialAdministratorStatus.TimedOut),
                    _ => new(InitialAdministratorStatus.SchemaNotReady)
                };
            }

            await using var scope = scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<AenaonDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AenaonUser>>();
            await context.Database.OpenConnectionAsync(timeout.Token);
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, timeout.Token);
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.Transaction = transaction.GetDbTransaction();
            command.CommandTimeout = 8;
            command.CommandText = """
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock
                    @Resource = N'AenaonERP.InitialAdministrator', @LockMode = 'Exclusive',
                    @LockOwner = 'Transaction', @LockTimeout = 5000;
                SELECT @result;
                """;
            if (await command.ExecuteScalarAsync(timeout.Token) is not int lockResult || lockResult < 0)
            {
                logger.LogWarning("Initial administrator lock unavailable");
                return new(InitialAdministratorStatus.Busy);
            }

            if (await context.Users.AnyAsync(timeout.Token) || await context.TenantMemberships.AnyAsync(timeout.Token))
            {
                logger.LogWarning("Initial administrator refused because accounts or memberships exist");
                return new(InitialAdministratorStatus.ExistingUsers);
            }

            string? tenantName = await context.Tenants.AsNoTracking()
                .Where(tenant => tenant.Id == tenantId)
                .Select(tenant => tenant.Name)
                .SingleOrDefaultAsync(timeout.Token);
            if (tenantName is null)
            {
                logger.LogWarning("Initial administrator target tenant does not exist");
                return new(InitialAdministratorStatus.TenantNotFound);
            }

            if (!apply)
            {
                logger.LogInformation("Initial administrator preview completed without data changes");
                return new(InitialAdministratorStatus.Ready, settings.InitialCatalog, tenantName);
            }

            logger.LogInformation("Initial administrator creation started for {TenantId}", tenantId);
            var user = new AenaonUser { UserName = userName, Email = email };
            IdentityResult created = await userManager.CreateAsync(user, password!);
            if (!created.Succeeded)
            {
                bool passwordFailure = created.Errors.Any(error =>
                    error.Code.StartsWith("Password", StringComparison.Ordinal));
                logger.LogWarning("Initial administrator Identity validation rejected ({FailureType})",
                    passwordFailure ? "password" : "user");
                return new(passwordFailure ? InitialAdministratorStatus.PasswordRejected :
                    InitialAdministratorStatus.ValidationFailed);
            }

            timeout.Token.ThrowIfCancellationRequested();
            context.TenantMemberships.Add(TenantMembership.Create(tenantId, user.Id, TenantMembershipRole.Owner));
            await context.SaveChangesAsync(timeout.Token);
            await transaction.CommitAsync(timeout.Token);
            logger.LogInformation("Initial administrator created for {TenantId} with user {UserId}", tenantId, user.Id);
            return new(InitialAdministratorStatus.Created, settings.InitialCatalog, tenantName, user.Id);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Initial administrator operation timed out");
            return new(InitialAdministratorStatus.TimedOut);
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
            logger.LogWarning("Initial administrator persistence configuration rejected");
            return new(InitialAdministratorStatus.InvalidConfiguration);
        }
    }

    private InitialAdministratorResult SqlFailure(int number)
    {
        logger.LogWarning("Initial administrator SQL operation failed with code {SqlErrorNumber}", number);
        return new(InitialAdministratorStatus.Unavailable, SqlErrorNumber: number);
    }
}
