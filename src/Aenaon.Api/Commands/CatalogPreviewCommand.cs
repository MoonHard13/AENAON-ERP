using System.Globalization;
using Aenaon.Application.Catalog;
using Aenaon.Application.Identity;
using Aenaon.Domain.Exceptions;
using Aenaon.Infrastructure.Database;
using Microsoft.Extensions.Logging;

namespace Aenaon.Api.Commands;

/// <summary>Διαδραστική, εξουσιοδοτημένη και μόνο-ανάγνωσης προεπισκόπηση καταλόγου.</summary>
public static class CatalogPreviewCommand
{
    public static async Task<int> ExecuteAsync(
        string[] args,
        ICredentialVerifier verifier,
        CatalogReadService service,
        IDatabaseSchemaManager schemaManager,
        ILogger logger,
        TextWriter output, TextWriter error,
        Func<string?> readPassword, bool interactive,
        CancellationToken cancellationToken = default)
    {
        string? user = null;
        string? tenantText = null;
        string? databaseName = null;
        string? entity = null;
        string? search = null;
        int page = 1;
        int pageSize = 25;
        bool includeInactive = false;
        bool invalid = false;

        for (int index = 0; index < args.Length && !invalid;)
        {
            string option = args[index++];
            if (option == "--include-inactive")
            {
                if (includeInactive)
                {
                    invalid = true;
                }
                includeInactive = true;
                continue;
            }

            if (index >= args.Length || args[index].StartsWith("--", StringComparison.Ordinal))
            {
                invalid = true;
                break;
            }

            string value = args[index++];
            switch (option)
            {
                case "--username" when user is null:
                    user = value;
                    break;
                case "--tenant-id" when tenantText is null:
                    tenantText = value;
                    break;
                case "--confirm-database" when databaseName is null:
                    databaseName = value;
                    break;
                case "--entity" when entity is null:
                    entity = value;
                    break;
                case "--search" when search is null:
                    search = value;
                    break;
                case "--page" when page == 1 &&
                    int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture,
                        out int parsedPage):
                    page = parsedPage;
                    break;
                case "--page-size" when pageSize == 25 &&
                    int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture,
                        out int parsedSize):
                    pageSize = parsedSize;
                    break;
                default:
                    invalid = true;
                    break;
            }
        }

        if (invalid || string.IsNullOrWhiteSpace(user) ||
            !Guid.TryParse(tenantText, out Guid tenantId) || tenantId == Guid.Empty ||
            string.IsNullOrWhiteSpace(databaseName) || entity is not ("categories" or "items"))
        {
            await error.WriteLineAsync(
                "Χρήση: --preview-catalog --username NAME --tenant-id GUID " +
                "--confirm-database NAME --entity categories|items " +
                "[--search PREFIX] [--page N] [--page-size 1..100] [--include-inactive]");
            return 2;
        }

        CatalogReadQuery query;
        try
        {
            query = new CatalogReadQuery(page, pageSize, search, includeInactive).Validated();
        }
        catch (DomainRuleException)
        {
            await error.WriteLineAsync("Μη έγκυρη σελίδα, μέγεθος ή αναζήτηση.");
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
            DatabaseSchemaResult schema = await schemaManager.ExecuteAsync(
                false, null, cancellationToken);
            if (schema.Status != DatabaseSchemaStatus.Ready ||
                schema.PendingMigrations?.Count != 0 ||
                !string.Equals(schema.DatabaseName, databaseName, StringComparison.Ordinal))
            {
                await error.WriteLineAsync(
                    "Η βάση δεν είναι ενημερωμένη ή δεν ταιριάζει με το ακριβές όνομα επιβεβαίωσης.");
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
                user, password, cancellationToken);
            if (verification.Status != CredentialVerificationStatus.Authenticated ||
                verification.UserId is not Guid userId)
            {
                await error.WriteLineAsync("Η επαλήθευση λογαριασμού απέτυχε.");
                logger.LogWarning("Catalog preview credential verification failed");
                return 2;
            }

            if (entity == "categories")
            {
                CatalogReadResult<CatalogCategorySummary> result = await service.ReadCategoriesAsync(
                    userId, tenantId, query, cancellationToken);
                if (!result.IsAuthorized || result.Page is null)
                {
                    await error.WriteLineAsync("Δεν επιτρέπεται πρόσβαση στον κατάλογο της επιχείρησης.");
                    logger.LogWarning("Catalog categories preview denied");
                    return 3;
                }

                await PrintHeaderAsync(output, entity, result.Page.Page,
                    result.Page.PageSize, result.Page.HasMore, result.Page.Rows.Count);
                foreach (CatalogCategorySummary category in result.Page.Rows)
                {
                    await output.WriteLineAsync(
                        $"{category.Code} | {category.Name} | {(category.IsActive ? "Ενεργή" : "Ανενεργή")}");
                }
            }
            else
            {
                CatalogReadResult<CatalogItemSummary> result = await service.ReadItemsAsync(
                    userId, tenantId, query, cancellationToken);
                if (!result.IsAuthorized || result.Page is null)
                {
                    await error.WriteLineAsync("Δεν επιτρέπεται πρόσβαση στον κατάλογο της επιχείρησης.");
                    logger.LogWarning("Catalog items preview denied");
                    return 3;
                }

                await PrintHeaderAsync(output, entity, result.Page.Page,
                    result.Page.PageSize, result.Page.HasMore, result.Page.Rows.Count);
                foreach (CatalogItemSummary item in result.Page.Rows)
                {
                    await output.WriteLineAsync(
                        $"{item.Code} | {item.Name} | {item.Kind} | " +
                        $"{(item.IsActive ? "Ενεργό" : "Ανενεργό")} | " +
                        $"Παρακολούθηση αποθέματος: {(item.TracksStock ? "Ναι" : "Όχι")}");
                }
            }

            logger.LogInformation("Catalog preview completed: {Entity}, {Page}, {PageSize}",
                entity, query.Page, query.PageSize);
            await output.WriteLineAsync(
                "Μόνο προβολή. Δεν έγιναν αλλαγές σε είδη, κατηγορίες, ΦΠΑ ή παραστατικά.");
            return 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await error.WriteLineAsync("Η προεπισκόπηση καταλόγου ακυρώθηκε.");
            return 130;
        }
    }

    private static async Task PrintHeaderAsync(TextWriter output, string entity,
        int page, int pageSize, bool hasMore, int count)
    {
        await output.WriteLineAsync($"CATALOG PREVIEW — {entity}");
        await output.WriteLineAsync(
            $"Σελίδα: {page} | Μέγεθος: {pageSize} | Εγγραφές: {count} | " +
            $"Επόμενη σελίδα: {(hasMore ? "Ναι" : "Όχι")}");
        if (count == 0)
        {
            await output.WriteLineAsync("Δεν υπάρχουν καταχωρημένα δεδομένα με αυτά τα φίλτρα.");
        }
    }
}
