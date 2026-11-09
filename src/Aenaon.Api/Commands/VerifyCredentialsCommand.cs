using Aenaon.Application.Identity;
using Aenaon.Infrastructure.Database;

namespace Aenaon.Api.Commands;

/// <summary>Δοκιμάζει διαδραστικά τον Identity κωδικό στο Development χωρίς έκδοση session.</summary>
public static class VerifyCredentialsCommand
{
    public static async Task<int> ExecuteAsync(string[] args, ICredentialVerifier verifier,
        IDatabaseSchemaManager schemaManager, TextWriter output, TextWriter error,
        Func<string?> readPassword, bool interactive, CancellationToken cancellationToken)
    {
        if (args is not ["--username", var userName, "--confirm-database", var confirmedDatabase] ||
            string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(confirmedDatabase))
        {
            await error.WriteLineAsync("Χρήση: --verify-credentials --username NAME --confirm-database DATABASE.");
            return 2;
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
                !string.Equals(schema.DatabaseName, confirmedDatabase, StringComparison.Ordinal))
            {
                await error.WriteLineAsync("Δεν επιβεβαιώθηκε ενημερωμένη βάση με το απαιτούμενο ακριβές όνομα.");
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

            var result = await verifier.VerifyAsync(userName, password, cancellationToken);
            if (result.Status == CredentialVerificationStatus.Authenticated)
            {
                await output.WriteLineAsync($"UserId: {result.UserId}");
                await output.WriteLineAsync("Ο κωδικός επαληθεύτηκε επιτυχώς. Δεν δημιουργήθηκε session.");
                return 0;
            }

            await error.WriteLineAsync("Αποτυχία επαλήθευσης. Έλεγξε τον κωδικό ή το προσωρινό κλείδωμα.");
            return 2;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await error.WriteLineAsync("Ο έλεγχος ακυρώθηκε.");
            return 130;
        }
    }
}
