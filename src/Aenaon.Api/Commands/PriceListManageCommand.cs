using System.Globalization;
using Aenaon.Application.Catalog;
using Aenaon.Application.Identity;
using Aenaon.Domain.Catalog;
using Aenaon.Domain.Exceptions;
using Aenaon.Infrastructure.Database;
using Microsoft.Extensions.Logging;

namespace Aenaon.Api.Commands;

/// <summary>Διαδραστική, δικαιωματική διαχείριση τιμοκαταλόγων από κοινό application service.</summary>
public static class PriceListManageCommand
{
    public static async Task<int> ExecuteAsync(string[] args,
        ICredentialVerifier verifier, PriceListManagementService service,
        IDatabaseSchemaManager schema, ILogger logger, TextWriter output,
        TextWriter error, Func<string?> readPassword, bool interactive,
        CancellationToken token = default)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var priceArgs = new List<string>();
        var flags = new HashSet<string>(StringComparer.Ordinal);
        bool invalid = false;
        for (int i = 0; i < args.Length && !invalid;)
        {
            string key = args[i++];
            if (key is "--dry-run" or "--apply" or "--inactive")
            {
                invalid = !flags.Add(key);
                continue;
            }

            if (key is not ("--username" or "--tenant-id" or "--confirm-database" or
                "--action" or "--id" or "--code" or "--name" or "--scope-store-id" or
                "--channel" or "--include-vat" or "--valid-from" or "--valid-until" or
                "--search" or "--page" or "--price") || i == args.Length ||
                args[i].StartsWith("--", StringComparison.Ordinal))
            {
                invalid = true;
                break;
            }

            string value = args[i++];
            if (key == "--price") priceArgs.Add(value);
            else invalid = !values.TryAdd(key, value);
        }

        string action = values.GetValueOrDefault("--action") ?? "";
        bool readOnly = action is "list" or "items";
        bool apply = flags.Contains("--apply");
        bool dryRun = flags.Contains("--dry-run");
        Guid tenantId = Guid.Empty;
        bool valid = !invalid && interactive && dryRun != apply && (readOnly ? dryRun : true) &&
            action is "list" or "items" or "create" or "update" or "activate" or "deactivate" or "prices" &&
            !string.IsNullOrWhiteSpace(values.GetValueOrDefault("--username")) &&
            !string.IsNullOrWhiteSpace(values.GetValueOrDefault("--confirm-database")) &&
            Guid.TryParse(values.GetValueOrDefault("--tenant-id"), out tenantId) &&
            tenantId != Guid.Empty;

        // Οι αναφορές δεν αποκαλύπτουν δεδομένα άλλου Tenant μέσω CLI.
        Guid? id = null;
        Guid? scopeStore = null;
        if (values.TryGetValue("--id", out string? idText))
        {
            if (Guid.TryParse(idText, out Guid parsed) && parsed != Guid.Empty) id = parsed;
            else valid = false;
        }
        if (values.TryGetValue("--scope-store-id", out string? storeText))
        {
            if (Guid.TryParse(storeText, out Guid parsed) && parsed != Guid.Empty) scopeStore = parsed;
            else valid = false;
        }

        int page = 1;
        if (values.TryGetValue("--page", out string? pageText) &&
            (!int.TryParse(pageText, NumberStyles.None, CultureInfo.InvariantCulture, out page) ||
             page is < 1 or > 100_000))
        {
            valid = false;
        }

        var changes = new List<PriceListPriceChange>();
        foreach (string field in priceArgs)
        {
            string[] pair = field.Split('=', 2);
            if (pair.Length != 2 ||
                !Guid.TryParse(pair[0], out Guid itemId) || itemId == Guid.Empty ||
                !decimal.TryParse(pair[1],
                    NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture, out decimal price) ||
                price < 0 || price > PriceListItem.MaxUnitPrice ||
                decimal.Round(price, 4) != price)
            {
                valid = false;
                break;
            }
            changes.Add(new(itemId, price));
        }

        if ((action is "items" or "update" or "activate" or "deactivate" or "prices" && !id.HasValue) ||
            (action == "create" && id.HasValue) ||
            (action == "prices" && (changes.Count == 0 || changes.Count > 50 ||
                changes.Select(x => x.CatalogItemId).Distinct().Count() != changes.Count)) ||
            (action != "prices" && changes.Count > 0) ||
            (action != "list" && flags.Contains("--inactive")) ||
            (values.TryGetValue("--search", out string? search) &&
                (search.Length > 80 || search.Any(char.IsControl))))
        {
            valid = false;
        }

        DateOnly from = default;
        DateOnly? until = null;
        PriceListChannel channel = PriceListChannel.All;
        bool includeVat = true;
        if (action is "create" or "update")
        {
            if (!DateOnly.TryParseExact(values.GetValueOrDefault("--valid-from"), "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out from) ||
                string.IsNullOrWhiteSpace(values.GetValueOrDefault("--name")))
            {
                valid = false;
            }

            if (values.TryGetValue("--valid-until", out string? endText))
            {
                if (DateOnly.TryParseExact(endText, "yyyy-MM-dd",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly end))
                    until = end;
                else valid = false;
            }

            if (action == "create")
            {
                if (!values.TryGetValue("--code", out string? code) ||
                    !values.TryGetValue("--channel", out string? channelText) ||
                    !Enum.TryParse(channelText, false, out channel) || !Enum.IsDefined(channel) ||
                    !values.TryGetValue("--include-vat", out string? vatText) ||
                    !bool.TryParse(vatText, out includeVat))
                {
                    valid = false;
                }
            }
            else if (values.ContainsKey("--code") || scopeStore.HasValue ||
                values.ContainsKey("--channel") || values.ContainsKey("--include-vat"))
            {
                valid = false;
            }
        }
        else if (scopeStore.HasValue || values.ContainsKey("--code") ||
            values.ContainsKey("--name") || values.ContainsKey("--channel") ||
            values.ContainsKey("--include-vat") || values.ContainsKey("--valid-from") ||
            values.ContainsKey("--valid-until"))
        {
            valid = false;
        }

        if (!valid)
        {
            await error.WriteLineAsync(
                "Χρήση: --manage-price-lists --username NAME --tenant-id GUID --confirm-database NAME " +
                "--action list|items|create|update|activate|deactivate|prices " +
                "[--id GUID] [--code CODE] [--name NAME] [--scope-store-id GUID] " +
                "[--channel All|DineIn|Takeaway|Delivery|Retail|Online] [--include-vat true|false] " +
                "[--valid-from yyyy-MM-dd] [--valid-until yyyy-MM-dd] " +
                "[--price ITEM_GUID=AMOUNT]... [--search PREFIX] [--page N] [--inactive] " +
                "(--dry-run | --apply). Απαιτείται διαδραστικό τερματικό.");
            return 2;
        }

        try
        {
            token.ThrowIfCancellationRequested();
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
            if (password is null) return 130;

            CredentialVerificationResult verified = await verifier.VerifyAsync(
                values["--username"], password, token);
            if (verified.Status != CredentialVerificationStatus.Authenticated ||
                verified.UserId is not Guid userId)
            {
                logger.LogWarning("Price list CLI authentication failed");
                await error.WriteLineAsync("Η επαλήθευση λογαριασμού απέτυχε.");
                return 3;
            }

            Guid tenant = tenantId;
            if (action == "list")
            {
                PriceListPage? rows = await service.ListAsync(userId, tenant,
                    values.GetValueOrDefault("--search"), flags.Contains("--inactive"),
                    page, 25, token);
                if (rows is null) return await DeniedAsync();
                foreach (PriceListSummary row in rows.Rows)
                    await output.WriteLineAsync($"{row.Id:D} | {row.Code} | {row.Name} | {row.Channel} | {row.ItemCount} είδη");
                await output.WriteLineAsync($"Σελίδα {rows.Page}, επόμενη: {rows.HasMore}. Χωρίς εγγραφές SQL.");
                return 0;
            }

            if (action == "items")
            {
                PriceListItemsPage? rows = await service.ReadItemsAsync(userId, tenant, id!.Value,
                    values.GetValueOrDefault("--search"), page, 25, token);
                if (rows is null)
                {
                    await error.WriteLineAsync("Δεν επιτρέπεται η πρόσβαση ή δεν βρέθηκε ο τιμοκατάλογος.");
                    return 4;
                }
                foreach (PriceListItemRow row in rows.Rows)
                    await output.WriteLineAsync($"{row.Id:D} | {row.Code} | {row.Name} | {row.UnitPrice?.ToString(CultureInfo.InvariantCulture) ?? "—"}");
                await output.WriteLineAsync($"Σελίδα {rows.Page}, επόμενη: {rows.HasMore}. Χωρίς εγγραφές SQL.");
                return 0;
            }

            if (!await service.CanManageAsync(userId, tenant, token))
                return await DeniedAsync();

            if (id.HasValue && await service.GetAsync(userId, tenant, id.Value, token) is null)
            {
                await error.WriteLineAsync("Ο τιμοκατάλογος δεν βρέθηκε.");
                return 4;
            }

            if (action == "create")
            {
                // Η βασική επικύρωση Domain εκτελείται πριν από προεπισκόπηση ή εγγραφή.
                _ = PriceList.Create(tenant, scopeStore, values["--code"], values["--name"],
                    channel, includeVat, from, until);
            }

            if (dryRun)
            {
                logger.LogInformation("Price list CLI dry-run {Action} by {UserId} in {TenantId}",
                    action, userId, tenant);
                await output.WriteLineAsync("DRY RUN: Τα βασικά πεδία και τα δικαιώματα ελέγχθηκαν. Καμία εγγραφή SQL. " +
                    "Η μοναδικότητα, οι αναφορές και οι συγκρούσεις επιβεβαιώνονται στο --apply.");
                return 0;
            }

            PriceListResult result = action == "prices"
                ? await service.SetPricesAsync(userId, tenant, id!.Value, changes, token)
                : await service.ApplyAsync(userId, tenant,
                    new PriceListChange(action switch
                    {
                        "create" => PriceListAction.Create,
                        "update" => PriceListAction.Update,
                        "activate" => PriceListAction.Activate,
                        _ => PriceListAction.Deactivate
                    }, id, values.GetValueOrDefault("--code"), values.GetValueOrDefault("--name"),
                    scopeStore, channel, includeVat, from, until), token);

            if (result.Status != PriceListStatus.Saved)
            {
                logger.LogWarning("Price list CLI rejected {Status} for {TenantId}", result.Status, tenant);
                await error.WriteLineAsync($"Η αλλαγή απορρίφθηκε: {result.Status}.");
                return result.Status == PriceListStatus.Denied ? 3 : 4;
            }

            logger.LogInformation("Price list CLI saved {Action} in {TenantId} by {UserId}, id {Id}",
                action, tenant, userId, result.Id);
            await output.WriteLineAsync($"Επιτυχία: {action}, αναγνωριστικό: {result.Id:D}.");
            return 0;

            async Task<int> DeniedAsync()
            {
                await error.WriteLineAsync("Απαιτείται δικαίωμα πρόσβασης στην επιχείρηση.");
                return 3;
            }
        }
        catch (DomainRuleException ex)
        {
            logger.LogWarning("Price list CLI validation failed: {Code}", ex.Code);
            await error.WriteLineAsync($"Μη έγκυρα στοιχεία: {ex.Message}");
            return 2;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            await error.WriteLineAsync("Η εντολή ακυρώθηκε.");
            return 130;
        }
    }
}
