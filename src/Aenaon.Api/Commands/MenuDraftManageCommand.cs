using System.Globalization;
using Aenaon.Application.Identity;
using Aenaon.Application.Menus;
using Aenaon.Application.Tenancy;
using Aenaon.Infrastructure.Database;
using Microsoft.Extensions.Logging;

namespace Aenaon.Api.Commands;

/// <summary>
/// Development-only CLI για πρόχειρα μενού. Δεν δέχεται password σε όρισμα και
/// δεν τροποποιεί SQL χωρίς ρητό --apply και επιβεβαίωση βάσης.
/// </summary>
public static class MenuDraftManageCommand
{
    public static async Task<int> ExecuteAsync(string[] args,
        ICredentialVerifier verifier, MenuDraftManagementService service,
        TenantAuthorizationService authorization, IDatabaseSchemaManager schema,
        ILogger logger, TextWriter output, TextWriter error,
        Func<string?> readPassword, bool interactive, CancellationToken token = default)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var flags = new HashSet<string>(StringComparer.Ordinal);
        bool invalid = false;
        Guid tenantId = Guid.Empty;
        for (int i = 0; i < args.Length && !invalid;)
        {
            string key = args[i++];
            if (key is "--dry-run" or "--apply")
            {
                invalid = !flags.Add(key);
                continue;
            }
            if (key is not ("--username" or "--tenant-id" or "--confirm-database" or
                "--action" or "--menu-id" or "--category-id" or "--item-id" or
                "--catalog-item-id" or "--expected-revision" or "--position" or
                "--name" or "--code" or "--name-override" or "--visible" or
                "--request-id" or "--search") || i >= args.Length ||
                args[i].StartsWith("--", StringComparison.Ordinal))
            {
                invalid = true;
                break;
            }
            invalid = !values.TryAdd(key, args[i++]);
        }

        string actionName = values.GetValueOrDefault("--action") ?? string.Empty;
        bool read = actionName is "list" or "details" or "catalog";
        bool apply = flags.Contains("--apply");
        bool dryRun = flags.Contains("--dry-run");
        bool valid = !invalid && interactive && (dryRun != apply) &&
            (!read || dryRun) &&
            !string.IsNullOrWhiteSpace(values.GetValueOrDefault("--username")) &&
            !string.IsNullOrWhiteSpace(values.GetValueOrDefault("--confirm-database")) &&
            Guid.TryParse(values.GetValueOrDefault("--tenant-id"), out tenantId) &&
            tenantId != Guid.Empty &&
            actionName is ("list" or "details" or "catalog" or
                "create-menu" or "import-catalog" or "update-menu" or "add-category" or "update-category" or
                "move-category" or "remove-category" or "add-item" or "rename-item" or
                "move-item" or "remove-item");

        Guid? menuId = null;
        Guid? categoryId = null;
        Guid? itemId = null;
        Guid? catalogItemId = null;
        Guid? requestId = null;
        if (!TryOptionalGuid(values, "--menu-id", out menuId) ||
            !TryOptionalGuid(values, "--category-id", out categoryId) ||
            !TryOptionalGuid(values, "--item-id", out itemId) ||
            !TryOptionalGuid(values, "--catalog-item-id", out catalogItemId) ||
            !TryOptionalGuid(values, "--request-id", out requestId))
        {
            valid = false;
        }

        long? revision = null;
        if (values.TryGetValue("--expected-revision", out string? revisionText))
        {
            if (long.TryParse(revisionText, NumberStyles.None,
                    CultureInfo.InvariantCulture, out long parsed) && parsed > 0)
            {
                revision = parsed;
            }
            else
            {
                valid = false;
            }
        }

        int? position = null;
        if (values.TryGetValue("--position", out string? positionText))
        {
            if (int.TryParse(positionText, NumberStyles.None,
                    CultureInfo.InvariantCulture, out int parsed) && parsed is >= 1 and <= 10000)
            {
                position = parsed;
            }
            else
            {
                valid = false;
            }
        }

        bool? visible = null;
        if (values.TryGetValue("--visible", out string? visibleText))
        {
            if (bool.TryParse(visibleText, out bool parsed))
            {
                visible = parsed;
            }
            else
            {
                valid = false;
            }
        }

        if (read && (revision.HasValue || requestId.HasValue || categoryId.HasValue ||
                     itemId.HasValue || catalogItemId.HasValue || position.HasValue ||
                     visible.HasValue))
        {
            valid = false;
        }

        if (!valid)
        {
            await error.WriteLineAsync(
                "Χρήση: --manage-menus --username NAME --tenant-id GUID --confirm-database NAME " +
                "--action list|details|catalog|create-menu|import-catalog|update-menu|add-category|update-category|" +
                "move-category|remove-category|add-item|rename-item|move-item|remove-item " +
                "[--menu-id GUID] [--category-id GUID] [--item-id GUID] [--catalog-item-id GUID] " +
                "[--code CODE] [--name NAME] [--name-override TEXT] [--expected-revision N] " +
                "[--position N] [--visible true|false] [--request-id GUID] [--search PREFIX] " +
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
            if (password is null)
            {
                return 130;
            }

            CredentialVerificationResult verified = await verifier.VerifyAsync(
                values["--username"], password, token);
            if (verified.Status != CredentialVerificationStatus.Authenticated ||
                verified.UserId is not Guid userId)
            {
                logger.LogWarning("Menu CLI authentication denied");
                await error.WriteLineAsync("Η επαλήθευση λογαριασμού απέτυχε.");
                return 3;
            }

            if (read)
            {
                MenuDraftReadResult readResult = await service.ReadAsync(userId, tenantId,
                    actionName == "details" ? menuId : null,
                    actionName == "catalog" ? values.GetValueOrDefault("--search") : null,
                    token);
                if (!readResult.IsAuthorized)
                {
                    await error.WriteLineAsync("Δεν έχετε δικαίωμα ανάγνωσης.");
                    return 3;
                }

                if (actionName == "catalog")
                {
                    foreach (MenuCatalogOption item in readResult.Catalog)
                    {
                        await output.WriteLineAsync($"{item.CatalogItemId:D} | {item.Code} | {item.Name}");
                    }
                }
                else if (actionName == "details")
                {
                    if (readResult.Details is not { } details || details.Menu.MenuId != menuId)
                    {
                        await error.WriteLineAsync("Το μενού δεν βρέθηκε.");
                        return 4;
                    }
                    await output.WriteLineAsync(
                        $"Μενού {details.Menu.Code}: {details.Menu.Name} | Revision {details.Menu.Revision}");
                    foreach (MenuDraftCategoryRecord category in details.Categories)
                    {
                        await output.WriteLineAsync(
                            $"{category.CategoryId:D} | #{category.Position} | {category.Name} | Visible={category.IsVisible}");
                        foreach (MenuDraftItemRecord item in details.Items.Where(x =>
                            x.CategoryId == category.CategoryId))
                        {
                            await output.WriteLineAsync(
                                $"  {item.ItemId:D} | #{item.Position} | {item.CatalogCode} | " +
                                $"{item.NameOverride ?? item.CatalogName} | Active={item.IsCatalogActive}");
                        }
                    }
                }
                else
                {
                    foreach (MenuDraftSummary menu in readResult.Menus)
                    {
                        await output.WriteLineAsync(
                            $"{menu.MenuId:D} | {menu.Code} | {menu.Name} | Revision {menu.Revision}");
                    }
                }
                return 0;
            }

            TenantAccessResult access = await authorization.CheckAsync(userId,
                tenantId, null, TenantPermission.Manage, token);
            if (access.Status != TenantAccessStatus.Granted)
            {
                await error.WriteLineAsync("Απαιτείται δικαίωμα Owner.");
                return 3;
            }

            MenuDraftAction action = actionName switch
            {
                "create-menu" => MenuDraftAction.CreateMenu,
                "import-catalog" => MenuDraftAction.ImportCatalogContents,
                "update-menu" => MenuDraftAction.UpdateMenu,
                "add-category" => MenuDraftAction.AddCategory,
                "update-category" => MenuDraftAction.UpdateCategory,
                "move-category" => MenuDraftAction.MoveCategory,
                "remove-category" => MenuDraftAction.RemoveCategory,
                "add-item" => MenuDraftAction.AddItem,
                "rename-item" => MenuDraftAction.RenameItem,
                "move-item" => MenuDraftAction.MoveItem,
                "remove-item" => MenuDraftAction.RemoveItem,
                _ => (MenuDraftAction)(-1)
            };

            Guid operation = requestId ?? Guid.NewGuid();
            var command = new MenuDraftCommand(action, operation, menuId, revision,
                categoryId, itemId, catalogItemId, values.GetValueOrDefault("--code"),
                values.GetValueOrDefault("--name"), values.GetValueOrDefault("--name-override"),
                position, visible);

            if (dryRun)
            {
                await output.WriteLineAsync($"ΠΡΟΕΠΙΣΚΟΠΗΣΗ ΧΩΡΙΣ SQL WRITE | {actionName} | Tenant {tenantId:D}");
                if (action is MenuDraftAction.CreateMenu or MenuDraftAction.ImportCatalogContents)
                {
                    await output.WriteLineAsync("Η εφαρμογή θα εισαγάγει τις ενεργές κατηγορίες ΜΑΖΙ με τα ενεργά είδη ERP, χωρίς αλλαγή του καταλόγου ή τιμών.");
                }
                await output.WriteLineAsync("Ο έλεγχος αναφοράς/σύγκρουσης θα εκτελεστεί μόνο κατά την εφαρμογή.");
                return 0;
            }

            MenuDraftMutationResult result = await service.ApplyAsync(
                userId, tenantId, command, token);
            if (result.Status == MenuDraftStatus.Saved)
            {
                await output.WriteLineAsync(
                    $"Αποθηκεύτηκε | MenuId={result.MenuId:D} | Id={result.RecordId:D} | " +
                    $"Revision={result.Revision} | RequestId={operation:D}");
                return 0;
            }

            await error.WriteLineAsync($"Η εντολή απορρίφθηκε: {result.Status} ({result.Error ?? "unknown"}).");
            return result.Status switch
            {
                MenuDraftStatus.Denied => 3,
                MenuDraftStatus.Missing => 4,
                MenuDraftStatus.Conflict => 5,
                MenuDraftStatus.Duplicate => 6,
                _ => 2
            };
        }
        catch (OperationCanceledException)
        {
            return 130;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Menu CLI failure for {Action}", actionName);
            await error.WriteLineAsync("Αποτυχία εντολής. Ελέγξτε τα ασφαλή διαγνωστικά.");
            return 1;
        }
    }

    private static bool TryOptionalGuid(Dictionary<string, string> values,
        string key, out Guid? id)
    {
        id = null;
        if (!values.TryGetValue(key, out string? value))
        {
            return true;
        }
        if (!Guid.TryParse(value, out Guid parsed) || parsed == Guid.Empty)
        {
            return false;
        }
        id = parsed;
        return true;
    }
}
