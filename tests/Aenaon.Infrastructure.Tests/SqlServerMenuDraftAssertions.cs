using Aenaon.Application.Catalog;
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
        Assert.IsTrue(initial.Categories.Any(x => x.Name == "Κυρίως"));
        Assert.IsTrue(initial.Categories.Any(x => x.Name == "Ποτά"));
        Assert.IsFalse(initial.Categories.Any(x => x.Name.Contains("Ανενεργά", StringComparison.Ordinal)));
        Assert.IsTrue(initial.Items.Any(x => x.CatalogItemId == activeParent.Id));
        Assert.IsTrue(initial.Items.Any(x => x.CatalogItemId == activeChild.Id));
        Assert.IsFalse(initial.Items.Any(x => x.CatalogItemId == inactiveProduct.Id));
        Assert.IsFalse(initial.Items.Any(x => x.CatalogItemId == productInInactiveCategory.Id));

        Guid foodId = initial.Categories.Single(x => x.Name == "Φαγητά").CategoryId;
        Guid mainId = initial.Categories.Single(x => x.Name == "Κυρίως").CategoryId;
        Assert.AreEqual(foodId,
            initial.Items.Single(x => x.CatalogItemId == activeParent.Id).CategoryId);
        Assert.AreEqual(mainId,
            initial.Items.Single(x => x.CatalogItemId == activeChild.Id).CategoryId);
        Assert.AreEqual(foodId, initial.Categories.Single(x =>
            x.CategoryId == mainId).ParentCategoryId);
        Assert.AreEqual(2, initial.Categories.Count(x => x.ParentCategoryId is null));
        Assert.AreEqual(1, initial.Categories.Count(x => x.ParentCategoryId == foodId));

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

        // Η δημιουργία νέας ERP κατηγορίας συγχρονίζει ατομικά όλα τα μενού.
        var catalogWrite = new SqlCatalogWriteStore(context);
        Guid categoryRequestId = Guid.NewGuid();
        CatalogChangeResult newCategory = await catalogWrite.ApplyAsync(menuTenantId,
            new(CatalogManagedEntity.Category, CatalogChangeAction.Create,
                Code: "DESSERT", Name: "Γλυκά", RequestId: categoryRequestId), token);
        Assert.AreEqual(CatalogChangeStatus.Saved, newCategory.Status);
        Assert.IsNotNull(newCategory.Id);

        MenuDraftDetails? afterCategory = await store.GetAsync(menuTenantId, menuId, token);
        Assert.IsNotNull(afterCategory);
        Assert.HasCount(4, afterCategory.Categories);
        Assert.AreEqual(6L, afterCategory.Menu.Revision);
        Assert.IsTrue(afterCategory.Categories.Any(x => x.Name == "Γλυκά"));
        Guid newMenuCategoryId = await context.MenuDraftCategories
            .Where(x => x.TenantId == menuTenantId && x.MenuId == menuId &&
                x.CatalogCategoryId == newCategory.Id)
            .Select(x => x.Id).SingleAsync(token);
        Guid legacyMenuCategoryId = await context.MenuDraftCategories
            .Where(x => x.TenantId == menuTenantId && x.MenuId == legacy.Id &&
                x.CatalogCategoryId == newCategory.Id)
            .Select(x => x.Id).SingleAsync(token);
        Assert.AreNotEqual(newMenuCategoryId, legacyMenuCategoryId);

        // Retry με ίδιο idempotency token δεν προσθέτει δεύτερη κατηγορία.
        CatalogChangeResult duplicateCategoryRequest = await catalogWrite.ApplyAsync(menuTenantId,
            new(CatalogManagedEntity.Category, CatalogChangeAction.Create,
                Code: "DESSERT", Name: "Γλυκά", RequestId: categoryRequestId), token);
        Assert.AreEqual(CatalogChangeStatus.Saved, duplicateCategoryRequest.Status);
        Assert.AreEqual(newCategory.Id, duplicateCategoryRequest.Id);
        Assert.AreEqual(1, await context.MenuDraftCategories.CountAsync(x =>
            x.TenantId == menuTenantId && x.MenuId == menuId &&
            x.CatalogCategoryId == newCategory.Id, token));

        // Το νέο ERP είδος προστίθεται στο σωστό auto-synced MenuDraftCategory.
        CatalogChangeResult newProduct = await catalogWrite.ApplyAsync(menuTenantId,
            new(CatalogManagedEntity.Product, CatalogChangeAction.Create,
                Code: "P100", Name: "Γλυκό ημέρας", CategoryId: newCategory.Id,
                RequestId: Guid.NewGuid()), token);
        Assert.AreEqual(CatalogChangeStatus.Saved, newProduct.Status);
        Assert.IsNotNull(newProduct.Id);
        Assert.IsTrue(await context.MenuDraftItems.AsNoTracking().AnyAsync(x =>
            x.TenantId == menuTenantId && x.MenuId == menuId &&
            x.MenuCategoryId == newMenuCategoryId &&
            x.CatalogItemId == newProduct.Id, token));
        Assert.IsTrue(await context.MenuDraftItems.AsNoTracking().AnyAsync(x =>
            x.TenantId == menuTenantId && x.MenuId == legacy.Id &&
            x.MenuCategoryId == legacyMenuCategoryId &&
            x.CatalogItemId == newProduct.Id, token));
        Assert.IsFalse(await context.MenuDraftItems.AsNoTracking().AnyAsync(x =>
            x.TenantId == foreignTenant && x.CatalogItemId == newProduct.Id, token));

        // Υπάρχον κατάστημα παίρνει default menu μία φορά μετά από ρητή
        // Owner/CLI εντολή, χωρίς αλλαγή σε άλλα μενού ή ERP Catalog.
        Store branch = menuTenant.AddStore("BRANCH", "Δεύτερο κατάστημα");
        context.Stores.Add(branch);
        await context.SaveChangesAsync(token);
        context.ChangeTracker.Clear();

        var provision = new MenuDraftCommand(MenuDraftAction.EnsureStoreDefault,
            Guid.NewGuid(), StoreId: branch.Id);
        MenuDraftMutationResult provisioned = await store.ApplyAsync(menuTenantId,
            ownerId, provision, token);
        Assert.AreEqual(MenuDraftStatus.Saved, provisioned.Status);
        Assert.IsNotNull(provisioned.MenuId);
        Guid storeDefault = provisioned.MenuId.Value;
        Assert.AreEqual(storeDefault, await store.GetDefaultMenuIdAsync(
            menuTenantId, branch.Id, token));
        MenuDraftDetails? storeContent = await store.GetAsync(
            menuTenantId, storeDefault, token);
        Assert.IsNotNull(storeContent);
        Assert.AreEqual("DEFAULT-BRANCH", storeContent.Menu.Code);
        Assert.HasCount(4, storeContent.Categories);
        Assert.HasCount(3, storeContent.Items);

        MenuDraftMutationResult repeatedProvision = await store.ApplyAsync(
            menuTenantId, ownerId,
            new(MenuDraftAction.EnsureStoreDefault, Guid.NewGuid(), StoreId: branch.Id), token);
        Assert.AreEqual(MenuDraftStatus.Saved, repeatedProvision.Status);
        Assert.AreEqual(storeDefault, repeatedProvision.MenuId);
        Assert.AreEqual(1, await context.StoreDefaultMenus.CountAsync(x =>
            x.TenantId == menuTenantId && x.StoreId == branch.Id, token));
        Assert.AreEqual(4, await context.MenuDraftCategories.CountAsync(x =>
            x.TenantId == menuTenantId && x.MenuId == storeDefault, token));
        Assert.IsNull(await store.GetDefaultMenuIdAsync(foreignTenant, branch.Id, token));

        // Τα είδη μπορούν να αποθηκευτούν απευθείας στη ρίζα, χωρίς κατηγορία.
        long currentRevision = (await store.GetAsync(menuTenantId, menuId, token))!.Menu.Revision;
        MenuDraftMutationResult rootItem = await store.ApplyAsync(menuTenantId, ownerId,
            new(MenuDraftAction.AddItem, Guid.NewGuid(), MenuId: menuId,
                ExpectedRevision: currentRevision, CatalogItemId: activeParent.Id), token);
        Assert.AreEqual(MenuDraftStatus.Saved, rootItem.Status);
        Assert.IsTrue((await store.GetAsync(menuTenantId, menuId, token))!.Items.Any(x =>
            x.ItemId == rootItem.RecordId && x.CategoryId is null));
        MenuDraftMutationResult duplicateRoot = await store.ApplyAsync(menuTenantId, ownerId,
            new(MenuDraftAction.AddItem, Guid.NewGuid(), MenuId: menuId,
                ExpectedRevision: rootItem.Revision, CatalogItemId: activeParent.Id), token);
        Assert.AreEqual(MenuDraftStatus.Duplicate, duplicateRoot.Status);

        // Πλήρης κύκλος είδους ρίζας: δεύτερο είδος, αναδιάταξη, αλλαγή ονόματος και αφαίρεση.
        MenuDraftMutationResult secondRoot = await store.ApplyAsync(menuTenantId, ownerId,
            new(MenuDraftAction.AddItem, Guid.NewGuid(), MenuId: menuId,
                ExpectedRevision: rootItem.Revision, CatalogItemId: activeChild.Id), token);
        Assert.AreEqual(MenuDraftStatus.Saved, secondRoot.Status);

        MenuDraftMutationResult reorderedRoot = await store.ApplyAsync(menuTenantId, ownerId,
            new(MenuDraftAction.MoveItem, Guid.NewGuid(), MenuId: menuId,
                ExpectedRevision: secondRoot.Revision, ItemId: rootItem.RecordId,
                TargetPosition: 2), token);
        Assert.AreEqual(MenuDraftStatus.Saved, reorderedRoot.Status);
        Assert.AreEqual(2, (await store.GetAsync(menuTenantId, menuId, token))!.Items
            .Single(x => x.ItemId == rootItem.RecordId).Position);

        MenuDraftMutationResult renamedRoot = await store.ApplyAsync(menuTenantId, ownerId,
            new(MenuDraftAction.RenameItem, Guid.NewGuid(), MenuId: menuId,
                ExpectedRevision: reorderedRoot.Revision, ItemId: rootItem.RecordId,
                NameOverride: "Χωρίς κατηγορία"), token);
        Assert.AreEqual(MenuDraftStatus.Saved, renamedRoot.Status);
        Assert.AreEqual("Χωρίς κατηγορία", (await store.GetAsync(menuTenantId, menuId, token))!
            .Items.Single(x => x.ItemId == rootItem.RecordId).NameOverride);

        MenuDraftMutationResult removedRoot = await store.ApplyAsync(menuTenantId, ownerId,
            new(MenuDraftAction.RemoveItem, Guid.NewGuid(), MenuId: menuId,
                ExpectedRevision: renamedRoot.Revision, ItemId: rootItem.RecordId), token);
        Assert.AreEqual(MenuDraftStatus.Saved, removedRoot.Status);
        MenuDraftDetails rootAfterRemove = (await store.GetAsync(menuTenantId, menuId, token))!;
        Assert.IsFalse(rootAfterRemove.Items.Any(x => x.ItemId == rootItem.RecordId));
        Assert.AreEqual(1, rootAfterRemove.Items.Single(x =>
            x.ItemId == secondRoot.RecordId).Position);

        MenuDraftMutationResult staleRoot = await store.ApplyAsync(menuTenantId, ownerId,
            new(MenuDraftAction.RemoveItem, Guid.NewGuid(), MenuId: menuId,
                ExpectedRevision: renamedRoot.Revision, ItemId: secondRoot.RecordId), token);
        Assert.AreEqual(MenuDraftStatus.Conflict, staleRoot.Status);

        // Προσθήκη υποκατηγορίας μόνο κάτω από κατηγορία του ίδιου μενού.
        Guid drinksId = (await store.GetAsync(menuTenantId, menuId, token))!
            .Categories.Single(x => x.Name == "Ποτά").CategoryId;
        MenuDraftMutationResult subcategory = await store.ApplyAsync(menuTenantId, ownerId,
            new(MenuDraftAction.AddCategory, Guid.NewGuid(), MenuId: menuId,
                ExpectedRevision: removedRoot.Revision, Name: "Καφές",
                ParentCategoryId: drinksId), token);
        Assert.AreEqual(MenuDraftStatus.Saved, subcategory.Status);
        Assert.AreEqual(drinksId, (await store.GetAsync(menuTenantId, menuId, token))!
            .Categories.Single(x => x.CategoryId == subcategory.RecordId).ParentCategoryId);
        MenuDraftMutationResult blockedParent = await store.ApplyAsync(menuTenantId,
            ownerId, new(MenuDraftAction.RemoveCategory, Guid.NewGuid(), MenuId: menuId,
                ExpectedRevision: subcategory.Revision, CategoryId: drinksId), token);
        Assert.AreEqual(MenuDraftStatus.Invalid, blockedParent.Status);
        MenuDraftMutationResult crossMenuParent = await store.ApplyAsync(menuTenantId,
            ownerId, new(MenuDraftAction.AddCategory, Guid.NewGuid(), MenuId: legacy.Id,
                ExpectedRevision: (await store.GetAsync(menuTenantId, legacy.Id, token))!.Menu.Revision,
                Name: "Άκυρο", ParentCategoryId: drinksId), token);
        Assert.AreEqual(MenuDraftStatus.Missing, crossMenuParent.Status);

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

        // Το ανεξάρτητο FK απορρίπτει είδος ρίζας χωρίς υπαρκτό μενού.
        context.MenuDraftItems.Add(MenuDraftItem.Create(menuTenantId,
            Guid.NewGuid(), null, activeParent.Id, 1));
        DbUpdateException orphanRoot = await Assert.ThrowsExactlyAsync<DbUpdateException>(
            () => context.SaveChangesAsync(token));
        Assert.IsInstanceOfType<SqlException>(orphanRoot.InnerException);
        Assert.AreEqual(547, ((SqlException)orphanRoot.InnerException).Number);
        context.ChangeTracker.Clear();

        // Ο γονέας κατηγορίας πρέπει να ανήκει στο ίδιο Menu και Tenant.
        context.MenuDraftCategories.Add(MenuDraftCategory.Create(menuTenantId,
            fkMenu.Id, "Ασύνδετη υποκατηγορία", 1, parentCategoryId: foodId));
        DbUpdateException crossMenuParentFk = await Assert.ThrowsExactlyAsync<DbUpdateException>(
            () => context.SaveChangesAsync(token));
        Assert.IsInstanceOfType<SqlException>(crossMenuParentFk.InnerException);
        Assert.AreEqual(547, ((SqlException)crossMenuParentFk.InnerException).Number);
        context.ChangeTracker.Clear();

        await AssertBulkAddAndCategoryRemovalAsync(context, store, menuTenantId,
            ownerId, activeParent.Id, activeChild.Id, foreignProductId, token);
        await AssertCategoryFilterAndExistingCategoryLinkAsync(context, store,
            menuTenantId, ownerId, parent, child, empty, inactive, activeParent,
            activeChild, foodId, token);
    }

    private static async Task AssertCategoryFilterAndExistingCategoryLinkAsync(
        AenaonDbContext context, SqlMenuDraftStore store, Guid tenantId, Guid userId,
        CatalogCategory sourceParent, CatalogCategory sourceChild,
        CatalogCategory emptySource, CatalogCategory inactiveSource,
        CatalogItem parentItem, CatalogItem childItem,
        Guid foreignMenuParentId, CancellationToken token)
    {
        // Τα φίλτρα αναζητούν μόνο είδη της επιλεγμένης ERP κατηγορίας του Tenant.
        IReadOnlyList<MenuCatalogCategoryOption> categories =
            await store.ListCatalogCategoriesAsync(tenantId, token);
        Assert.IsTrue(categories.Any(x => x.CatalogCategoryId == sourceParent.Id));
        Assert.IsTrue(categories.Any(x => x.CatalogCategoryId == sourceChild.Id));
        Assert.IsFalse(categories.Any(x => x.CatalogCategoryId == inactiveSource.Id));

        IReadOnlyList<MenuCatalogOption> parentResults =
            await store.SearchCatalogAsync(tenantId, "P001", token, sourceParent.Id);
        Assert.HasCount(1, parentResults);
        Assert.AreEqual(parentItem.Id, parentResults[0].CatalogItemId);
        IReadOnlyList<MenuCatalogOption> wholeFoodBranch =
            await store.SearchCatalogAsync(tenantId, null, token, sourceParent.Id);
        Assert.HasCount(2, wholeFoodBranch);
        Assert.IsTrue(wholeFoodBranch.Any(x => x.CatalogItemId == parentItem.Id));
        Assert.IsTrue(wholeFoodBranch.Any(x => x.CatalogItemId == childItem.Id));
        Assert.HasCount(0, await store.SearchCatalogAsync(
            tenantId, "P001", token, sourceChild.Id));
        IReadOnlyList<MenuCatalogOption> childResults =
            await store.SearchCatalogAsync(tenantId, null, token, sourceChild.Id);
        Assert.HasCount(1, childResults);
        Assert.AreEqual(childItem.Id, childResults[0].CatalogItemId);
        Assert.HasCount(0, await store.SearchCatalogAsync(
            tenantId, null, token, inactiveSource.Id));
        Assert.HasCount(0, await store.SearchCatalogAsync(
            tenantId, null, token, Guid.NewGuid()));

        // Η νέα κατηγορία του μενού συνδέεται με το υπάρχον ERP record, χωρίς copy/delete.
        RestaurantMenu menu = RestaurantMenu.Create(tenantId, "ERP-RELINK", "Δοκιμή κατηγορίας");
        context.RestaurantMenus.Add(menu);
        context.MenuDrafts.Add(MenuDraft.Create(tenantId, menu.Id, DateTime.UtcNow));
        await context.SaveChangesAsync(token);
        context.ChangeTracker.Clear();

        var linkParent = new MenuDraftCommand(MenuDraftAction.LinkCatalogCategory,
            Guid.NewGuid(), MenuId: menu.Id, ExpectedRevision: 1,
            SourceCatalogCategoryId: sourceParent.Id);
        MenuDraftMutationResult linkedParent = await store.ApplyAsync(
            tenantId, userId, linkParent, token);
        Assert.AreEqual(MenuDraftStatus.Saved, linkedParent.Status);
        Assert.AreEqual(2L, linkedParent.Revision);

        // Replay με ίδιο RequestId δεν διπλασιάζει τη σύνδεση.
        MenuDraftMutationResult replay = await store.ApplyAsync(
            tenantId, userId, linkParent, token);
        Assert.AreEqual(MenuDraftStatus.Saved, replay.Status);
        Assert.AreEqual(linkedParent.RecordId, replay.RecordId);

        MenuDraftMutationResult duplicate = await store.ApplyAsync(
            tenantId, userId, linkParent with
            {
                RequestId = Guid.NewGuid(),
                ExpectedRevision = linkedParent.Revision
            }, token);
        Assert.AreEqual(MenuDraftStatus.Duplicate, duplicate.Status);

        MenuDraftMutationResult nested = await store.ApplyAsync(
            tenantId, userId,
            new(MenuDraftAction.LinkCatalogCategory, Guid.NewGuid(), MenuId: menu.Id,
                ExpectedRevision: linkedParent.Revision,
                ParentCategoryId: linkedParent.RecordId,
                SourceCatalogCategoryId: sourceChild.Id), token);
        Assert.AreEqual(MenuDraftStatus.Saved, nested.Status);
        MenuDraftDetails afterNested = (await store.GetAsync(tenantId, menu.Id, token))!;
        Assert.HasCount(2, afterNested.Categories);
        Assert.AreEqual(sourceChild.Id, afterNested.Categories.Single(x =>
            x.CategoryId == nested.RecordId).CatalogCategoryId);
        Assert.AreEqual(linkedParent.RecordId,
            afterNested.Categories.Single(x =>
                x.CategoryId == nested.RecordId).ParentCategoryId);
        Assert.HasCount(0, afterNested.Items);

        // Ξένη κατηγορία, ανενεργή ERP κατηγορία και γονέας άλλου μενού απορρίπτονται.
        MenuDraftMutationResult inactive = await store.ApplyAsync(
            tenantId, userId,
            new(MenuDraftAction.LinkCatalogCategory, Guid.NewGuid(), MenuId: menu.Id,
                ExpectedRevision: nested.Revision,
                SourceCatalogCategoryId: inactiveSource.Id), token);
        Assert.AreEqual(MenuDraftStatus.Missing, inactive.Status);
        MenuDraftMutationResult foreignSource = await store.ApplyAsync(
            tenantId, userId,
            new(MenuDraftAction.LinkCatalogCategory, Guid.NewGuid(), MenuId: menu.Id,
                ExpectedRevision: nested.Revision,
                SourceCatalogCategoryId: Guid.NewGuid()), token);
        Assert.AreEqual(MenuDraftStatus.Missing, foreignSource.Status);
        MenuDraftMutationResult crossMenuParent = await store.ApplyAsync(
            tenantId, userId,
            new(MenuDraftAction.LinkCatalogCategory, Guid.NewGuid(), MenuId: menu.Id,
                ExpectedRevision: nested.Revision,
                ParentCategoryId: foreignMenuParentId,
                SourceCatalogCategoryId: emptySource.Id), token);
        Assert.AreEqual(MenuDraftStatus.Missing, crossMenuParent.Status);

        // Αφαίρεση και επανασύνδεση ERP κατηγορίας επιτρέπεται στο ίδιο μενού.
        MenuDraftMutationResult removed = await store.ApplyAsync(
            tenantId, userId,
            new(MenuDraftAction.RemoveCategory, Guid.NewGuid(), MenuId: menu.Id,
                ExpectedRevision: nested.Revision, CategoryId: linkedParent.RecordId,
                RemoveContents: true), token);
        Assert.AreEqual(MenuDraftStatus.Saved, removed.Status);
        Assert.HasCount(0, (await store.GetAsync(tenantId, menu.Id, token))!.Categories);
        MenuDraftMutationResult relinked = await store.ApplyAsync(
            tenantId, userId, linkParent with
            {
                RequestId = Guid.NewGuid(),
                ExpectedRevision = removed.Revision
            }, token);
        Assert.AreEqual(MenuDraftStatus.Saved, relinked.Status);
        Assert.AreEqual(sourceParent.Id, (await store.GetAsync(tenantId, menu.Id, token))!
            .Categories.Single().CatalogCategoryId);
        Assert.IsTrue(await context.CatalogCategories.AsNoTracking().AnyAsync(x =>
            x.TenantId == tenantId && x.Id == sourceParent.Id, token));
    }

    private static async Task AssertBulkAddAndCategoryRemovalAsync(
        AenaonDbContext context, SqlMenuDraftStore store, Guid tenantId,
        Guid userId, Guid firstCatalogItem, Guid secondCatalogItem,
        Guid foreignCatalogItem, CancellationToken token)
    {
        // Δοκιμές σε νέο ανεξάρτητο draft, ώστε να μη μεταβληθούν άλλα fixtures.
        RestaurantMenu menu = RestaurantMenu.Create(tenantId, "BULK-REMOVE", "Μαζική επιλογή");
        context.RestaurantMenus.Add(menu);
        context.MenuDrafts.Add(MenuDraft.Create(tenantId, menu.Id, DateTime.UtcNow));
        await context.SaveChangesAsync(token);
        context.ChangeTracker.Clear();

        var batch = new MenuDraftCommand(MenuDraftAction.AddItems, Guid.NewGuid(),
            MenuId: menu.Id, ExpectedRevision: 1,
            CatalogItemIds: [firstCatalogItem, secondCatalogItem]);
        MenuDraftMutationResult added = await store.ApplyAsync(tenantId, userId, batch, token);
        Assert.AreEqual(MenuDraftStatus.Saved, added.Status);
        Assert.AreEqual(2L, added.Revision);
        MenuDraftDetails afterAdd = (await store.GetAsync(tenantId, menu.Id, token))!;
        Assert.HasCount(2, afterAdd.Items);
        Assert.IsTrue(afterAdd.Items.All(x => x.CategoryId is null));
        CollectionAssert.AreEquivalent(new[] { 1, 2 },
            afterAdd.Items.Select(x => x.Position).ToArray());

        // Ίδιο RequestId επιστρέφει την αρχική επιτυχία, χωρίς δεύτερη εισαγωγή.
        MenuDraftMutationResult replay = await store.ApplyAsync(tenantId, userId, batch, token);
        Assert.AreEqual(MenuDraftStatus.Saved, replay.Status);
        Assert.AreEqual(2L, replay.Revision);
        Assert.AreEqual(2, await context.MenuDraftItems.CountAsync(x =>
            x.TenantId == tenantId && x.MenuId == menu.Id, token));

        // Κανένα μέρος του batch δεν καταχωρίζεται αν ένα είδος είναι ξένου Tenant.
        MenuDraftMutationResult invalid = await store.ApplyAsync(tenantId, userId,
            new(MenuDraftAction.AddItems, Guid.NewGuid(), MenuId: menu.Id,
                ExpectedRevision: 2,
                CatalogItemIds: [firstCatalogItem, foreignCatalogItem]), token);
        Assert.AreEqual(MenuDraftStatus.Invalid, invalid.Status);
        Assert.AreEqual(2L, (await store.GetAsync(tenantId, menu.Id, token))!.Menu.Revision);
        Assert.AreEqual(2, await context.MenuDraftItems.CountAsync(x =>
            x.TenantId == tenantId && x.MenuId == menu.Id, token));

        // Το ίδιο είδος στην ίδια θέση απορρίπτεται ατομικά.
        MenuDraftMutationResult duplicate = await store.ApplyAsync(tenantId, userId,
            new(MenuDraftAction.AddItems, Guid.NewGuid(), MenuId: menu.Id,
                ExpectedRevision: 2,
                CatalogItemIds: [firstCatalogItem, secondCatalogItem]), token);
        Assert.AreEqual(MenuDraftStatus.Duplicate, duplicate.Status);

        MenuDraftCategory parent = MenuDraftCategory.Create(tenantId, menu.Id,
            "Προσωπικό μενού", 1);
        MenuDraftCategory sibling = MenuDraftCategory.Create(tenantId, menu.Id,
            "Ξεχωριστή κατηγορία", 2);
        MenuDraftCategory child = MenuDraftCategory.Create(tenantId, menu.Id,
            "Υποκατηγορία", 1, parentCategoryId: parent.Id);
        MenuDraftCategory grandchild = MenuDraftCategory.Create(tenantId, menu.Id,
            "Βαθύτερο επίπεδο", 1, parentCategoryId: child.Id);
        context.MenuDraftCategories.AddRange(parent, sibling, child, grandchild);
        await context.SaveChangesAsync(token);
        context.ChangeTracker.Clear();

        MenuDraftMutationResult childAdded = await store.ApplyAsync(tenantId, userId,
            new(MenuDraftAction.AddItems, Guid.NewGuid(), MenuId: menu.Id,
                CategoryId: child.Id, ExpectedRevision: 2,
                CatalogItemIds: [firstCatalogItem, secondCatalogItem]), token);
        Assert.AreEqual(MenuDraftStatus.Saved, childAdded.Status);
        MenuDraftMutationResult deepAdded = await store.ApplyAsync(tenantId, userId,
            new(MenuDraftAction.AddItem, Guid.NewGuid(), MenuId: menu.Id,
                CategoryId: grandchild.Id, ExpectedRevision: childAdded.Revision,
                CatalogItemId: secondCatalogItem), token);
        Assert.AreEqual(MenuDraftStatus.Saved, deepAdded.Status);

        // Χωρίς ρητή επιβεβαίωση απαγορεύεται η διαγραφή μη άδειου δέντρου.
        var destructive = new MenuDraftCommand(MenuDraftAction.RemoveCategory, Guid.NewGuid(),
            MenuId: menu.Id, CategoryId: parent.Id, ExpectedRevision: deepAdded.Revision,
            RemoveContents: true);
        MenuDraftMutationResult blocked = await store.ApplyAsync(tenantId, userId,
            destructive with { RequestId = Guid.NewGuid(), RemoveContents = false }, token);
        Assert.AreEqual(MenuDraftStatus.Invalid, blocked.Status);
        Assert.HasCount(4, (await store.GetAsync(tenantId, menu.Id, token))!.Categories);

        // Δοκιμάζεται και το stale revision πριν από οποιαδήποτε διαγραφή.
        MenuDraftMutationResult staleRemoval = await store.ApplyAsync(tenantId, userId,
            destructive with { RequestId = Guid.NewGuid(), ExpectedRevision = 2 }, token);
        Assert.AreEqual(MenuDraftStatus.Conflict, staleRemoval.Status);

        // Ρητά εγκεκριμένη διαγραφή όλων των παιδιών + ειδών, μόνο στο επιλεγμένο μενού.
        MenuDraftMutationResult removed = await store.ApplyAsync(tenantId, userId, destructive, token);
        Assert.AreEqual(MenuDraftStatus.Saved, removed.Status);
        MenuDraftDetails afterRemoval = (await store.GetAsync(tenantId, menu.Id, token))!;
        Assert.HasCount(1, afterRemoval.Categories);
        Assert.AreEqual(sibling.Id, afterRemoval.Categories[0].CategoryId);
        Assert.AreEqual(1, afterRemoval.Categories[0].Position);
        Assert.HasCount(2, afterRemoval.Items);
        Assert.IsTrue(afterRemoval.Items.All(x => x.CategoryId is null));
        Assert.AreEqual(2, await context.MenuDraftItems.CountAsync(x =>
            x.TenantId == tenantId && x.MenuId == menu.Id, token));

        // Ο πραγματικός ERP Catalog διατηρείται, ανεξάρτητα από το προσχέδιο.
        Assert.IsTrue(await context.CatalogItems.AsNoTracking().AnyAsync(x =>
            x.TenantId == tenantId && x.Id == firstCatalogItem, token));
        Assert.IsTrue(await context.CatalogItems.AsNoTracking().AnyAsync(x =>
            x.TenantId == tenantId && x.Id == secondCatalogItem, token));
        Assert.AreEqual(removed.Revision,
            (await store.ApplyAsync(tenantId, userId, destructive, token)).Revision);
    }
}
