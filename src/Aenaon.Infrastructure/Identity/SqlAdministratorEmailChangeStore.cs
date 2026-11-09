using Aenaon.Application.Identity;
using Aenaon.Domain.Tenancy;
using Aenaon.Infrastructure.Database;
using Aenaon.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aenaon.Infrastructure.Identity;

/// <summary>Διορθώνει με Identity το δοκιμαστικό email μόνο μετά από έλεγχο κωδικού Owner.</summary>
public sealed class SqlAdministratorEmailChangeStore(
    string? connectionString,
    bool isDevelopment,
    IDatabaseSchemaManager schemaManager,
    AenaonDbContext context,
    UserManager<AenaonUser> userManager,
    ICredentialVerifier verifier,
    ILogger<SqlAdministratorEmailChangeStore> logger) : IAdministratorEmailChangeStore
{
    private const string PlaceholderEmail = "your-email@example.com";

    public async Task<AdministratorEmailChangeResult> ExecuteAsync(Guid tenantId, Guid userId,
        string email, string? password, bool apply, string? confirmedDatabase,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return new(AdministratorEmailChangeStatus.NotConfigured);
        }

        SqlConnectionStringBuilder settings;
        try
        {
            settings = SqlConnectionPolicy.CreateBuilder(connectionString, isDevelopment);
        }
        catch (ArgumentException)
        {
            logger.LogWarning("Administrator email change SQL configuration rejected");
            return new(AdministratorEmailChangeStatus.InvalidConfiguration);
        }

        if (!isDevelopment || (apply &&
            !string.Equals(settings.InitialCatalog, confirmedDatabase, StringComparison.Ordinal)))
        {
            return new(AdministratorEmailChangeStatus.InvalidConfiguration);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        try
        {
            var schema = await schemaManager.ExecuteAsync(false, null, timeout.Token);
            if (schema.Status != DatabaseSchemaStatus.Ready || schema.PendingMigrations?.Count != 0)
            {
                return new(AdministratorEmailChangeStatus.SchemaNotReady);
            }

            // Η εντολή είναι σκόπιμα μιας χρήσης και μόνο για τον αρχικό μοναδικό Owner.
            if (await context.Users.CountAsync(timeout.Token) != 1 ||
                await context.TenantMemberships.CountAsync(timeout.Token) != 1 ||
                !await context.TenantMemberships.AnyAsync(item =>
                    item.UserId == userId && item.TenantId == tenantId &&
                    item.Role == TenantMembershipRole.Owner, timeout.Token))
            {
                return new(AdministratorEmailChangeStatus.TargetNotEligible);
            }

            AenaonUser? user = await userManager.FindByIdAsync(userId.ToString());
            if (user is null || !string.Equals(user.UserName, "admin", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(user.Email, PlaceholderEmail, StringComparison.OrdinalIgnoreCase))
            {
                return new(AdministratorEmailChangeStatus.TargetNotEligible);
            }

            if (!apply)
            {
                logger.LogInformation("Administrator email correction preview completed without changes");
                return new(AdministratorEmailChangeStatus.Ready, settings.InitialCatalog, userId);
            }

            CredentialVerificationResult verified =
                await verifier.VerifyAsync(user.UserName, password, timeout.Token);
            if (verified.Status != CredentialVerificationStatus.Authenticated || verified.UserId != userId)
            {
                logger.LogWarning("Administrator email correction credential verification rejected");
                return new(AdministratorEmailChangeStatus.InvalidCredentials);
            }

            timeout.Token.ThrowIfCancellationRequested();
            IdentityResult updated = await userManager.SetEmailAsync(user, email);
            if (!updated.Succeeded)
            {
                logger.LogWarning("Administrator email correction rejected by Identity");
                return new(AdministratorEmailChangeStatus.InvalidInput);
            }

            logger.LogInformation("Administrator email corrected for {UserId}; email confirmation remains required", userId);
            return new(AdministratorEmailChangeStatus.Updated, settings.InitialCatalog, userId);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Administrator email correction timed out");
            return new(AdministratorEmailChangeStatus.TimedOut);
        }
        catch (SqlException error)
        {
            logger.LogWarning("Administrator email correction SQL error {SqlErrorNumber}", error.Number);
            return new(AdministratorEmailChangeStatus.Unavailable, SqlErrorNumber: error.Number);
        }
        catch (DbUpdateException error) when (error.InnerException is SqlException sqlError)
        {
            logger.LogWarning("Administrator email correction SQL error {SqlErrorNumber}", sqlError.Number);
            return new(AdministratorEmailChangeStatus.Unavailable, SqlErrorNumber: sqlError.Number);
        }
    }
}
