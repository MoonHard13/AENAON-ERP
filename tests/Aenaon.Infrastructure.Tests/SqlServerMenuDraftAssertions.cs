using Aenaon.Application.Menus;
using Aenaon.Domain.Menus;
using Aenaon.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Infrastructure.Tests;

/// <summary>Πραγματικές δοκιμές SQL Server 2022 αποκλειστικά στην απομονωμένη CI βάση.</summary>
internal static class SqlServerMenuDraftAssertions
{
    public static async Task RunAsync(AenaonDbContext context, Guid tenant,
        Guid foreignTenant, CancellationToken token)
    {
        var store = new SqlMenuDraftStore(context, TimeProvider.System,
            NullLogger<SqlMenuDraftStore>.Instance);
        Guid ownerId = Guid.NewGuid();

        // Τα αιτήματα προστατεύονται με τον ίδιο RequestId και δεν δημιουργούν διπλή εγγραφή.
        var createCommand = new MenuDraftCommand(MenuDraftAction.CreateMenu, Guid.NewGuid(),
            Code: "MAIN-MENU", Name: "Κυρίως Μενού");
        MenuDraftMutationResult created = await store.ApplyAsync(tenant, ownerId,
            createCommand, token);
        Assert.AreEqual(MenuDraftStatus.Saved, created.Status);
        Assert.IsNotNull(created.MenuId);
        Guid menuId = created.MenuId.Value;
        Assert.AreEqual(1L, created.Revision);

        MenuDraftMutationResult repeated = await store.ApplyAsync(tenant, ownerId,
            createCommand, token);
        Assert.AreEqual(MenuDraftStatus.Saved, repeated.Status);
        Assert.AreEqual(menuId, repeated.MenuId);
        Assert.AreEqual(1, await context.RestaurantMenus.CountAsync(x =>
            x.TenantId == tenant && x.Id == menuId, token));

        MenuDraftMutationResult duplicateCode = await store.ApplyAsync(tenant, ownerId,
            new(MenuDraftAction.CreateMenu, Guid.NewGuid(), Code: "main-menu", Name: "Άλλο"),
            token);
        Assert.AreEqual(MenuDraftStatus.Duplicate, duplicateCode.Status);
        Assert.HasCount(1, await store.ListAsync(tenant, token));
        Assert.HasCount(0, await store.ListAsync(foreignTenant, token));
        Assert.IsNull(await store.GetAsync(foreignTenant, menuId, token));

        MenuDraftMutationResult category = await store.ApplyAsync(tenant, ownerId,
            new(MenuDraftAction.AddCategory, Guid.NewGuid(), MenuId: menuId,
                ExpectedRevision: 1, Name: "Ορεκτικά"), token);
        Assert.AreEqual(MenuDraftStatus.Saved, category.Status);
        Assert.AreEqual(2L, category.Revision);
        Assert.IsNotNull(category.RecordId);

        MenuDraftMutationResult stale = await store.ApplyAsync(tenant, ownerId,
            new(MenuDraftAction.AddCategory, Guid.NewGuid(), MenuId: menuId,
                ExpectedRevision: 1, Name: "Παλιό"), token);
        Assert.AreEqual(MenuDraftStatus.Conflict, stale.Status);

        Guid productId = await context.CatalogItems.AsNoTracking()
            .Where(x => x.TenantId == tenant && x.IsActive)
            .Select(x => x.Id).FirstAsync(token);
        Guid foreignProductId = await context.CatalogItems.AsNoTracking()
            .Where(x => x.TenantId == foreignTenant)
            .Select(x => x.Id).FirstAsync(token);

        MenuDraftMutationResult foreign = await store.ApplyAsync(tenant, ownerId,
            new(MenuDraftAction.AddItem, Guid.NewGuid(), MenuId: menuId,
                ExpectedRevision: 2, CategoryId: category.RecordId,
                CatalogItemId: foreignProductId), token);
        Assert.AreEqual(MenuDraftStatus.Invalid, foreign.Status);

        MenuDraftMutationResult item = await store.ApplyAsync(tenant, ownerId,
            new(MenuDraftAction.AddItem, Guid.NewGuid(), MenuId: menuId,
                ExpectedRevision: 2, CategoryId: category.RecordId,
                CatalogItemId: productId), token);
        Assert.AreEqual(MenuDraftStatus.Saved, item.Status);
        Assert.AreEqual(3L, item.Revision);

        MenuDraftMutationResult duplicatedItem = await store.ApplyAsync(tenant, ownerId,
            new(MenuDraftAction.AddItem, Guid.NewGuid(), MenuId: menuId,
                ExpectedRevision: 3, CategoryId: category.RecordId,
                CatalogItemId: productId), token);
        Assert.AreEqual(MenuDraftStatus.Duplicate, duplicatedItem.Status);

        MenuDraftDetails? details = await store.GetAsync(tenant, menuId, token);
        Assert.IsNotNull(details);
        Assert.HasCount(1, details.Categories);
        Assert.HasCount(1, details.Items);
        Assert.AreEqual(productId, details.Items[0].CatalogItemId);
        Assert.AreEqual(3L, details.Menu.Revision);
        Assert.HasCount(0, (await store.SearchCatalogAsync(foreignTenant, "SKU01", token))
            .Where(x => x.CatalogItemId == productId).ToArray());

        MenuDraftMutationResult removeBlocked = await store.ApplyAsync(tenant, ownerId,
            new(MenuDraftAction.RemoveCategory, Guid.NewGuid(), MenuId: menuId,
                ExpectedRevision: 3, CategoryId: category.RecordId), token);
        Assert.AreEqual(MenuDraftStatus.Invalid, removeBlocked.Status);

        MenuDraftMutationResult removed = await store.ApplyAsync(tenant, ownerId,
            new(MenuDraftAction.RemoveItem, Guid.NewGuid(), MenuId: menuId,
                ExpectedRevision: 3, CategoryId: category.RecordId, ItemId: item.RecordId),
            token);
        Assert.AreEqual(MenuDraftStatus.Saved, removed.Status);
        Assert.IsTrue(await context.CatalogItems.AsNoTracking()
            .AnyAsync(x => x.TenantId == tenant && x.Id == productId, token));

        MenuDraftMutationResult categoryRemoved = await store.ApplyAsync(tenant, ownerId,
            new(MenuDraftAction.RemoveCategory, Guid.NewGuid(), MenuId: menuId,
                ExpectedRevision: 4, CategoryId: category.RecordId), token);
        Assert.AreEqual(MenuDraftStatus.Saved, categoryRemoved.Status);

        // Άμεσο SQL insert με ξένο TenantId/CatalogItemId αποτυγχάνει από το σύνθετο FK.
        var fakeMenu = RestaurantMenu.Create(tenant, "FK-PROBE", "Έλεγχος FK");
        var draft = MenuDraft.Create(tenant, fakeMenu.Id, DateTime.UtcNow);
        var cat = MenuDraftCategory.Create(tenant, fakeMenu.Id, "Test", 1);
        context.RestaurantMenus.Add(fakeMenu);
        context.MenuDrafts.Add(draft);
        context.MenuDraftCategories.Add(cat);
        await context.SaveChangesAsync(token);
        context.ChangeTracker.Clear();

        var invalidItem = MenuDraftItem.Create(tenant, fakeMenu.Id, cat.Id,
            foreignProductId, 1);
        context.MenuDraftItems.Add(invalidItem);
        DbUpdateException crossTenant = await Assert.ThrowsExactlyAsync<DbUpdateException>(
            () => context.SaveChangesAsync(token));
        Assert.IsInstanceOfType<SqlException>(crossTenant.InnerException);
        Assert.AreEqual(547, ((SqlException)crossTenant.InnerException).Number);
        context.ChangeTracker.Clear();
    }
}
