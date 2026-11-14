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

        if (validated.ParentCategoryId is { } parentId)
        {
            categories = categories.Where(category => category.ParentCategoryId == parentId);
        }

        // Η ταξινόμηση εκτελείται στη SQL, πριν από το Skip/Take.
        // Οι καθαρά αριθμητικοί κωδικοί ταξινομούνται ως 1, 2, 10·
        // οι μη αριθμητικοί ακολουθούν αλφαβητικά, με σταθερό Id tie-breaker.
        // Το Convert.ToInt64 εφαρμόζεται μόνο για καθαρά ψηφία έως 18 χαρακτήρες,
        // ώστε οι παλιοί κωδικοί 001/002 να συγκρίνονται αριθμητικά χωρίς overflow.
        var numericCategories = categories.Select(category => new
        {
            Category = category,
            IsTextCode = category.Code == "" ||
                EF.Functions.Like(category.Code, "%[^0-9]%")
        });
        var ordered = (validated.SortBy, validated.Descending) switch
        {
            ("name", false) => numericCategories.OrderBy(x => x.Category.Name)
                .ThenBy(x => x.Category.Id),
            ("name", true) => numericCategories.OrderByDescending(x => x.Category.Name)
                .ThenBy(x => x.Category.Id),
            ("status", false) => numericCategories.OrderBy(x => x.Category.IsActive)
                .ThenBy(x => x.Category.Id),
            ("status", true) => numericCategories.OrderByDescending(x => x.Category.IsActive)
                .ThenBy(x => x.Category.Id),
            (_, false) => numericCategories.OrderBy(x => x.IsTextCode)
                .ThenBy(x => x.IsTextCode ? 0L : x.Category.Code.Length > 18
                    ? long.MaxValue : Convert.ToInt64(x.Category.Code))
                .ThenBy(x => x.IsTextCode ? 0 : x.Category.Code.Length > 18
                    ? x.Category.Code.Length : 0)
                .ThenBy(x => x.Category.Code).ThenBy(x => x.Category.Id),
            _ => numericCategories.OrderBy(x => x.IsTextCode)
                .ThenByDescending(x => x.IsTextCode ? 0L : x.Category.Code.Length > 18
                    ? long.MaxValue : Convert.ToInt64(x.Category.Code))
                .ThenByDescending(x => x.IsTextCode ? 0 : x.Category.Code.Length > 18
                    ? x.Category.Code.Length : 0)
                .ThenByDescending(x => x.Category.Code).ThenBy(x => x.Category.Id)
        };

        // Το επιπλέον record επιτρέπει HasMore χωρίς ανεξάρτητο COUNT query.
        int offset = (validated.Page - 1) * validated.PageSize;
        var rows = await ordered.Skip(offset).Take(validated.PageSize + 1)
            .Select(x => new CatalogCategorySummary(
                x.Category.Id, x.Category.Code, x.Category.Name, x.Category.IsActive,
                x.Category.ParentCategoryId, null,
                context.CatalogCategories.Any(child =>
                    child.TenantId == x.Category.TenantId &&
                    child.ParentCategoryId == x.Category.Id && child.IsActive)))
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
                item.Code.StartsWith(term) || item.Name.StartsWith(term) ||
                (item.Barcode != null && item.Barcode.StartsWith(term)));
        }

        if (validated.Kind is { } kind)
        {
            items = items.Where(item => item.Kind == kind);
        }

        // Το σύνθετο join δεν επιτρέπει ανάκτηση κατηγορίας από άλλο Tenant.
        // Πρώτα ταξινομούμε όλο το tenant-scoped σύνολο και μετά κάνουμε σελιδοποίηση.
        var joined = from item in items
                     join category in context.CatalogCategories.AsNoTracking()
                         on new { item.TenantId, Id = item.CategoryId }
                         equals new { category.TenantId, category.Id }
                     select new
                     {
                         Item = item,
                         Category = category,
                         IsTextCode = item.Code == "" || EF.Functions.Like(item.Code, "%[^0-9]%")
                     };

        var ordered = (validated.SortBy, validated.Descending) switch
        {
            ("barcode", false) => joined.OrderBy(x => x.Item.Barcode)
                .ThenBy(x => x.Item.Id),
            ("barcode", true) => joined.OrderByDescending(x => x.Item.Barcode)
                .ThenBy(x => x.Item.Id),
            ("name", false) => joined.OrderBy(x => x.Item.Name)
                .ThenBy(x => x.Item.Id),
            ("name", true) => joined.OrderByDescending(x => x.Item.Name)
                .ThenBy(x => x.Item.Id),
            ("category", false) => joined.OrderBy(x => x.Category.Name)
                .ThenBy(x => x.Item.Name).ThenBy(x => x.Item.Id),
            ("category", true) => joined.OrderByDescending(x => x.Category.Name)
                .ThenBy(x => x.Item.Name).ThenBy(x => x.Item.Id),
            ("kind", false) => joined.OrderBy(x => x.Item.Kind)
                .ThenBy(x => x.Item.Name).ThenBy(x => x.Item.Id),
            ("kind", true) => joined.OrderByDescending(x => x.Item.Kind)
                .ThenBy(x => x.Item.Name).ThenBy(x => x.Item.Id),
            ("unit", false) => joined.OrderBy(x => x.Item.UnitOfMeasure)
                .ThenBy(x => x.Item.Name).ThenBy(x => x.Item.Id),
            ("unit", true) => joined.OrderByDescending(x => x.Item.UnitOfMeasure)
                .ThenBy(x => x.Item.Name).ThenBy(x => x.Item.Id),
            ("accounting", false) => joined.OrderBy(x => x.Item.AccountingType)
                .ThenBy(x => x.Item.Name).ThenBy(x => x.Item.Id),
            ("accounting", true) => joined.OrderByDescending(x => x.Item.AccountingType)
                .ThenBy(x => x.Item.Name).ThenBy(x => x.Item.Id),
            ("stock", false) => joined.OrderBy(x => x.Item.TracksStock)
                .ThenBy(x => x.Item.Name).ThenBy(x => x.Item.Id),
            ("stock", true) => joined.OrderByDescending(x => x.Item.TracksStock)
                .ThenBy(x => x.Item.Name).ThenBy(x => x.Item.Id),
            ("status", false) => joined.OrderBy(x => x.Item.IsActive)
                .ThenBy(x => x.Item.Name).ThenBy(x => x.Item.Id),
            ("status", true) => joined.OrderByDescending(x => x.Item.IsActive)
                .ThenBy(x => x.Item.Name).ThenBy(x => x.Item.Id),
            (_, false) => joined.OrderBy(x => x.IsTextCode)
                .ThenBy(x => x.IsTextCode ? 0L : x.Item.Code.Length > 18
                    ? long.MaxValue : Convert.ToInt64(x.Item.Code))
                .ThenBy(x => x.IsTextCode ? 0 : x.Item.Code.Length > 18
                    ? x.Item.Code.Length : 0)
                .ThenBy(x => x.Item.Code).ThenBy(x => x.Item.Id),
            _ => joined.OrderBy(x => x.IsTextCode)
                .ThenByDescending(x => x.IsTextCode ? 0L : x.Item.Code.Length > 18
                    ? long.MaxValue : Convert.ToInt64(x.Item.Code))
                .ThenByDescending(x => x.IsTextCode ? 0 : x.Item.Code.Length > 18
                    ? x.Item.Code.Length : 0)
                .ThenByDescending(x => x.Item.Code).ThenBy(x => x.Item.Id)
        };

        int offset = (validated.Page - 1) * validated.PageSize;
        var rows = await ordered.Skip(offset).Take(validated.PageSize + 1)
            .Select(x => new CatalogItemSummary(
                x.Item.Id, x.Item.CategoryId, x.Item.Code, x.Item.Name,
                x.Item.Kind, x.Item.TracksStock, x.Item.IsActive, x.Category.Name,
                null, x.Item.UnitOfMeasure, x.Item.AccountingType,
                x.Item.Barcode))
            .ToListAsync(cancellationToken);
        bool hasMore = rows.Count > validated.PageSize;
        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        // Ολοκληρώνουμε τα breadcrumb paths μόνο για τις κατηγορίες της τρέχουσας σελίδας.
        Guid[] categoryIds = rows.Select(x => x.CategoryId).Distinct().ToArray();
        var selectedCategories = await context.CatalogCategories.AsNoTracking()
            .Where(x => x.TenantId == tenantId && categoryIds.Contains(x.Id))
            .Select(x => new { x.Id, x.ParentCategoryId, x.Name })
            .ToListAsync(cancellationToken);
        Dictionary<Guid, string> paths = await CatalogCategoryPaths.ResolveAsync(
            context, tenantId,
            selectedCategories.Select(x => (x.Id, x.ParentCategoryId, x.Name)),
            cancellationToken);
        return new(rows.Select(x => x with
        {
            CategoryPath = paths.GetValueOrDefault(x.CategoryId) ?? x.CategoryName
        }).ToList(), validated.Page, validated.PageSize, hasMore);
    }
}
