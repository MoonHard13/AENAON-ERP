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
                category.Id, category.Code, category.Name, category.IsActive,
                category.ParentCategoryId))
            .ToListAsync(cancellationToken);
        bool hasMore = rows.Count > validated.PageSize;
        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        // Η πλήρης διαδρομή υπολογίζεται με περιορισμένες, tenant-scoped αναγνώσεις γονέων.
        Dictionary<Guid, string> paths = await CatalogCategoryPaths.ResolveAsync(context,
            tenantId, rows.Select(x => (x.Id, x.ParentCategoryId, x.Name)), cancellationToken);
        return new(rows.Select(x => x with { CategoryPath = paths.GetValueOrDefault(x.Id, x.Name) })
            .ToList(), validated.Page, validated.PageSize, hasMore);
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

        if (validated.Kind is { } kind)
        {
            items = items.Where(item => item.Kind == kind);
        }

        // Το σύνθετο join δεν επιτρέπει την ανάκτηση ονόματος κατηγορίας από άλλο Tenant.
        int offset = (validated.Page - 1) * validated.PageSize;
        var rows = await (
            from item in items
            join category in context.CatalogCategories.AsNoTracking()
                on new { item.TenantId, Id = item.CategoryId }
                equals new { category.TenantId, category.Id }
            orderby item.Code, item.Id
            select new CatalogItemSummary(
                item.Id, item.CategoryId, item.Code, item.Name,
                item.Kind, item.TracksStock, item.IsActive, category.Name))
            .Skip(offset).Take(validated.PageSize + 1)
            .ToListAsync(cancellationToken);
        bool hasMore = rows.Count > validated.PageSize;
        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        return new(rows, validated.Page, validated.PageSize, hasMore);
    }
}
