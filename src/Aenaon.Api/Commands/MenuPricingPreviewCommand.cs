using System.Globalization;
using Aenaon.Application.Identity;
using Aenaon.Application.Menus;
using Aenaon.Domain.Catalog;
using Aenaon.Infrastructure.Database;
using Microsoft.Extensions.Logging;

namespace Aenaon.Api.Commands;

/// <summary>
/// Μόνο ανάγνωση προσχεδίου και πραγματικής τιμής ανά Store/Channel/BusinessDate.
/// Δεν πραγματοποιεί SQL εγγραφές ούτε δημοσίευση ή φορολογικές λειτουργίες.
/// </summary>
public static class MenuPricingPreviewCommand
{
    public static async Task<int> ExecuteAsync(string[] args,
        ICredentialVerifier verifier, MenuPreviewService service,
        IDatabaseSchemaManager schema, ILogger logger,
        TextWriter output, TextWriter error, Func<string?> readPassword,
        bool interactive, CancellationToken token = default)
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
            else if (key is "--username" or "--tenant-id" or "--menu-id" or
                         "--store-id" or "--channel" or "--business-date" or
                         "--confirm-database")
            {
                valid = i < args.Length && !args[i].StartsWith("--", StringComparison.Ordinal) &&
                    values.TryAdd(key, args[i++]);
            }
            else
            {
                valid = false;
            }
        }

        valid = valid && dryRun &&
            !string.IsNullOrWhiteSpace(values.GetValueOrDefault("--username")) &&
            !string.IsNullOrWhiteSpace(values.GetValueOrDefault("--confirm-database")) &&
            Guid.TryParse(values.GetValueOrDefault("--tenant-id"), out Guid tenantId) &&
            tenantId != Guid.Empty &&
            Guid.TryParse(values.GetValueOrDefault("--menu-id"), out Guid menuId) &&
            menuId != Guid.Empty &&
            Guid.TryParse(values.GetValueOrDefault("--store-id"), out Guid storeId) &&
            storeId != Guid.Empty &&
            Enum.TryParse(values.GetValueOrDefault("--channel"), false,
                out PriceListChannel channel) &&
            Enum.IsDefined(channel) && channel != PriceListChannel.All &&
            DateOnly.TryParseExact(values.GetValueOrDefault("--business-date"),
                "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None,
                out DateOnly businessDate);

        if (!valid)
        {
            await error.WriteLineAsync(
                "Χρήση: --preview-menu --username NAME --tenant-id GUID --menu-id GUID " +
                "--store-id GUID --channel DineIn|Takeaway|Delivery|Retail|Online " +
                "--business-date yyyy-MM-dd --confirm-database NAME --dry-run. " +
                "Development μόνο, διαδραστική επαλήθευση, καμία SQL μεταβολή.");
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
                await error.WriteLineAsync("Η βάση έχει εκκρεμείς migrations ή δεν συμφωνεί με την επιβεβαίωση.");
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
                logger.LogWarning("Menu pricing preview CLI authentication failed");
                await error.WriteLineAsync("Η επαλήθευση λογαριασμού απέτυχε.");
                return 3;
            }

            MenuPreviewResult result = await service.PreviewAsync(userId, tenantId, menuId,
                storeId, channel, businessDate, token);
            logger.LogInformation(
                "Menu pricing preview {Status} tenant {TenantId} menu {MenuId} store {StoreId} channel {Channel} date {Date}",
                result.Status, tenantId, menuId, storeId, channel, businessDate);
            if (result.Status != MenuPreviewStatus.Ready)
            {
                await error.WriteLineAsync(result.Status switch
                {
                    MenuPreviewStatus.Denied => "Δεν επιτρέπεται πρόσβαση στην επιχείρηση ή στο κατάστημα.",
                    MenuPreviewStatus.Missing => "Το προσχέδιο μενού δεν βρέθηκε στο επιλεγμένο Tenant.",
                    MenuPreviewStatus.TooLarge => "Το προσχέδιο υπερβαίνει το όριο προεπισκόπησης.",
                    _ => "Μη έγκυρα δεδομένα προεπισκόπησης."
                });
                return result.Status == MenuPreviewStatus.Denied ? 3 :
                    result.Status == MenuPreviewStatus.Invalid ? 2 : 4;
            }

            await output.WriteLineAsync(
                $"Προσχέδιο: {result.Menu!.Code} — {result.Menu.Name} | Αναθεώρηση {result.Menu.Revision}");
            await output.WriteLineAsync(
                $"Κατάστημα: {storeId:D} | Κανάλι: {channel} | Ημερομηνία: {businessDate:yyyy-MM-dd}");
            foreach (MenuPreviewItem item in result.Items ?? [])
            {
                string value = item.Status == MenuPreviewItemStatus.Resolved
                    ? $"{item.UnitPrice!.Value.ToString("F4", CultureInfo.InvariantCulture)} {item.Currency} " +
                      (item.PricesIncludeVat == true ? "(τιμή με ΦΠΑ)" : "(τιμή χωρίς ΦΠΑ)")
                    : $"ΑΠΟΚΛΕΙΣΜΕΝΟ: {item.Status}";
                await output.WriteLineAsync($"{item.Code} | {item.Name} | {value}");
            }
            await output.WriteLineAsync(
                $"Επιλύθηκαν: {result.ResolvedCount} | Αποκλεισμένα: {result.BlockedCount}");
            await output.WriteLineAsync(
                "Μόνο διάγνωση. Δεν υπολογίζεται νόμιμος ΦΠΑ, δεν εφαρμόζονται ωράρια και δεν πραγματοποιείται Publish/POS.");
            return result.HasBlockingIssues ? 5 : 0;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            await error.WriteLineAsync("Η προεπισκόπηση ακυρώθηκε.");
            return 130;
        }
    }
}
