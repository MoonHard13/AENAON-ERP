using Aenaon.Application.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Aenaon.Infrastructure.Persistence;

/// <summary>
/// Ανακτά σελίδες κατηγοριών και ειδών με υποχρεωτικό φίλτρο TenantId.
/// Δεν εκθέτει IQueryable ή παρακολουθούμενες οντότητες στον caller.
/// </summary>
public sealed class SqlCatalogReadStore(AenaonDbContext context) : ICatalogReadStore
{
    public async Task<CatalogReadPage<CatalogCategorySummary>> ReadCategoriesAsync(
        Guid tenantId, CatalogReadQuery query,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            return new([], query.Page, query.PageSize, false);
        }

        CatalogReadQuery validated = query.Validated();
        var categories = context.CatalogCategories.AsNoTracking()
            .Where(category => category.TenantId == tenantId);

        if (!validated.IncludeInactive)
        {
            categories = categories.Where(category => category.IsActive);
        }

        if (validated.Search is { } term)
        {
            categories = categories.Where(category =>
                category.Code.StartsWith(term) || category.Name.StartsWith(term));
        }

        // Το επιπλέον record επιτρέπει HasMore χωρίς ανεξάρτητο COUNT query.
        int offset = (validated.Page - 1) * validated.PageSize;
        var rows = await categories.OrderBy(category => category.Code)
            .ThenBy(category => category.Id)
            .Skip(offset).Take(validated.PageSize + 1)
            .Select(category => new CatalogCategorySummary(
                category.Id, category.Code, category.Name, category.IsActive))
            .ToListAsync(cancellationToken);
        bool hasMore = rows.Count > validated.PageSize;
        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        return new(rows, validated.Page, validated.PageSize, hasMore);
    }

    public async Task<CatalogReadPage<CatalogItemSummary>> ReadItemsAsync(
        Guid tenantId, CatalogReadQuery query,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            return new([], query.Page, query.PageSize, false);
        }

        CatalogReadQuery validated = query.Validated();
        var items = context.CatalogItems.AsNoTracking()
            .Where(item => item.TenantId == tenantId);
        if (!validated.IncludeInactive)
        {
            items = items.Where(item => item.IsActive);
        }

        if (validated.Search is { } term)
        {
            items = items.Where(item =>
                item.Code.StartsWith(term) || item.Name.StartsWith(term));
        }

        int offset = (validated.Page - 1) * validated.PageSize;
        var rows = await items.OrderBy(item => item.Code)
            .ThenBy(item => item.Id)
            .Skip(offset).Take(validated.PageSize + 1)
            .Select(item => new CatalogItemSummary(
                item.Id, item.CategoryId, item.Code, item.Name,
                item.Kind, item.TracksStock, item.IsActive))
            .ToListAsync(cancellationToken);
        bool hasMore = rows.Count > validated.PageSize;
        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        return new(rows, validated.Page, validated.PageSize, hasMore);
    }
}
