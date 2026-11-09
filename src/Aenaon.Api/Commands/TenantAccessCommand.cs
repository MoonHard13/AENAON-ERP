using Aenaon.Application.Identity;
using Aenaon.Application.Tenancy;
using Aenaon.Infrastructure.Database;

namespace Aenaon.Api.Commands;

/// <summary>Ελέγχει δικαιώματα επιχείρησης/καταστήματος μετά από επαλήθευση κωδικού στο Development.</summary>
public static class TenantAccessCommand
{
    public static async Task<int> ExecuteAsync(string[] args, ICredentialVerifier verifier,
        TenantAuthorizationService authorization, IDatabaseSchemaManager schemaManager,
        TextWriter output, TextWriter error, Func<string?> readPassword, bool interactive,
        CancellationToken cancellationToken)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        var accepted = new HashSet<string>(StringComparer.Ordinal)
        {
            "--username", "--tenant-id", "--store-id", "--permission", "--confirm-database"
        };
        for (int index = 0; index < args.Length; index++)
        {
            if (accepted.Contains(args[index]) && index + 1 < args.Length &&
                options.TryAdd(args[index], args[index + 1]))
            {
                index++;
            }
            else
            {
                return await InvalidAsync(error);
            }
        }

        if (!options.TryGetValue("--username", out string? userName) ||
            string.IsNullOrWhiteSpace(userName) ||
            !options.TryGetValue("--tenant-id", out string? rawTenant) ||
            !Guid.TryParse(rawTenant, out Guid tenantId) || tenantId == Guid.Empty ||
            !options.TryGetValue("--permission", out string? rawPermission) ||
            !Enum.TryParse(rawPermission, true, out TenantPermission permission) ||
            !Enum.IsDefined(permission) ||
            !options.TryGetValue("--confirm-database", out string? databaseName) ||
            string.IsNullOrWhiteSpace(databaseName))
        {
            return await InvalidAsync(error);
        }

        Guid? storeId = null;
        if (options.TryGetValue("--store-id", out string? rawStore))
        {
            if (!Guid.TryParse(rawStore, out Guid parsedStore) || parsedStore == Guid.Empty)
            {
                return await InvalidAsync(error);
            }
            storeId = parsedStore;
        }

        if (!interactive)
        {
            await error.WriteLineAsync("Απαιτείται διαδραστικό τερματικό χωρίς ανακατεύθυνση stdin.");
            return 2;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var schema = await schemaManager.ExecuteAsync(false, null, cancellationToken);
            if (schema.Status != DatabaseSchemaStatus.Ready ||
                schema.PendingMigrations?.Count != 0 ||
                !string.Equals(schema.DatabaseName, databaseName, StringComparison.Ordinal))
            {
                await error.WriteLineAsync("Δεν επιβεβαιώθηκε ενημερωμένη βάση με το ακριβές απαιτούμενο όνομα.");
                return 2;
            }

            await output.WriteAsync("Κωδικός λογαριασμού: ");
            string? password = readPassword();
            await output.WriteLineAsync();
            if (password is null)
            {
                await error.WriteLineAsync("Ο έλεγχος ακυρώθηκε.");
                return 130;
            }

            var credentials = await verifier.VerifyAsync(userName, password, cancellationToken);
            if (credentials.Status != CredentialVerificationStatus.Authenticated ||
                credentials.UserId is not Guid authenticatedUserId)
            {
                await error.WriteLineAsync("Η επαλήθευση λογαριασμού απέτυχε.");
                return 2;
            }

            var result = await authorization.CheckAsync(authenticatedUserId, tenantId, storeId,
                permission, cancellationToken);
            if (result.Status != TenantAccessStatus.Granted)
            {
                await error.WriteLineAsync("Η πρόσβαση στον επιλεγμένο πόρο δεν επιτρέπεται.");
                return 3;
            }

            await output.WriteLineAsync($"UserId: {authenticatedUserId}");
            await output.WriteLineAsync($"TenantId: {tenantId}");
            if (storeId.HasValue)
            {
                await output.WriteLineAsync($"StoreId: {storeId}");
            }
            await output.WriteLineAsync($"Ρόλος: {result.Role}");
            await output.WriteLineAsync($"Δικαίωμα: {permission} — επιτρέπεται.");
            await output.WriteLineAsync("Πραγματοποιήθηκε μόνο έλεγχος πρόσβασης. Δεν δημιουργήθηκε session.");
            return 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await error.WriteLineAsync("Ο έλεγχος ακυρώθηκε.");
            return 130;
        }
    }

    private static async Task<int> InvalidAsync(TextWriter error)
    {
        await error.WriteLineAsync(
            "Χρήση: --check-tenant-access --username NAME --tenant-id GUID [--store-id GUID] --permission Read|Manage --confirm-database NAME.");
        return 2;
    }
}
