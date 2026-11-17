using Aenaon.Application.Menus;
using Aenaon.Domain.Catalog;
using Aenaon.Domain.Menus;
using Aenaon.Domain.Tenancy;
using Aenaon.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Infrastructure.Tests;

/// <summary>Επαληθεύει την πραγματική αυτόματη εισαγωγή ERP και την ακεραιότητα των προσχεδίων σε απομονωμένο SQL Server.</summary>
internal static class SqlServerMenuDraftAssertions
{
    public static async Task RunAsync(AenaonDbContext context, Guid tenant,
        Guid foreignTenant, CancellationToken token)
    {
        // Ξεχωριστή επιχείρηση για να μην αλλοιώνονται τα fixtures των τιμοκαταλόγων.
        Tenant menuTenant = Tenant.Create("QA Menu Import");
        Guid menuTenantId = menuTenant.Id;
        CatalogCategory parent = CatalogCategory.Create(menuTenantId, "FOOD", "Φαγητά");
        CatalogCategory child = CatalogCategory.Create(menuTenantId, "MAIN", "Κυρίως");
        child.ChangeParent(parent.Id);
        CatalogCategory empty = CatalogCategory.Create(menuTenantId, "DRINKS", "Ποτά");
        CatalogCategory inactive = CatalogCategory.Create(menuTenantId, "DISABLED", "Ανενεργά");
        inactive.ChangeActive(false);

        CatalogItem activeParent = CatalogItem.Create(menuTenantId, parent.Id,
            "P001", "Σαλάτα", CatalogItemKind.Product, false);
        CatalogItem activeChild = CatalogItem.Create(menuTenantId, child.Id,
            "P002", "Πιάτο", CatalogItemKind.Product, false);
        CatalogItem inactiveProduct = CatalogItem.Create(menuTenantId, parent.Id,
            "P003", "Ανενεργό είδος", CatalogItemKind.Product, false);
        inactiveProduct.ChangeActive(false);
        CatalogItem productInInactiveCategory = CatalogItem.Create(menuTenantId, inactive.Id,
            "P004", "Είδος κρυφής κατηγορίας", CatalogItemKind.Product, false);

        context.Tenants.Add(menuTenant);
        context.CatalogCategories.AddRange(parent, child, empty, inactive);
        context.CatalogItems.AddRange(activeParent, activeChild, inactiveProduct,
            productInInactiveCategory);
        await context.SaveChangesAsync(token);
        context.ChangeTracker.Clear();

        var store = new SqlMenuDraftStore(context, TimeProvider.System,
            NullLogger<SqlMenuDraftStore>.Instance);
        Guid ownerId = Guid.NewGuid();

        // Δημιουργία μενού σημαίνει ταυτόχρονη μεταφορά ενεργών κατηγοριών ΚΑΙ ειδών.
        var createCommand = new MenuDraftCommand(MenuDraftAction.CreateMenu, Guid.NewGuid(),
            Code: "MAIN-MENU", Name: "Κυρίως Μενού");
        MenuDraftMutationResult created = await store.ApplyAsync(menuTenantId, ownerId,
            createCommand, token);
        Assert.AreEqual(MenuDraftStatus.Saved, created.Status);
        Assert.IsNotNull(created.MenuId);
        Guid menuId = created.MenuId.Value;
        Assert.AreEqual(1L, created.Revision);

        MenuDraftDetails? initial = await store.GetAsync(menuTenantId, menuId, token);
        Assert.IsNotNull(initial);
        Assert.HasCount(3, initial.Categories);
        Assert.HasCount(2, initial.Items);
        Assert.IsTrue(initial.Categories.Any(x => x.Name == "Φαγητά"));
        Assert.IsTrue(initial.Categories.Any(x => x.Name == "Φαγητά / Κυρίως"));
        Assert.IsTrue(initial.Categories.Any(x => x.Name == "Ποτά"));
        Assert.IsFalse(initial.Categories.Any(x => x.Name.Contains("Ανενεργά", StringComparison.Ordinal)));
        Assert.IsTrue(initial.Items.Any(x => x.CatalogItemId == activeParent.Id));
        Assert.IsTrue(initial.Items.Any(x => x.CatalogItemId == activeChild.Id));
        Assert.IsFalse(initial.Items.Any(x => x.CatalogItemId == inactiveProduct.Id));
        Assert.IsFalse(initial.Items.Any(x => x.CatalogItemId == productInInactiveCategory.Id));

        Guid foodId = initial.Categories.Single(x => x.Name == "Φαγητά").CategoryId;
        Guid mainId = initial.Categories.Single(x => x.Name == "Φαγητά / Κυρίως").CategoryId;
        Assert.AreEqual(foodId,
            initial.Items.Single(x => x.CatalogItemId == activeParent.Id).CategoryId);
        Assert.AreEqual(mainId,
            initial.Items.Single(x => x.CatalogItemId == activeChild.Id).CategoryId);

        // Ο ERP Catalog δεν τροποποιείται και κανένα είδος δεν παίρνει τιμή στο προσχέδιο.
        Assert.AreEqual(4, await context.CatalogItems.CountAsync(x =>
            x.TenantId == menuTenantId, token));
        Assert.AreEqual(4, await context.CatalogCategories.CountAsync(x =>
            x.TenantId == menuTenantId, token));

        MenuDraftMutationResult repeated = await store.ApplyAsync(menuTenantId, ownerId,
            createCommand, token);
        Assert.AreEqual(MenuDraftStatus.Saved, repeated.Status);
        Assert.AreEqual(menuId, repeated.MenuId);
        Assert.AreEqual(1, await context.RestaurantMenus.CountAsync(x =>
            x.TenantId == menuTenantId && x.Id == menuId, token));
        Assert.AreEqual(3, await context.MenuDraftCategories.CountAsync(x =>
            x.TenantId == menuTenantId && x.MenuId == menuId, token));
        Assert.AreEqual(2, await context.MenuDraftItems.CountAsync(x =>
            x.TenantId == menuTenantId && x.MenuId == menuId, token));

        MenuDraftMutationResult duplicateCode = await store.ApplyAsync(menuTenantId, ownerId,
            new(MenuDraftAction.CreateMenu, Guid.NewGuid(), Code: "main-menu", Name: "Άλλο"),
            token);
        Assert.AreEqual(MenuDraftStatus.Duplicate, duplicateCode.Status);
        Assert.HasCount(1, await store.ListAsync(menuTenantId, token));
        Assert.IsNull(await store.GetAsync(foreignTenant, menuId, token));

        // Η εκ νέου εισαγωγή σε μη κενό μενού αποκλείεται χωρίς καμία εγγραφή.
        MenuDraftMutationResult importAgain = await store.ApplyAsync(menuTenantId, ownerId,
            new(MenuDraftAction.ImportCatalogContents, Guid.NewGuid(),
                MenuId: menuId, ExpectedRevision: 1), token);
        Assert.AreEqual(MenuDraftStatus.Duplicate, importAgain.Status);
        Assert.AreEqual(1L, (await store.GetAsync(menuTenantId, menuId, token))!.Menu.Revision);

        // Ο Owner εξακολουθεί να μπορεί να προσθέτει αποκλειστικά δική του κατηγορία.
        MenuDraftMutationResult custom = await store.ApplyAsync(menuTenantId, ownerId,
            new(MenuDraftAction.AddCategory, Guid.NewGuid(), MenuId: menuId,
                ExpectedRevision: 1, Name: "Προσωπική κατηγορία"), token);
        Assert.AreEqual(MenuDraftStatus.Saved, custom.Status);
        Assert.AreEqual(2L, custom.Revision);

        MenuDraftMutationResult stale = await store.ApplyAsync(menuTenantId, ownerId,
            new(MenuDraftAction.AddCategory, Guid.NewGuid(), MenuId: menuId,
                ExpectedRevision: 1, Name: "Παλιό"), token);
        Assert.AreEqual(MenuDraftStatus.Conflict, stale.Status);

        Guid foreignProductId = await context.CatalogItems.AsNoTracking()
            .Where(x => x.TenantId == foreignTenant)
            .Select(x => x.Id).FirstAsync(token);
        MenuDraftMutationResult forbidden = await store.ApplyAsync(menuTenantId, ownerId,
            new(MenuDraftAction.AddItem, Guid.NewGuid(), MenuId: menuId,
                ExpectedRevision: 2, CategoryId: custom.RecordId,
                CatalogItemId: foreignProductId), token);
        Assert.AreEqual(MenuDraftStatus.Invalid, forbidden.Status);

        MenuDraftMutationResult item = await store.ApplyAsync(menuTenantId, ownerId,
            new(MenuDraftAction.AddItem, Guid.NewGuid(), MenuId: menuId,
                ExpectedRevision: 2, CategoryId: custom.RecordId,
                CatalogItemId: activeParent.Id), token);
        Assert.AreEqual(MenuDraftStatus.Saved, item.Status);
        Assert.AreEqual(3L, item.Revision);

        MenuDraftMutationResult duplicatedItem = await store.ApplyAsync(menuTenantId, ownerId,
            new(MenuDraftAction.AddItem, Guid.NewGuid(), MenuId: menuId,
                ExpectedRevision: 3, CategoryId: custom.RecordId,
                CatalogItemId: activeParent.Id), token);
        Assert.AreEqual(MenuDraftStatus.Duplicate, duplicatedItem.Status);

        MenuDraftDetails? details = await store.GetAsync(menuTenantId, menuId, token);
        Assert.IsNotNull(details);
        Assert.HasCount(4, details.Categories);
        Assert.HasCount(3, details.Items);
        Assert.AreEqual(activeParent.Id,
            details.Items.Single(x => x.CategoryId == custom.RecordId).CatalogItemId);
        Assert.AreEqual(3L, details.Menu.Revision);
        Assert.HasCount(0, (await store.SearchCatalogAsync(foreignTenant, "P001", token))
            .Where(x => x.CatalogItemId == activeParent.Id).ToArray());

        MenuDraftMutationResult removeBlocked = await store.ApplyAsync(menuTenantId, ownerId,
            new(MenuDraftAction.RemoveCategory, Guid.NewGuid(), MenuId: menuId,
                ExpectedRevision: 3, CategoryId: custom.RecordId), token);
        Assert.AreEqual(MenuDraftStatus.Invalid, removeBlocked.Status);

        MenuDraftMutationResult removed = await store.ApplyAsync(menuTenantId, ownerId,
            new(MenuDraftAction.RemoveItem, Guid.NewGuid(), MenuId: menuId,
                ExpectedRevision: 3, CategoryId: custom.RecordId, ItemId: item.RecordId), token);
        Assert.AreEqual(MenuDraftStatus.Saved, removed.Status);
        Assert.IsTrue(await context.CatalogItems.AsNoTracking()
            .AnyAsync(x => x.TenantId == menuTenantId && x.Id == activeParent.Id, token));

        MenuDraftMutationResult categoryRemoved = await store.ApplyAsync(menuTenantId, ownerId,
            new(MenuDraftAction.RemoveCategory, Guid.NewGuid(), MenuId: menuId,
                ExpectedRevision: 4, CategoryId: custom.RecordId), token);
        Assert.AreEqual(MenuDraftStatus.Saved, categoryRemoved.Status);
        Assert.HasCount(3, (await store.GetAsync(menuTenantId, menuId, token))!.Categories);

        // Υφιστάμενο, παλαιότερο κενό προσχέδιο: εισάγεται ο ίδιος κατάλογος ατομικά.
        RestaurantMenu legacy = RestaurantMenu.Create(menuTenantId, "OLD-EMPTY", "Παλιό κενό");
        context.RestaurantMenus.Add(legacy);
        context.MenuDrafts.Add(MenuDraft.Create(menuTenantId, legacy.Id, DateTime.UtcNow));
        await context.SaveChangesAsync(token);
        context.ChangeTracker.Clear();

        var legacyImport = new MenuDraftCommand(MenuDraftAction.ImportCatalogContents,
            Guid.NewGuid(), MenuId: legacy.Id, ExpectedRevision: 1);
        MenuDraftMutationResult migrated = await store.ApplyAsync(menuTenantId, ownerId,
            legacyImport, token);
        Assert.AreEqual(MenuDraftStatus.Saved, migrated.Status);
        Assert.AreEqual(2L, migrated.Revision);
        MenuDraftDetails? legacyDetails = await store.GetAsync(menuTenantId, legacy.Id, token);
        Assert.IsNotNull(legacyDetails);
        Assert.HasCount(3, legacyDetails.Categories);
        Assert.HasCount(2, legacyDetails.Items);

        MenuDraftMutationResult sameRequest = await store.ApplyAsync(menuTenantId, ownerId,
            legacyImport, token);
        Assert.AreEqual(MenuDraftStatus.Saved, sameRequest.Status);
        Assert.AreEqual(2L, sameRequest.Revision);
        Assert.AreEqual(2, await context.MenuDraftItems.CountAsync(x =>
            x.TenantId == menuTenantId && x.MenuId == legacy.Id, token));

        // Το σύνθετο SQL FK απορρίπτει σύνδεση με προϊόν άλλου Tenant.
        RestaurantMenu fkMenu = RestaurantMenu.Create(menuTenantId, "FK-PROBE", "Έλεγχος FK");
        MenuDraftCategory fkCategory = MenuDraftCategory.Create(menuTenantId, fkMenu.Id,
            "Test", 1);
        context.RestaurantMenus.Add(fkMenu);
        context.MenuDrafts.Add(MenuDraft.Create(menuTenantId, fkMenu.Id, DateTime.UtcNow));
        context.MenuDraftCategories.Add(fkCategory);
        await context.SaveChangesAsync(token);
        context.ChangeTracker.Clear();

        context.MenuDraftItems.Add(MenuDraftItem.Create(menuTenantId, fkMenu.Id,
            fkCategory.Id, foreignProductId, 1));
        DbUpdateException crossTenant = await Assert.ThrowsExactlyAsync<DbUpdateException>(
            () => context.SaveChangesAsync(token));
        Assert.IsInstanceOfType<SqlException>(crossTenant.InnerException);
        Assert.AreEqual(547, ((SqlException)crossTenant.InnerException).Number);
        context.ChangeTracker.Clear();
    }
}
