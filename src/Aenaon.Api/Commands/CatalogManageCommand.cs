using Aenaon.Application.Catalog;
using Aenaon.Application.Identity;
using Aenaon.Domain.Catalog;
using Aenaon.Domain.Exceptions;
using Aenaon.Infrastructure.Database;
using Microsoft.Extensions.Logging;

namespace Aenaon.Api.Commands;

/// <summary>Διαδραστική, ασφαλής CLI διαχείριση καταλόγου μέσω της ίδιας υπηρεσίας με το Blazor.</summary>
public static class CatalogManageCommand
{
    public static async Task<int> ExecuteAsync(
        string[] args, ICredentialVerifier verifier, CatalogWriteService service,
        IDatabaseSchemaManager schema, ILogger logger,
        TextWriter output, TextWriter error, Func<string?> readPassword,
        bool interactive, CancellationToken cancellationToken = default)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var flags = new HashSet<string>(StringComparer.Ordinal);
        bool invalid = false;

        for (int index = 0; index < args.Length && !invalid;)
        {
            string option = args[index++];
            if (option is "--apply" or "--dry-run" or "--tracks-stock" or "--auto-code" or "--root")
            {
                invalid = !flags.Add(option);
                continue;
            }

            if (option is not ("--username" or "--tenant-id" or "--confirm-database" or
                "--view" or "--action" or "--id" or "--code" or "--name" or "--category-id" or
                "--parent-category-id") ||
                index == args.Length ||
                args[index].StartsWith("--", StringComparison.Ordinal))
            {
                invalid = true;
                break;
            }

            invalid = !values.TryAdd(option, args[index++]);
        }

        string userName = values.GetValueOrDefault("--username") ?? "";
        string database = values.GetValueOrDefault("--confirm-database") ?? "";
        string view = values.GetValueOrDefault("--view") ?? "";
        string actionText = values.GetValueOrDefault("--action") ?? "";
        CatalogManagedEntity entity = view switch
        {
            "categories" => CatalogManagedEntity.Category,
            "products" => CatalogManagedEntity.Product,
            "services" => CatalogManagedEntity.Service,
            _ => (CatalogManagedEntity)(-1)
        };
        CatalogChangeAction action = actionText switch
        {
            "create" => CatalogChangeAction.Create,
            "update" => CatalogChangeAction.Update,
            "activate" => CatalogChangeAction.Activate,
            "deactivate" => CatalogChangeAction.Deactivate,
            _ => (CatalogChangeAction)(-1)
        };

        bool apply = flags.Contains("--apply");
        bool dryRun = flags.Contains("--dry-run");
        bool autoCode = flags.Contains("--auto-code");
        Guid? id = null;
        Guid? categoryId = null;
        Guid? parentId = null;
        string? idField = values.GetValueOrDefault("--id");
        string? categoryField = values.GetValueOrDefault("--category-id");
        string? parentField = values.GetValueOrDefault("--parent-category-id");
        if (Guid.TryParse(idField, out Guid parsedId) && parsedId != Guid.Empty)
        {
            id = parsedId;
        }
        if (Guid.TryParse(categoryField, out Guid parsedCategory) && parsedCategory != Guid.Empty)
        {
            categoryId = parsedCategory;
        }
        if (Guid.TryParse(parentField, out Guid parsedParent) && parsedParent != Guid.Empty)
        {
            parentId = parsedParent;
        }

        if (invalid || apply == dryRun || !Enum.IsDefined(entity) ||
            !Enum.IsDefined(action) ||
            !Guid.TryParse(values.GetValueOrDefault("--tenant-id"), out Guid tenantId) ||
            tenantId == Guid.Empty || string.IsNullOrWhiteSpace(userName) ||
            string.IsNullOrWhiteSpace(database) ||
            (action == CatalogChangeAction.Create && idField is not null) ||
            (action != CatalogChangeAction.Create && id is null) ||
            (idField is not null && id is null) ||
            (categoryField is not null && categoryId is null) ||
            (parentField is not null && parentId is null) ||
            (parentField is not null && entity != CatalogManagedEntity.Category) ||
            (flags.Contains("--root") && (entity != CatalogManagedEntity.Category ||
                parentField is not null)) ||
            (autoCode && (action != CatalogChangeAction.Create ||
                values.ContainsKey("--code"))) ||
            (entity == CatalogManagedEntity.Category && categoryField is not null) ||
            (entity != CatalogManagedEntity.Product && flags.Contains("--tracks-stock")) ||
            (action is CatalogChangeAction.Create or CatalogChangeAction.Update &&
             ((!autoCode && string.IsNullOrWhiteSpace(values.GetValueOrDefault("--code"))) ||
              string.IsNullOrWhiteSpace(values.GetValueOrDefault("--name")) ||
              (entity != CatalogManagedEntity.Category && !categoryId.HasValue))) ||
            !interactive)
        {
            await error.WriteLineAsync(
                "Χρήση: --manage-catalog --username NAME --tenant-id GUID --confirm-database NAME " +
                "--view categories|products|services --action create|update|activate|deactivate " +
                "[--id GUID] [--code CODE | --auto-code] [--name NAME] " +
                "[--category-id GUID] [--parent-category-id GUID | --root] " +
                "[--tracks-stock] (--dry-run | --apply). Απαιτείται διαδραστικό τερματικό.");
            return 2;
        }

        CatalogChangeCommand command = new(entity, action, id,
            values.GetValueOrDefault("--code"), values.GetValueOrDefault("--name"),
            categoryId, flags.Contains("--tracks-stock"), parentId, autoCode, Guid.NewGuid());

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            DatabaseSchemaResult state = await schema.ExecuteAsync(false, null, cancellationToken);
            if (state.Status != DatabaseSchemaStatus.Ready ||
                state.PendingMigrations?.Count != 0 ||
                !string.Equals(state.DatabaseName, database, StringComparison.Ordinal))
            {
                await error.WriteLineAsync(
                    "Η βάση δεν είναι ενημερωμένη ή το όνομα επιβεβαίωσης δεν αντιστοιχεί.");
                return 2;
            }

            await output.WriteAsync("Κωδικός λογαριασμού: ");
            string? password = readPassword();
            await output.WriteLineAsync();
            if (password is null)
            {
                return 130;
            }

            CredentialVerificationResult verification = await verifier.VerifyAsync(
                userName, password, cancellationToken);
            if (verification.Status != CredentialVerificationStatus.Authenticated ||
                verification.UserId is not Guid userId)
            {
                logger.LogWarning("Catalog management CLI credential verification failed");
                await error.WriteLineAsync("Η επαλήθευση λογαριασμού απέτυχε.");
                return 3;
            }

            // Η αναγνώσιμη προεπισκόπηση απαιτεί Manage πριν από οποιοδήποτε SQL write.
            CatalogManageResult existing = await service.GetDetailsAsync(
                userId, tenantId, entity, action == CatalogChangeAction.Create ? null : id,
                cancellationToken);
            if (!existing.IsAuthorized)
            {
                logger.LogWarning("Catalog management CLI denied for {UserId} {TenantId}", userId, tenantId);
                await error.WriteLineAsync("Απαιτείται δικαίωμα Owner/Manage στον Tenant.");
                return 3;
            }

            if (action != CatalogChangeAction.Create && existing.Details?.Record is null)
            {
                await error.WriteLineAsync("Η καταχώρηση δεν βρέθηκε σε αυτήν την επιχείρηση.");
                return 4;
            }

            if (entity == CatalogManagedEntity.Category &&
                action == CatalogChangeAction.Update &&
                parentField is null && !flags.Contains("--root"))
            {
                // Χωρίς ρητή μετακίνηση, η ενημέρωση διατηρεί τον υπάρχοντα γονέα.
                command = command with
                {
                    ParentCategoryId = existing.Details?.Record?.ParentCategoryId
                };
            }

            if (action is CatalogChangeAction.Create or CatalogChangeAction.Update)
            {
                string validationCode = autoCode ? "1" : command.Code ?? "";
                if (entity == CatalogManagedEntity.Category)
                {
                    _ = CatalogCategory.Create(tenantId, validationCode, command.Name);
                }
                else
                {
                    _ = CatalogItem.Create(tenantId, command.CategoryId!.Value,
                        validationCode, command.Name,
                        entity == CatalogManagedEntity.Service
                            ? CatalogItemKind.Service : CatalogItemKind.Product,
                        command.TracksStock);
                }
            }

            if (dryRun)
            {
                logger.LogInformation(
                    "Catalog CLI dry run by {UserId} for {TenantId} {Entity} {Action}",
                    userId, tenantId, entity, action);
                await output.WriteLineAsync(
                    "DRY RUN: Η βασική επικύρωση ολοκληρώθηκε. Δεν έγινε εγγραφή σε SQL. " +
                    "Ο τελικός έλεγχος κατηγορίας/unique code θα γίνει μόνο με --apply.");
                return 0;
            }

            CatalogChangeResult result = await service.ApplyAsync(
                userId, tenantId, command, cancellationToken);
            if (result.Status != CatalogChangeStatus.Saved)
            {
                logger.LogWarning("Catalog CLI change rejected: {Status} for {TenantId}",
                    result.Status, tenantId);
                await error.WriteLineAsync($"Η αλλαγή απορρίφθηκε: {result.Status}.");
                return result.Status == CatalogChangeStatus.Denied ? 3 : 4;
            }

            logger.LogInformation("Catalog CLI changed by {UserId} for {TenantId}: {Entity} {Action} {Id}",
                userId, tenantId, entity, action, result.Id);
            await output.WriteLineAsync($"Επιτυχία: {entity} {action}, αναγνωριστικό: {result.Id}.");
            return 0;
        }
        catch (DomainRuleException exception)
        {
            logger.LogWarning("Catalog CLI validation rejected: {Code}", exception.Code);
            await error.WriteLineAsync($"Μη έγκυρα στοιχεία: {exception.Message}");
            return 2;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await error.WriteLineAsync("Η εντολή ακυρώθηκε.");
            return 130;
        }
    }
}
