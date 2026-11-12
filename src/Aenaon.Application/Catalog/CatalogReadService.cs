using Aenaon.Application.Tenancy;
using Aenaon.Domain.Catalog;
using Aenaon.Domain.Exceptions;

namespace Aenaon.Application.Catalog;

/// <summary>Ασφαλές όριο σελίδας για προβολή καταλόγου, χωρίς δικαίωμα μεταβολής.</summary>
public sealed record CatalogReadQuery(int Page = 1, int PageSize = 25,
    string? Search = null, bool IncludeInactive = false, CatalogItemKind? Kind = null,
    Guid? ParentCategoryId = null)
{
    public const int MaxPage = 1000;
    public const int MaxPageSize = 100;
    public const int MaxSearchLength = 80;

    public CatalogReadQuery Validated()
    {
        string? term = Search?.Trim();
        if (Page is < 1 or > MaxPage || PageSize is < 1 or > MaxPageSize ||
            term is { Length: > MaxSearchLength } ||
            term?.Any(char.IsControl) == true ||
            (Kind.HasValue && !Enum.IsDefined(Kind.Value)) ||
            ParentCategoryId == Guid.Empty)
        {
            throw new DomainRuleException("catalog.read.query.invalid",
                "Μη έγκυρη σελίδα, μέγεθος ή αναζήτηση καταλόγου.");
        }

        return this with { Search = string.IsNullOrEmpty(term) ? null : term };
    }
}

/// <summary>Μία ονομασμένη κατηγορία του επιλεγμένου TenantId.</summary>
public sealed record CatalogCategorySummary(Guid Id, string Code, string Name, bool IsActive,
    Guid? ParentCategoryId = null, string? CategoryPath = null, bool HasChildren = false);

/// <summary>Είδος ή υπηρεσία χωρίς τιμές, ΦΠΑ, αποθήκη ή δεδομένα παραστατικών.</summary>
public sealed record CatalogItemSummary(
    Guid Id, Guid CategoryId, string Code, string Name,
    CatalogItemKind Kind, bool TracksStock, bool IsActive,
    string? CategoryName = null, string? CategoryPath = null);

/// <summary>Σελιδοποιημένη λίστα με περιορισμένο αριθμό αναγνώσεων.</summary>
public sealed record CatalogReadPage<T>(
    IReadOnlyList<T> Rows, int Page, int PageSize, bool HasMore);

/// <summary>Κλειστή πρόσβαση όταν ο χρήστης δεν ανήκει στην επιλεγμένη επιχείρηση.</summary>
public sealed record CatalogReadResult<T>(
    bool IsAuthorized, CatalogReadPage<T>? Page)
{
    public static CatalogReadResult<T> Denied => new(false, null);
}

/// <summary>Η υποδομή πραγματοποιεί αποκλειστικά tenant-scoped αναγνώσεις.</summary>
public interface ICatalogReadStore
{
    Task<CatalogReadPage<CatalogCategorySummary>> ReadCategoriesAsync(
        Guid tenantId, CatalogReadQuery query, CancellationToken cancellationToken = default);

    Task<CatalogReadPage<CatalogItemSummary>> ReadItemsAsync(
        Guid tenantId, CatalogReadQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Επιβάλλει εξουσιοδότηση πριν την ανάγνωση καταλόγου από το ίδιο store που
/// χρησιμοποιούν το CLI και οι μελλοντικές οθόνες Blazor.
/// </summary>
public sealed class CatalogReadService(
    TenantAuthorizationService authorization,
    ICatalogReadStore store)
{
    public async Task<CatalogReadResult<CatalogCategorySummary>> ReadCategoriesAsync(
        Guid authenticatedUserId, Guid tenantId, CatalogReadQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        CatalogReadQuery validated = query.Validated();
        cancellationToken.ThrowIfCancellationRequested();
        if (!await AuthorizedAsync(authenticatedUserId, tenantId, cancellationToken))
        {
            return CatalogReadResult<CatalogCategorySummary>.Denied;
        }

        return new(true, await store.ReadCategoriesAsync(
            tenantId, validated, cancellationToken));
    }

    public async Task<CatalogReadResult<CatalogItemSummary>> ReadItemsAsync(
        Guid authenticatedUserId, Guid tenantId, CatalogReadQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        CatalogReadQuery validated = query.Validated();
        cancellationToken.ThrowIfCancellationRequested();
        if (!await AuthorizedAsync(authenticatedUserId, tenantId, cancellationToken))
        {
            return CatalogReadResult<CatalogItemSummary>.Denied;
        }

        return new(true, await store.ReadItemsAsync(
            tenantId, validated, cancellationToken));
    }

    private async Task<bool> AuthorizedAsync(
        Guid authenticatedUserId, Guid tenantId, CancellationToken cancellationToken)
    {
        if (authenticatedUserId == Guid.Empty || tenantId == Guid.Empty)
        {
            return false;
        }

        TenantAccessResult access = await authorization.CheckAsync(
            authenticatedUserId, tenantId, null, TenantPermission.Read, cancellationToken);
        return access.Status == TenantAccessStatus.Granted;
    }
}
