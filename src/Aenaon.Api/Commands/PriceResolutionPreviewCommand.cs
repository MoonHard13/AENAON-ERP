using System.Globalization;
using Aenaon.Application.Catalog;
using Aenaon.Application.Identity;
using Aenaon.Domain.Catalog;
using Aenaon.Infrastructure.Database;
using Microsoft.Extensions.Logging;

namespace Aenaon.Api.Commands;

/// <summary>Ασφαλής, μόνο-ανάγνωσης προεπισκόπηση εμπορικής τιμής μέσω CLI.</summary>
public static class PriceResolutionPreviewCommand
{
    public static async Task<int> ExecuteAsync(string[] args,
        ICredentialVerifier verifier, PriceResolutionService service,
        IDatabaseSchemaManager schema, ILogger logger, TextWriter output,
        TextWriter error, Func<string?> readPassword, bool interactive,
        CancellationToken token = default)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        bool dryRun = false;
        bool valid = interactive;

        for (int i = 0; i < args.Length && valid;)
        {
            string key = args[i++];
            if (key == "--dry-run")
            {
                valid = !dryRun;
                dryRun = true;
            }
            else if (key is "--username" or "--tenant-id" or "--item-id" or
                         "--store-id" or "--channel" or "--business-date" or "--confirm-database")
            {
                valid = i < args.Length && !args[i].StartsWith("--", StringComparison.Ordinal)
                    && values.TryAdd(key, args[i++]);
            }
            else
            {
                valid = false;
            }
        }

        Guid tenantId = Guid.Empty;
        Guid itemId = Guid.Empty;
        PriceListChannel channel = PriceListChannel.All;
        DateOnly businessDate = default;

        valid = valid && dryRun &&
            !string.IsNullOrWhiteSpace(values.GetValueOrDefault("--username")) &&
            !string.IsNullOrWhiteSpace(values.GetValueOrDefault("--confirm-database")) &&
            Guid.TryParse(values.GetValueOrDefault("--tenant-id"), out tenantId) &&
            tenantId != Guid.Empty &&
            Guid.TryParse(values.GetValueOrDefault("--item-id"), out itemId) &&
            itemId != Guid.Empty &&
            Enum.TryParse(values.GetValueOrDefault("--channel"), false, out channel) &&
            Enum.IsDefined(channel) &&
            DateOnly.TryParseExact(values.GetValueOrDefault("--business-date"),
                "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None,
                out businessDate);

        Guid? storeId = null;
        if (values.TryGetValue("--store-id", out string? rawStore))
        {
            if (Guid.TryParse(rawStore, out Guid parsed) && parsed != Guid.Empty)
            {
                storeId = parsed;
            }
            else
            {
                valid = false;
            }
        }

        if (!valid)
        {
            await error.WriteLineAsync(
                "Χρήση: --resolve-price --username NAME --tenant-id GUID --item-id GUID " +
                "[--store-id GUID] --channel All|DineIn|Takeaway|Delivery|Retail|Online " +
                "--business-date yyyy-MM-dd --confirm-database NAME --dry-run. " +
                "Μόνο Development και διαδραστικό τερματικό. Καμία εγγραφή SQL.");
            return 2;
        }

        try
        {
            DatabaseSchemaResult state = await schema.ExecuteAsync(false, null, token);
            if (state.Status != DatabaseSchemaStatus.Ready ||
                state.PendingMigrations?.Count != 0 ||
                !string.Equals(state.DatabaseName,
                    values["--confirm-database"], StringComparison.Ordinal))
            {
                await error.WriteLineAsync("Η βάση δεν είναι ενημερωμένη ή δεν ταιριάζει με την επιβεβαίωση.");
                return 2;
            }

            await output.WriteAsync("Κωδικός λογαριασμού: ");
            string? password = readPassword();
            await output.WriteLineAsync();
            if (password is null)
            {
                return 130;
            }

            CredentialVerificationResult verified = await verifier.VerifyAsync(
                values["--username"], password, token);
            if (verified.Status != CredentialVerificationStatus.Authenticated ||
                verified.UserId is not Guid userId)
            {
                logger.LogWarning("Price resolution CLI authentication failed");
                await error.WriteLineAsync("Η επαλήθευση λογαριασμού απέτυχε.");
                return 3;
            }

            PriceResolutionResult result = await service.ResolveAsync(
                userId, tenantId, itemId, storeId, channel, businessDate, token);

            logger.LogInformation(
                "Price resolution preview {Status} by {UserId} tenant {TenantId} item {ItemId} channel {Channel}",
                result.Status, userId, tenantId, itemId, channel);
            if (result.Status == PriceResolutionStatus.Resolved)
            {
                await output.WriteLineAsync(
                    $"Εμπορική τιμή: {result.UnitPrice!.Value.ToString("F4", CultureInfo.InvariantCulture)} " +
                    $"{result.Currency}, {(result.PricesIncludeVat == true ? "με ΦΠΑ" : "χωρίς ΦΠΑ")}. " +
                    $"Τιμοκατάλογος: {result.PriceListCode} ({result.PriceListId:D}).");
                await output.WriteLineAsync("Προεπισκόπηση μόνο. Δεν υπολογίζεται ΦΠΑ και δεν εκδίδεται παραστατικό.");
                return 0;
            }

            if (result.Status == PriceResolutionStatus.Ambiguous)
            {
                await error.WriteLineAsync("Αμφίσημη τιμή: υπάρχουν πολλοί ισόβαθμοι τιμοκατάλογοι. " +
                    "Δεν επιλέχθηκε αυθαίρετη τιμή.");
                foreach (Guid id in result.ConflictingPriceListIds ?? [])
                {
                    await error.WriteLineAsync($"Συγκρουόμενος τιμοκατάλογος: {id:D}");
                }
                return 5;
            }

            await error.WriteLineAsync(result.Status switch
            {
                PriceResolutionStatus.Denied => "Δεν επιτρέπεται η πρόσβαση σε αυτή την επιχείρηση ή το κατάστημα.",
                PriceResolutionStatus.ItemUnavailable => "Το είδος δεν υπάρχει ή είναι ανενεργό.",
                PriceResolutionStatus.NotPriced => "Δεν βρέθηκε έγκυρη τιμή για ημερομηνία, κατάστημα και κανάλι.",
                _ => "Μη έγκυρα στοιχεία προεπισκόπησης."
            });
            return result.Status == PriceResolutionStatus.Denied ? 3 :
                result.Status == PriceResolutionStatus.Invalid ? 2 : 4;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            await error.WriteLineAsync("Η προεπισκόπηση ακυρώθηκε.");
            return 130;
        }
    }
}
