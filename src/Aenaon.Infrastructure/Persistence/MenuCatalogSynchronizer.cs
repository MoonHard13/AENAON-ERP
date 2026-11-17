using Aenaon.Domain.Catalog;
using Aenaon.Domain.Menus;
using Microsoft.EntityFrameworkCore;

namespace Aenaon.Infrastructure.Persistence;

/// <summary>
/// Εισάγει αποκλειστικά νέες ERP κατηγορίες/είδη σε όλα τα μη αρχειοθετημένα
/// προσχέδια του Tenant, χωρίς αντικατάσταση προσαρμογών ή τιμών.
/// </summary>
internal static class MenuCatalogSynchronizer
{
    private const int MaxMenus = 1000;

    public static async Task<bool> CategoryCreatedAsync(AenaonDbContext context,
        CatalogCategory source, CancellationToken cancellationToken)
    {
        Guid tenantId = source.TenantId;
        List<Guid> menuIds = await context.RestaurantMenus.AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsArchived)
            .OrderBy(x => x.Id).Select(x => x.Id)
            .Take(MaxMenus + 1).ToListAsync(cancellationToken);
        if (menuIds.Count > MaxMenus)
        {
            return false;
        }

        if (menuIds.Count == 0)
        {
            return true;
        }

        List<Guid> existingLinks = await context.MenuDraftCategories.AsNoTracking()
            .Where(x => x.TenantId == tenantId &&
                x.CatalogCategoryId == source.Id &&
                menuIds.Contains(x.MenuId))
            .Select(x => x.MenuId).ToListAsync(cancellationToken);

        var positions = await context.MenuDraftCategories.AsNoTracking()
            .Where(x => x.TenantId == tenantId && menuIds.Contains(x.MenuId))
            .GroupBy(x => x.MenuId)
            .Select(g => new { MenuId = g.Key, Last = g.Max(x => x.Position) })
            .ToDictionaryAsync(x => x.MenuId, x => x.Last, cancellationToken);

        var drafts = await context.MenuDrafts
            .Where(x => x.TenantId == tenantId && menuIds.Contains(x.MenuId))
            .ToDictionaryAsync(x => x.MenuId, cancellationToken);
        if (drafts.Count != menuIds.Count)
        {
            return false;
        }

        Dictionary<Guid, string> paths = await CatalogCategoryPaths.ResolveAsync(context,
            tenantId, [(source.Id, source.ParentCategoryId, source.Name)], cancellationToken);
        string resolved = paths[source.Id].Length <= MenuDraftCategory.MaxNameLength
            ? paths[source.Id] : source.Name;
        HashSet<Guid> linked = existingLinks.ToHashSet();

        foreach (Guid menuId in menuIds)
        {
            if (linked.Contains(menuId))
            {
                continue;
            }

            int next = positions.GetValueOrDefault(menuId) + 1;
            if (next > 10000)
            {
                return false;
            }

            context.MenuDraftCategories.Add(MenuDraftCategory.Create(
                tenantId, menuId, resolved, next, source.Id));
            drafts[menuId].Touch(DateTime.UtcNow);
        }

        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public static async Task<bool> ItemCreatedAsync(AenaonDbContext context,
        CatalogItem source, CancellationToken cancellationToken)
    {
        Guid tenantId = source.TenantId;
        var categories = await
            (from category in context.MenuDraftCategories.AsNoTracking()
             join menu in context.RestaurantMenus.AsNoTracking()
                 on new { category.TenantId, category.MenuId }
                 equals new { menu.TenantId, MenuId = menu.Id }
             where category.TenantId == tenantId &&
                   category.CatalogCategoryId == source.CategoryId &&
                   !menu.IsArchived
             select new { category.MenuId, CategoryId = category.Id })
            .Take(MaxMenus + 1).ToListAsync(cancellationToken);

        if (categories.Count > MaxMenus)
        {
            return false;
        }

        if (categories.Count == 0)
        {
            return true;
        }

        Guid[] categoryIds = categories.Select(x => x.CategoryId).ToArray();
        var maxPositions = await context.MenuDraftItems.AsNoTracking()
            .Where(x => x.TenantId == tenantId && categoryIds.Contains(x.MenuCategoryId))
            .GroupBy(x => x.MenuCategoryId)
            .Select(g => new { CategoryId = g.Key, Last = g.Max(x => x.Position) })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Last, cancellationToken);

        HashSet<Guid> existing = (await context.MenuDraftItems.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.CatalogItemId == source.Id &&
                categoryIds.Contains(x.MenuCategoryId))
            .Select(x => x.MenuCategoryId).ToListAsync(cancellationToken))
            .ToHashSet();

        Guid[] menus = categories.Select(x => x.MenuId).Distinct().ToArray();
        var drafts = await context.MenuDrafts
            .Where(x => x.TenantId == tenantId && menus.Contains(x.MenuId))
            .ToDictionaryAsync(x => x.MenuId, cancellationToken);
        if (drafts.Count != menus.Length)
        {
            return false;
        }

        var touched = new HashSet<Guid>();
        foreach (var category in categories)
        {
            if (existing.Contains(category.CategoryId))
            {
                continue;
            }

            int position = maxPositions.GetValueOrDefault(category.CategoryId) + 1;
            if (position > 10000)
            {
                return false;
            }

            context.MenuDraftItems.Add(MenuDraftItem.Create(
                tenantId, category.MenuId, category.CategoryId, source.Id, position));
            if (touched.Add(category.MenuId))
            {
                drafts[category.MenuId].Touch(DateTime.UtcNow);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
