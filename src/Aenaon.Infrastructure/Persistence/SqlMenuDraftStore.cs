using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aenaon.Application.Menus;
using Aenaon.Domain.Catalog;
using Aenaon.Domain.Menus;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Aenaon.Infrastructure.Persistence;

/// <summary>
/// Προσπελαύνει αποκλειστικά στοιχεία ενός Tenant, προστατεύει τη revision και
/// δεν μεταβάλλει ποτέ καταχωρίσεις του κύριου ERP Catalog.
/// </summary>
public sealed class SqlMenuDraftStore(AenaonDbContext context, TimeProvider clock,
    ILogger<SqlMenuDraftStore> logger) : IMenuDraftStore
{
    public async Task<IReadOnlyList<MenuDraftSummary>> ListAsync(Guid tenantId,
        CancellationToken cancellationToken = default) =>
        await (from menu in context.RestaurantMenus.AsNoTracking()
               join draft in context.MenuDrafts.AsNoTracking()
                   on new { menu.TenantId, MenuId = menu.Id }
                   equals new { draft.TenantId, draft.MenuId }
               where menu.TenantId == tenantId && !menu.IsArchived
               orderby menu.Code, menu.Id
               select new MenuDraftSummary(menu.Id, menu.Code, menu.Name,
                   draft.Revision, draft.EditedUtc))
            .Take(100).ToListAsync(cancellationToken);

    public async Task<MenuDraftDetails?> GetAsync(Guid tenantId, Guid menuId,
        CancellationToken cancellationToken = default)
    {
        MenuDraftSummary? menu = await
            (from row in context.RestaurantMenus.AsNoTracking()
             join draft in context.MenuDrafts.AsNoTracking()
                 on new { row.TenantId, MenuId = row.Id }
                 equals new { draft.TenantId, draft.MenuId }
             where row.TenantId == tenantId && row.Id == menuId && !row.IsArchived
             select new MenuDraftSummary(row.Id, row.Code, row.Name,
                 draft.Revision, draft.EditedUtc))
            .SingleOrDefaultAsync(cancellationToken);
        if (menu is null)
        {
            return null;
        }

        List<MenuDraftCategoryRecord> categories = await context.MenuDraftCategories
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.MenuId == menuId)
            .OrderBy(x => x.Position).ThenBy(x => x.Id)
            .Select(x => new MenuDraftCategoryRecord(x.Id, x.Name, x.Position, x.IsVisible))
            .ToListAsync(cancellationToken);

        List<MenuDraftItemRecord> items = await
            (from item in context.MenuDraftItems.AsNoTracking()
             join catalog in context.CatalogItems.AsNoTracking()
                 on new { item.TenantId, Id = item.CatalogItemId }
                 equals new { catalog.TenantId, catalog.Id }
             where item.TenantId == tenantId && item.MenuId == menuId
             orderby item.MenuCategoryId, item.Position, item.Id
             select new MenuDraftItemRecord(item.Id, item.MenuCategoryId, item.CatalogItemId,
                 catalog.Code, catalog.Name, item.NameOverride, catalog.IsActive, item.Position))
            .ToListAsync(cancellationToken);

        return new(menu, categories, items);
    }

    public async Task<IReadOnlyList<MenuCatalogOption>> SearchCatalogAsync(Guid tenantId,
        string? search, CancellationToken cancellationToken = default)
    {
        IQueryable<CatalogItem> query = context.CatalogItems.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            if (term.Length > 80)
            {
                return [];
            }
            query = query.Where(x => x.Code.StartsWith(term) || x.Name.StartsWith(term));
        }

        return await query.OrderBy(x => x.Code).ThenBy(x => x.Id)
            .Select(x => new MenuCatalogOption(x.Id, x.Code, x.Name))
            .Take(50).ToListAsync(cancellationToken);
    }

    public async Task<MenuDraftMutationResult> ApplyAsync(Guid tenantId, Guid userId,
        MenuDraftCommand command, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || userId == Guid.Empty)
        {
            return new(MenuDraftStatus.Denied);
        }

        string hash = RequestHash(command);
        try
        {
            await using IDbContextTransaction transaction =
                await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted,
                    cancellationToken);

            MenuDraftOperationReceipt? prior = await context.MenuDraftOperationReceipts.AsNoTracking()
                .SingleOrDefaultAsync(x => x.TenantId == tenantId &&
                    x.RequestId == command.RequestId, cancellationToken);
            if (prior is not null)
            {
                return prior.RequestHash == hash && prior.ActorUserId == userId
                    ? new(MenuDraftStatus.Saved, prior.RecordId, prior.MenuId, prior.Revision)
                    : new(MenuDraftStatus.Invalid, Error: "menu.request.reused");
            }

            RestaurantMenu menu;
            MenuDraft draft;
            Guid? recordId;
            if (command.Action == MenuDraftAction.CreateMenu)
            {
                menu = RestaurantMenu.Create(tenantId, command.Code, command.Name);
                draft = MenuDraft.Create(tenantId, menu.Id, clock.GetUtcNow().UtcDateTime);
                context.RestaurantMenus.Add(menu);
                context.MenuDrafts.Add(draft);
                recordId = menu.Id;
            }
            else
            {
                menu = (await context.RestaurantMenus.SingleOrDefaultAsync(x =>
                    x.TenantId == tenantId && x.Id == command.MenuId && !x.IsArchived,
                    cancellationToken))!;
                if (menu is null)
                {
                    return new(MenuDraftStatus.Missing);
                }

                draft = (await context.MenuDrafts.SingleOrDefaultAsync(x =>
                    x.TenantId == tenantId && x.MenuId == menu.Id, cancellationToken))!;
                if (draft is null)
                {
                    return new(MenuDraftStatus.Missing);
                }

                if (command.ExpectedRevision != draft.Revision)
                {
                    return new(MenuDraftStatus.Conflict, Revision: draft.Revision,
                        Error: "menu.draft.stale");
                }

                MenuDraftMutationResult operation = await MutateAsync(tenantId,
                    menu, command, cancellationToken);
                if (operation.Status != MenuDraftStatus.Saved)
                {
                    return operation;
                }

                recordId = operation.RecordId;
                draft.Touch(clock.GetUtcNow().UtcDateTime);
            }

            context.MenuDraftOperationReceipts.Add(new MenuDraftOperationReceipt(
                tenantId, command.RequestId, menu.Id, recordId, draft.Revision,
                hash, userId, clock.GetUtcNow().UtcDateTime));

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            logger.LogInformation("Menu draft write succeeded {TenantId} {UserId} {MenuId} {Action} {RequestId} {Revision}",
                tenantId, userId, menu.Id, command.Action, command.RequestId, draft.Revision);
            return new(MenuDraftStatus.Saved, recordId, menu.Id, draft.Revision);
        }
        catch (DbUpdateConcurrencyException)
        {
            logger.LogWarning("Menu draft concurrency conflict {TenantId} {UserId} {Action}",
                tenantId, userId, command.Action);
            return new(MenuDraftStatus.Conflict, Error: "menu.draft.stale");
        }
        catch (DbUpdateException ex) when (SqlError(ex, 2601, 2627))
        {
            return new(MenuDraftStatus.Duplicate, Error: "menu.duplicate");
        }
        catch (DbUpdateException ex) when (SqlError(ex, 547))
        {
            return new(MenuDraftStatus.Invalid, Error: "menu.reference.invalid");
        }
        catch (SqlException ex) when (ex.Number == 1205)
        {
            return new(MenuDraftStatus.Conflict, Error: "menu.concurrent.retry");
        }
        finally
        {
            // Καθαρισμός tracked entities ώστε κανένα αποτυχημένο INSERT να μην επανεκτελεστεί.
            context.ChangeTracker.Clear();
        }
    }

    private async Task<MenuDraftMutationResult> MutateAsync(Guid tenantId,
        RestaurantMenu menu, MenuDraftCommand command, CancellationToken token)
    {
        switch (command.Action)
        {
            case MenuDraftAction.UpdateMenu:
                menu.Rename(command.Name);
                return new(MenuDraftStatus.Saved, menu.Id);

            case MenuDraftAction.AddCategory:
                {
                    int count = await context.MenuDraftCategories.CountAsync(x =>
                        x.TenantId == tenantId && x.MenuId == menu.Id, token);
                    if (count >= 10000)
                    {
                        return new(MenuDraftStatus.Invalid, Error: "menu.category.limit");
                    }
                    MenuDraftCategory category = MenuDraftCategory.Create(
                        tenantId, menu.Id, command.Name, count + 1);
                    context.MenuDraftCategories.Add(category);
                    return new(MenuDraftStatus.Saved, category.Id);
                }
            case MenuDraftAction.UpdateCategory:
                {
                    MenuDraftCategory? category = await FindCategoryAsync(tenantId,
                        menu.Id, command.CategoryId!.Value, token);
                    if (category is null)
                    {
                        return new(MenuDraftStatus.Missing);
                    }
                    category.Update(command.Name, command.IsVisible!.Value);
                    return new(MenuDraftStatus.Saved, category.Id);
                }
            case MenuDraftAction.MoveCategory:
                {
                    List<MenuDraftCategory> rows = await context.MenuDraftCategories
                        .Where(x => x.TenantId == tenantId && x.MenuId == menu.Id)
                        .OrderBy(x => x.Position).ThenBy(x => x.Id).ToListAsync(token);
                    int previous = rows.FindIndex(x => x.Id == command.CategoryId);
                    if (previous < 0)
                    {
                        return new(MenuDraftStatus.Missing);
                    }
                    int target = Math.Min(command.TargetPosition!.Value, rows.Count) - 1;
                    MenuDraftCategory selected = rows[previous];
                    rows.RemoveAt(previous);
                    rows.Insert(target, selected);
                    for (int i = 0; i < rows.Count; i++)
                    {
                        rows[i].SetPosition(i + 1);
                    }
                    return new(MenuDraftStatus.Saved, selected.Id);
                }
            case MenuDraftAction.RemoveCategory:
                {
                    MenuDraftCategory? category = await FindCategoryAsync(tenantId,
                        menu.Id, command.CategoryId!.Value, token);
                    if (category is null)
                    {
                        return new(MenuDraftStatus.Missing);
                    }

                    bool hasItems = await context.MenuDraftItems.AnyAsync(x =>
                        x.TenantId == tenantId && x.MenuId == menu.Id &&
                        x.MenuCategoryId == category.Id, token);
                    if (hasItems)
                    {
                        return new(MenuDraftStatus.Invalid, Error: "menu.category.not_empty");
                    }

                    context.MenuDraftCategories.Remove(category);
                    List<MenuDraftCategory> remaining = await context.MenuDraftCategories
                        .Where(x => x.TenantId == tenantId && x.MenuId == menu.Id &&
                            x.Id != category.Id)
                        .OrderBy(x => x.Position).ThenBy(x => x.Id).ToListAsync(token);
                    for (int i = 0; i < remaining.Count; i++)
                    {
                        remaining[i].SetPosition(i + 1);
                    }
                    return new(MenuDraftStatus.Saved, category.Id);
                }
            case MenuDraftAction.AddItem:
                {
                    if (!await context.MenuDraftCategories.AnyAsync(x =>
                            x.TenantId == tenantId && x.MenuId == menu.Id &&
                            x.Id == command.CategoryId, token))
                    {
                        return new(MenuDraftStatus.Missing);
                    }
                    bool eligible = await context.CatalogItems.AnyAsync(x =>
                        x.TenantId == tenantId && x.Id == command.CatalogItemId &&
                        x.IsActive, token);
                    if (!eligible)
                    {
                        return new(MenuDraftStatus.Invalid, Error: "menu.catalog.unavailable");
                    }
                    bool duplicate = await context.MenuDraftItems.AnyAsync(x =>
                        x.TenantId == tenantId && x.MenuId == menu.Id &&
                        x.MenuCategoryId == command.CategoryId &&
                        x.CatalogItemId == command.CatalogItemId, token);
                    if (duplicate)
                    {
                        return new(MenuDraftStatus.Duplicate, Error: "menu.item.duplicate");
                    }
                    int count = await context.MenuDraftItems.CountAsync(x =>
                        x.TenantId == tenantId && x.MenuId == menu.Id &&
                        x.MenuCategoryId == command.CategoryId, token);
                    if (count >= 10000)
                    {
                        return new(MenuDraftStatus.Invalid, Error: "menu.item.limit");
                    }
                    MenuDraftItem item = MenuDraftItem.Create(tenantId, menu.Id,
                        command.CategoryId!.Value, command.CatalogItemId!.Value,
                        count + 1, command.NameOverride);
                    context.MenuDraftItems.Add(item);
                    return new(MenuDraftStatus.Saved, item.Id);
                }
            case MenuDraftAction.RenameItem:
                {
                    MenuDraftItem? item = await FindItemAsync(tenantId, menu.Id,
                        command.CategoryId!.Value, command.ItemId!.Value, token);
                    if (item is null)
                    {
                        return new(MenuDraftStatus.Missing);
                    }
                    item.Rename(command.NameOverride);
                    return new(MenuDraftStatus.Saved, item.Id);
                }
            case MenuDraftAction.MoveItem:
                {
                    List<MenuDraftItem> rows = await context.MenuDraftItems
                        .Where(x => x.TenantId == tenantId && x.MenuId == menu.Id &&
                            x.MenuCategoryId == command.CategoryId)
                        .OrderBy(x => x.Position).ThenBy(x => x.Id).ToListAsync(token);
                    int previous = rows.FindIndex(x => x.Id == command.ItemId);
                    if (previous < 0)
                    {
                        return new(MenuDraftStatus.Missing);
                    }
                    int target = Math.Min(command.TargetPosition!.Value, rows.Count) - 1;
                    MenuDraftItem selected = rows[previous];
                    rows.RemoveAt(previous);
                    rows.Insert(target, selected);
                    for (int i = 0; i < rows.Count; i++)
                    {
                        rows[i].SetPosition(i + 1);
                    }
                    return new(MenuDraftStatus.Saved, selected.Id);
                }
            case MenuDraftAction.RemoveItem:
                {
                    MenuDraftItem? item = await FindItemAsync(tenantId, menu.Id,
                        command.CategoryId!.Value, command.ItemId!.Value, token);
                    if (item is null)
                    {
                        return new(MenuDraftStatus.Missing);
                    }
                    context.MenuDraftItems.Remove(item);
                    List<MenuDraftItem> remaining = await context.MenuDraftItems
                        .Where(x => x.TenantId == tenantId && x.MenuId == menu.Id &&
                            x.MenuCategoryId == command.CategoryId && x.Id != item.Id)
                        .OrderBy(x => x.Position).ThenBy(x => x.Id).ToListAsync(token);
                    for (int i = 0; i < remaining.Count; i++)
                    {
                        remaining[i].SetPosition(i + 1);
                    }
                    return new(MenuDraftStatus.Saved, item.Id);
                }
            default:
                return new(MenuDraftStatus.Invalid, Error: "menu.action.invalid");
        }
    }

    private Task<MenuDraftCategory?> FindCategoryAsync(Guid tenantId, Guid menuId,
        Guid categoryId, CancellationToken token) =>
        context.MenuDraftCategories.SingleOrDefaultAsync(x =>
            x.TenantId == tenantId && x.MenuId == menuId && x.Id == categoryId, token);

    private Task<MenuDraftItem?> FindItemAsync(Guid tenantId, Guid menuId,
        Guid categoryId, Guid itemId, CancellationToken token) =>
        context.MenuDraftItems.SingleOrDefaultAsync(x =>
            x.TenantId == tenantId && x.MenuId == menuId && x.MenuCategoryId == categoryId &&
            x.Id == itemId, token);

    private static string RequestHash(MenuDraftCommand command)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(command)));
        return Convert.ToHexString(bytes);
    }

    private static bool SqlError(DbUpdateException ex, params int[] numbers) =>
        ex.InnerException is SqlException sql && numbers.Contains(sql.Number);
}
