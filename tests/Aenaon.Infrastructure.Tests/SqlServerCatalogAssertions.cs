using System.Data;
using Aenaon.Application.Catalog;
using Aenaon.Domain.Catalog;
using Aenaon.Domain.Exceptions;
using Aenaon.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Infrastructure.Tests;

/// <summary>Ελέγχει πραγματικά SQL constraints και την επαναφόρτωση ειδών σε απομονωμένη βάση CI.</summary>
internal static class SqlServerCatalogAssertions
{
    public static async Task RunAsync(SqlConnection connection, AenaonDbContext context,
        Guid firstTenantId, Guid secondTenantId, CancellationToken cancellationToken)
    {
        var firstCatalog = ProductCatalog.Create(firstTenantId);
        var secondCatalog = ProductCatalog.Create(secondTenantId);
        var firstCategory = firstCatalog.AddCategory("FOOD", "Φαγητά");
        var secondCategory = secondCatalog.AddCategory("FOOD", "Φαγητά");
        var product = firstCatalog.AddItem(firstCategory.Id, "SKU01", "Μαργαρίτα",
            CatalogItemKind.Product, tracksStock: true);
        var service = firstCatalog.AddItem(firstCategory.Id, "SERV01", "Catering",
            CatalogItemKind.Service);
        product.UpdateDetails("4006381333931", "Περιγραφή με δύο γραμμές.\nΔεύτερη γραμμή");
        var independentProduct = secondCatalog.AddItem(secondCategory.Id, "SKU01",
            "Δεύτερη επιχείρηση", CatalogItemKind.Product);
        independentProduct.UpdateDetails("4006381333931", "Άλλο tenant");

        context.CatalogCategories.AddRange(firstCategory, secondCategory);
        context.CatalogItems.AddRange(product, service, independentProduct);
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        var firstProducts = await context.CatalogItems
            .Where(item => item.TenantId == firstTenantId)
            .OrderBy(item => item.Code)
            .ToListAsync(cancellationToken);
        Assert.HasCount(2, firstProducts);
        Assert.AreEqual(firstTenantId, firstProducts[0].TenantId);
        Assert.AreEqual(firstCategory.Id, firstProducts[0].CategoryId);
        Assert.AreEqual(CatalogItemKind.Service, firstProducts[0].Kind);
        Assert.IsFalse(firstProducts[0].TracksStock);
        Assert.AreEqual(CatalogUnitOfMeasure.Service, firstProducts[0].UnitOfMeasure);
        Assert.AreEqual(CatalogAccountingType.Service, firstProducts[0].AccountingType);
        Assert.AreEqual(CatalogItemKind.Product, firstProducts[1].Kind);
        Assert.IsTrue(firstProducts[1].TracksStock);
        Assert.AreEqual(CatalogUnitOfMeasure.Piece, firstProducts[1].UnitOfMeasure);
        Assert.AreEqual(CatalogAccountingType.Merchandise, firstProducts[1].AccountingType);
        Assert.AreEqual("4006381333931", firstProducts[1].Barcode);
        Assert.Contains("Δεύτερη γραμμή", firstProducts[1].LongDescription!);
        Assert.AreEqual(1, await context.CatalogItems.CountAsync(
            item => item.TenantId == secondTenantId, cancellationToken));
        Assert.AreEqual(2, await context.CatalogCategories.CountAsync(cancellationToken));

        // Ελέγχουμε πραγματικά το tenant predicate, τη SQL μετάφραση του StartsWith
        // και το όριο των παραγόμενων σελίδων στο απομονωμένο CI SQL Server.
        var reader = new SqlCatalogReadStore(context);
        CatalogReadPage<CatalogItemSummary> firstPage = await reader.ReadItemsAsync(
            firstTenantId, new CatalogReadQuery(1, 1), cancellationToken);
        Assert.HasCount(1, firstPage.Rows);
        Assert.AreEqual("SERV01", firstPage.Rows[0].Code);
        Assert.IsTrue(firstPage.HasMore);
        CatalogReadPage<CatalogItemSummary> nextPage = await reader.ReadItemsAsync(
            firstTenantId, new CatalogReadQuery(2, 1), cancellationToken);
        Assert.HasCount(1, nextPage.Rows);
        Assert.AreEqual("SKU01", nextPage.Rows[0].Code);
        Assert.IsFalse(nextPage.HasMore);

        // Ο τύπος εφαρμόζεται στη SQL πριν από την σελιδοποίηση και την εμφάνιση.
        CatalogReadPage<CatalogItemSummary> services = await reader.ReadItemsAsync(
            firstTenantId, new CatalogReadQuery(1, 1, Kind: CatalogItemKind.Service),
            cancellationToken);
        Assert.HasCount(1, services.Rows);
        Assert.AreEqual("SERV01", services.Rows[0].Code);
        Assert.AreEqual("Φαγητά", services.Rows[0].CategoryName);
        Assert.IsFalse(services.HasMore);

        CatalogReadPage<CatalogItemSummary> products = await reader.ReadItemsAsync(
            firstTenantId, new CatalogReadQuery(1, 1, Kind: CatalogItemKind.Product),
            cancellationToken);
        Assert.HasCount(1, products.Rows);
        Assert.AreEqual("SKU01", products.Rows[0].Code);
        Assert.IsFalse(products.HasMore);
        CatalogReadPage<CatalogItemSummary> barcodeSearch = await reader.ReadItemsAsync(
            firstTenantId, new CatalogReadQuery(1, 25, Search: "400638",
                Kind: CatalogItemKind.Product, SortBy: "barcode"), cancellationToken);
        Assert.AreEqual("SKU01", Assert.ContainsSingle(barcodeSearch.Rows).Code);
        Assert.AreEqual("4006381333931", barcodeSearch.Rows[0].Barcode);

        CatalogReadPage<CatalogItemSummary> otherTenant = await reader.ReadItemsAsync(
            secondTenantId, new CatalogReadQuery(1, 20, "SKU"), cancellationToken);
        Assert.HasCount(1, otherTenant.Rows);
        Assert.AreEqual("Δεύτερη επιχείρηση", otherTenant.Rows[0].Name);
        Assert.AreEqual(secondCategory.Id, otherTenant.Rows[0].CategoryId);

        CatalogReadPage<CatalogItemSummary> noLeak = await reader.ReadItemsAsync(
            secondTenantId, new CatalogReadQuery(1, 20, "SERV"), cancellationToken);
        Assert.HasCount(0, noLeak.Rows);
        CatalogReadPage<CatalogCategorySummary> categories = await reader.ReadCategoriesAsync(
            firstTenantId, new CatalogReadQuery(1, 1, "FO"), cancellationToken);
        Assert.HasCount(1, categories.Rows);
        Assert.AreEqual(firstCategory.Id, categories.Rows[0].Id);
        CatalogReadPage<CatalogItemSummary> noTenant = await reader.ReadItemsAsync(
            Guid.Empty, new CatalogReadQuery(), cancellationToken);
        Assert.HasCount(0, noTenant.Rows);

        // Ο μοναδικός κωδικός επιβάλλεται στη βάση, ακόμη και με διαφορετικά πεζά/κεφαλαία.
        SqlException duplicateCategory = await Assert.ThrowsExactlyAsync<SqlException>(
            () => InsertCategoryAsync(connection, firstTenantId, "food", cancellationToken));
        Assert.IsTrue(duplicateCategory.Number is 2601 or 2627);

        SqlException duplicateItem = await Assert.ThrowsExactlyAsync<SqlException>(
            () => InsertItemAsync(connection, firstTenantId, firstCategory.Id, "sku01", 1, false,
                cancellationToken));
        Assert.IsTrue(duplicateItem.Number is 2601 or 2627);

        SqlException duplicateBarcode = await Assert.ThrowsExactlyAsync<SqlException>(
            () => InsertItemAsync(connection, firstTenantId, firstCategory.Id,
                "SKU-BAR-OTHER", 1, false, cancellationToken, "4006381333931"));
        Assert.IsTrue(duplicateBarcode.Number is 2601 or 2627);
        SqlException invalidBarcode = await Assert.ThrowsExactlyAsync<SqlException>(
            () => InsertItemAsync(connection, firstTenantId, firstCategory.Id,
                "SKU-BAR-INVALID", 1, false, cancellationToken, "BAD BAR"));
        Assert.AreEqual(547, invalidBarcode.Number);

        // Μια κατηγορία άλλης επιχείρησης απορρίπτεται από το σύνθετο foreign key.
        SqlException foreignCategory = await Assert.ThrowsExactlyAsync<SqlException>(
            () => InsertItemAsync(connection, firstTenantId, secondCategory.Id, "FOREIGN", 1, false,
                cancellationToken));
        Assert.AreEqual(547, foreignCategory.Number);

        SqlException invalidService = await Assert.ThrowsExactlyAsync<SqlException>(
            () => InsertItemAsync(connection, firstTenantId, firstCategory.Id, "BROKEN", 2, true,
                cancellationToken));
        Assert.AreEqual(547, invalidService.Number);

        SqlException unknownTenant = await Assert.ThrowsExactlyAsync<SqlException>(
            () => InsertCategoryAsync(connection, Guid.NewGuid(), "ORPHAN", cancellationToken));
        Assert.AreEqual(547, unknownTenant.Number);

        await using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.CommandText = "DELETE FROM dbo.CatalogCategories WHERE Id = @categoryId;";
            deleteCommand.Parameters.Add("@categoryId", SqlDbType.UniqueIdentifier).Value = firstCategory.Id;
            SqlException restrict = await Assert.ThrowsExactlyAsync<SqlException>(async () =>
                await deleteCommand.ExecuteNonQueryAsync(cancellationToken));
            Assert.AreEqual(547, restrict.Number);
        }

        Assert.AreEqual(3, await context.CatalogItems.CountAsync(cancellationToken));
        await AssertCatalogWriteStoreAsync(context, firstTenantId, secondTenantId,
            firstCategory.Id, cancellationToken);
    }

    private static async Task AssertCatalogWriteStoreAsync(
        AenaonDbContext context, Guid tenantId, Guid foreignTenantId,
        Guid categoryId, CancellationToken token)
    {
        // Πραγματική SQL εγγραφή σε απομονωμένη βάση μόνο του CI.
        context.ChangeTracker.Clear();
        var writer = new SqlCatalogWriteStore(context);
        CatalogChangeResult category = await writer.ApplyAsync(tenantId,
            new(CatalogManagedEntity.Category, CatalogChangeAction.Create,
                Code: "DRINKS", Name: "Ποτά"), token);
        Assert.AreEqual(CatalogChangeStatus.Saved, category.Status);
        Assert.IsNotNull(category.Id);

        CatalogChangeResult item = await writer.ApplyAsync(tenantId,
            new(CatalogManagedEntity.Product, CatalogChangeAction.Create,
                Code: "COLA001", Name: "Αναψυκτικό", CategoryId: category.Id,
                TracksStock: true, UnitOfMeasure: CatalogUnitOfMeasure.Liter,
                AccountingType: CatalogAccountingType.Merchandise,
                Barcode: "INT-COLA", LongDescription: "Περιγραφή αναψυκτικού"), token);
        Assert.AreEqual(CatalogChangeStatus.Saved, item.Status);
        Assert.IsNotNull(item.Id);

        CatalogManageDetails details = await writer.GetDetailsAsync(
            tenantId, CatalogManagedEntity.Product, item.Id, token);
        Assert.AreEqual("COLA001", details.Record?.Code);
        Assert.AreEqual(CatalogUnitOfMeasure.Liter, details.Record?.UnitOfMeasure);
        Assert.AreEqual(CatalogAccountingType.Merchandise, details.Record?.AccountingType);
        Assert.AreEqual("INT-COLA", details.Record?.Barcode);
        Assert.AreEqual("Περιγραφή αναψυκτικού", details.Record?.LongDescription);
        Assert.IsTrue(details.Categories.Any(x => x.Id == category.Id));

        CatalogChangeResult edited = await writer.ApplyAsync(tenantId,
            new(CatalogManagedEntity.Product, CatalogChangeAction.Update,
                item.Id, "COLA002", "Αναψυκτικό 330ml", categoryId, false,
                UnitOfMeasure: CatalogUnitOfMeasure.Milliliter,
                AccountingType: CatalogAccountingType.FinishedProduct), token);
        Assert.AreEqual(CatalogChangeStatus.Saved, edited.Status);
        CatalogItem updatedCommercial = await context.CatalogItems.AsNoTracking()
            .SingleAsync(x => x.TenantId == tenantId && x.Id == item.Id, token);
        Assert.AreEqual(CatalogUnitOfMeasure.Milliliter, updatedCommercial.UnitOfMeasure);
        Assert.AreEqual(CatalogAccountingType.FinishedProduct, updatedCommercial.AccountingType);
        // Παλαιά CLI/SSR φόρμα χωρίς νέα flags δεν διαγράφει προϋπάρχουσες λεπτομέρειες.
        Assert.AreEqual("INT-COLA", updatedCommercial.Barcode);
        Assert.AreEqual("Περιγραφή αναψυκτικού", updatedCommercial.LongDescription);

        // Η SQL πρέπει να απαγορεύει ασύμβατο προϊόν/υπηρεσία ακόμη και έξω από την εφαρμογή.
        await using (var checkConnection = new SqlConnection(context.Database.GetConnectionString()))
        {
            await checkConnection.OpenAsync(token);
            await using var invalidSql = checkConnection.CreateCommand();
            invalidSql.CommandText = """
                UPDATE dbo.CatalogItems SET AccountingType = 5
                WHERE TenantId = @tenant AND Id = @id;
                """;
            invalidSql.Parameters.Add("@tenant", SqlDbType.UniqueIdentifier).Value = tenantId;
            invalidSql.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = item.Id.Value;
            SqlException rejected = await Assert.ThrowsExactlyAsync<SqlException>(
                async () => await invalidSql.ExecuteNonQueryAsync(token));
            Assert.AreEqual(547, rejected.Number);
        }

        CatalogReadPage<CatalogItemSummary> commercialRead =
            await new SqlCatalogReadStore(context).ReadItemsAsync(tenantId,
                new CatalogReadQuery(1, 25, Kind: CatalogItemKind.Product,
                    Search: "COLA002", SortBy: "unit"), token);
        CatalogItemSummary loadedCommercial = Assert.ContainsSingle(commercialRead.Rows);
        Assert.AreEqual(CatalogUnitOfMeasure.Milliliter, loadedCommercial.UnitOfMeasure);
        Assert.AreEqual(CatalogAccountingType.FinishedProduct, loadedCommercial.AccountingType);
        Assert.AreEqual("INT-COLA", loadedCommercial.Barcode);

        CatalogChangeResult duplicateThroughApp = await writer.ApplyAsync(tenantId,
            new(CatalogManagedEntity.Product, CatalogChangeAction.Create,
                Code: "COLA-OTHER", Name: "Δεύτερο αναψυκτικό", CategoryId: category.Id,
                Barcode: "INT-COLA"), token);
        Assert.AreEqual(CatalogChangeStatus.Duplicate, duplicateThroughApp.Status);
        Assert.AreEqual("catalog.item.barcode.duplicate", duplicateThroughApp.Error);

        CatalogChangeResult clearDetails = await writer.ApplyAsync(tenantId,
            new(CatalogManagedEntity.Product, CatalogChangeAction.Update,
                item.Id, "COLA002", "Αναψυκτικό 330ml", categoryId, false,
                Barcode: null, LongDescription: "", SetBarcode: true,
                SetLongDescription: true), token);
        Assert.AreEqual(CatalogChangeStatus.Saved, clearDetails.Status);
        CatalogItem afterClear = await context.CatalogItems.AsNoTracking()
            .SingleAsync(x => x.TenantId == tenantId && x.Id == item.Id, token);
        Assert.IsNull(afterClear.Barcode);
        Assert.IsNull(afterClear.LongDescription);


        CatalogChangeResult disabled = await writer.ApplyAsync(tenantId,
            new(CatalogManagedEntity.Product, CatalogChangeAction.Deactivate, item.Id), token);
        Assert.AreEqual(CatalogChangeStatus.Saved, disabled.Status);
        Assert.IsFalse(await context.CatalogItems.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Id == item.Id)
            .Select(x => x.IsActive).SingleAsync(token));

        CatalogChangeResult enabled = await writer.ApplyAsync(tenantId,
            new(CatalogManagedEntity.Product, CatalogChangeAction.Activate, item.Id), token);
        Assert.AreEqual(CatalogChangeStatus.Saved, enabled.Status);

        CatalogChangeResult foreignEdit = await writer.ApplyAsync(foreignTenantId,
            new(CatalogManagedEntity.Product, CatalogChangeAction.Deactivate, item.Id), token);
        Assert.AreEqual(CatalogChangeStatus.Missing, foreignEdit.Status);
        CatalogManageDetails foreignDetails = await writer.GetDetailsAsync(
            foreignTenantId, CatalogManagedEntity.Product, item.Id, token);
        Assert.IsNull(foreignDetails.Record);

        DomainRuleException serviceStock = await Assert.ThrowsExactlyAsync<DomainRuleException>(
            () => writer.ApplyAsync(tenantId,
                new(CatalogManagedEntity.Service, CatalogChangeAction.Create,
                    Code: "STOCKSERVICE", Name: "Λάθος υπηρεσία",
                    CategoryId: category.Id, TracksStock: true), token));
        Assert.AreEqual("catalog.item.service.stock_forbidden", serviceStock.Code);

        // Ο μοναδικός SQL index επιστρέφει φιλικό Duplicate status χωρίς δημιουργία δεύτερης εγγραφής.
        CatalogChangeResult duplicate = await writer.ApplyAsync(tenantId,
            new(CatalogManagedEntity.Category, CatalogChangeAction.Create,
                Code: "FOOD", Name: "Δεύτερη κατηγορία"), token);
        Assert.AreEqual(CatalogChangeStatus.Duplicate, duplicate.Status);

        // Το επόμενο SaveChanges δεν επιτρέπεται να επαναλάβει το αποτυχημένο INSERT.
        CatalogChangeResult afterDuplicate = await writer.ApplyAsync(tenantId,
            new(CatalogManagedEntity.Category, CatalogChangeAction.Create,
                Code: "AFTERDUP", Name: "Μετά από σύγκρουση"), token);
        Assert.AreEqual(CatalogChangeStatus.Saved, afterDuplicate.Status);

        // Οι κωδικοί 001 και 002 μετρούν ως 1 και 2 χωρίς αλλαγή της υπάρχουσας μορφής τους.
        CatalogChangeResult oldOne = await writer.ApplyAsync(tenantId,
            new(CatalogManagedEntity.Category, CatalogChangeAction.Create,
                Code: "001", Name: "Πρώτη αριθμητική"), token);
        CatalogChangeResult oldTwo = await writer.ApplyAsync(tenantId,
            new(CatalogManagedEntity.Category, CatalogChangeAction.Create,
                Code: "002", Name: "Δεύτερη αριθμητική"), token);
        Assert.AreEqual(CatalogChangeStatus.Saved, oldOne.Status);
        Assert.AreEqual(CatalogChangeStatus.Saved, oldTwo.Status);
        Guid request = Guid.NewGuid();
        CatalogChangeCommand autoCommand = new(CatalogManagedEntity.Category,
            CatalogChangeAction.Create, Name: "Τρίτη αριθμητική",
            AutoCode: true, RequestId: request);
        CatalogChangeResult auto = await writer.ApplyAsync(tenantId, autoCommand, token);
        Assert.AreEqual(CatalogChangeStatus.Saved, auto.Status);
        Assert.AreEqual("3", auto.Code);
        CatalogChangeResult replay = await writer.ApplyAsync(tenantId, autoCommand, token);
        Assert.AreEqual(auto.Id, replay.Id);
        Assert.AreEqual(auto.Code, replay.Code);
        Assert.AreEqual(1, await context.CatalogCategories.AsNoTracking()
            .CountAsync(x => x.TenantId == tenantId && x.Code == "3", token));

        // Η πραγματική SQL ταξινομεί αριθμούς φυσικά: 001, 002, 3, 10.
        CatalogChangeResult tenCategory = await writer.ApplyAsync(tenantId,
            new(CatalogManagedEntity.Category, CatalogChangeAction.Create,
                Code: "10", Name: "Δέκα"), token);
        Assert.AreEqual(CatalogChangeStatus.Saved, tenCategory.Status);
        var sortingReader = new SqlCatalogReadStore(context);
        CatalogReadPage<CatalogCategorySummary> sortedCodes =
            await sortingReader.ReadCategoriesAsync(tenantId,
                new CatalogReadQuery(1, 4, SortBy: "code"), token);
        CollectionAssert.AreEqual(new[] { "001", "002", "3", "10" },
            sortedCodes.Rows.Select(x => x.Code).ToArray());

        CatalogReadPage<CatalogCategorySummary> descendingCodes =
            await sortingReader.ReadCategoriesAsync(tenantId,
                new CatalogReadQuery(1, 4, SortBy: "code", Descending: true), token);
        CollectionAssert.AreEqual(new[] { "10", "3", "002", "001" },
            descendingCodes.Rows.Select(x => x.Code).ToArray());

        CatalogReadPage<CatalogCategorySummary> nameSort =
            await sortingReader.ReadCategoriesAsync(tenantId,
                new CatalogReadQuery(1, 25, SortBy: "name"), token);
        Assert.IsTrue(nameSort.Rows.Count > 0);
        Assert.AreEqual(nameSort.Rows.OrderBy(x => x.Name, StringComparer.Create(
            System.Globalization.CultureInfo.GetCultureInfo("el-GR"), false))
            .First().Name, nameSort.Rows[0].Name);

        // Η ιεραρχία δεν δέχεται κύκλους ούτε γονείς ξένης επιχείρησης.
        CatalogChangeResult parent = await writer.ApplyAsync(tenantId,
            new(CatalogManagedEntity.Category, CatalogChangeAction.Create,
                Code: "ROOT", Name: "Ρίζα"), token);
        CatalogChangeResult child = await writer.ApplyAsync(tenantId,
            new(CatalogManagedEntity.Category, CatalogChangeAction.Create,
                Code: "CHILD", Name: "Κλάδος", ParentCategoryId: parent.Id), token);
        Assert.AreEqual(CatalogChangeStatus.Saved, child.Status);
        CatalogChangeResult grandchild = await writer.ApplyAsync(tenantId,
            new(CatalogManagedEntity.Category, CatalogChangeAction.Create,
                Code: "GRAND", Name: "Υποκλάδος", ParentCategoryId: child.Id), token);
        Assert.AreEqual(CatalogChangeStatus.Saved, grandchild.Status);

        CatalogChangeResult cycle = await writer.ApplyAsync(tenantId,
            new(CatalogManagedEntity.Category, CatalogChangeAction.Update,
                parent.Id, "ROOT", "Ρίζα", ParentCategoryId: grandchild.Id), token);
        Assert.AreEqual(CatalogChangeStatus.Invalid, cycle.Status);
        CatalogChangeResult foreignParent = await writer.ApplyAsync(foreignTenantId,
            new(CatalogManagedEntity.Category, CatalogChangeAction.Create,
                Code: "FOREIGNCHILD", Name: "Απαγορευμένο", ParentCategoryId: parent.Id), token);
        Assert.AreEqual(CatalogChangeStatus.Invalid, foreignParent.Status);
        CatalogChangeResult blocked = await writer.ApplyAsync(tenantId,
            new(CatalogManagedEntity.Category, CatalogChangeAction.Deactivate, parent.Id), token);
        Assert.AreEqual(CatalogChangeStatus.Invalid, blocked.Status);

        CatalogReadPage<CatalogCategorySummary> children = await new SqlCatalogReadStore(context)
            .ReadCategoriesAsync(tenantId,
                new CatalogReadQuery(1, 25, ParentCategoryId: parent.Id), token);
        Assert.HasCount(1, children.Rows);
        Assert.AreEqual("CHILD", children.Rows[0].Code);
        Assert.AreEqual("Ρίζα / Κλάδος", children.Rows[0].CategoryPath);

        // Το πλήρες μονοπάτι εμφανίζεται επίσης στην προβολή ειδών/υπηρεσιών.
        CatalogChangeResult deepItem = await writer.ApplyAsync(tenantId,
            new(CatalogManagedEntity.Product, CatalogChangeAction.Create,
                Code: "TREEITEM", Name: "Χυμός", CategoryId: grandchild.Id), token);
        Assert.AreEqual(CatalogChangeStatus.Saved, deepItem.Status);
        CatalogReadPage<CatalogItemSummary> deepItems = await new SqlCatalogReadStore(context)
            .ReadItemsAsync(tenantId,
                new CatalogReadQuery(1, 25, Search: "TREEITEM",
                    Kind: CatalogItemKind.Product), token);
        Assert.HasCount(1, deepItems.Rows);
        Assert.AreEqual("Ρίζα / Κλάδος / Υποκλάδος", deepItems.Rows[0].CategoryPath);

        // Οι υπηρεσίες και τα προϊόντα μοιράζονται τον ίδιο αύξοντα χώρο κωδικών.
        CatalogChangeResult productAuto = await writer.ApplyAsync(tenantId,
            new(CatalogManagedEntity.Product, CatalogChangeAction.Create,
                Name: "Προϊόν 1", CategoryId: child.Id, AutoCode: true), token);
        CatalogChangeResult serviceAuto = await writer.ApplyAsync(tenantId,
            new(CatalogManagedEntity.Service, CatalogChangeAction.Create,
                Name: "Υπηρεσία 2", CategoryId: child.Id, AutoCode: true), token);
        Assert.AreEqual("1", productAuto.Code);
        Assert.AreEqual("2", serviceAuto.Code);

        // Το sort εκτελείται στον SQL Server πριν από την αλλαγή σελίδας.
        CatalogChangeResult tenItem = await writer.ApplyAsync(tenantId,
            new(CatalogManagedEntity.Product, CatalogChangeAction.Create,
                Code: "10", Name: "Δέκα προϊόν", CategoryId: categoryId), token);
        Assert.AreEqual(CatalogChangeStatus.Saved, tenItem.Status);
        CatalogReadPage<CatalogItemSummary> firstNumeric =
            await new SqlCatalogReadStore(context).ReadItemsAsync(tenantId,
                new CatalogReadQuery(1, 1, Kind: CatalogItemKind.Product,
                    SortBy: "code"), token);
        CatalogReadPage<CatalogItemSummary> secondNumeric =
            await new SqlCatalogReadStore(context).ReadItemsAsync(tenantId,
                new CatalogReadQuery(2, 1, Kind: CatalogItemKind.Product,
                    SortBy: "code"), token);
        Assert.AreEqual("1", firstNumeric.Rows[0].Code);
        Assert.AreEqual("10", secondNumeric.Rows[0].Code);

        CatalogReadPage<CatalogItemSummary> byCategory = await new SqlCatalogReadStore(context)
            .ReadItemsAsync(tenantId, new CatalogReadQuery(1, 25,
                Kind: CatalogItemKind.Product, SortBy: "category"), token);
        Assert.IsTrue(byCategory.Rows.Count > 0);
        Assert.IsTrue(byCategory.Rows.All(x => x.CategoryName is not null));

        // Ενεργοποίηση είδους δεν επιτρέπεται αν η κατηγορία του έχει απενεργοποιηθεί.
        CatalogChangeResult temporaryCategory = await writer.ApplyAsync(tenantId,
            new(CatalogManagedEntity.Category, CatalogChangeAction.Create,
                Code: "TEMP-GROUP", Name: "Προσωρινή κατηγορία"), token);
        CatalogChangeResult temporaryItem = await writer.ApplyAsync(tenantId,
            new(CatalogManagedEntity.Product, CatalogChangeAction.Create,
                Code: "TEMP-ITEM", Name: "Προσωρινό είδος",
                CategoryId: temporaryCategory.Id), token);
        Assert.AreEqual(CatalogChangeStatus.Saved, temporaryItem.Status);
        CatalogChangeResult disabledItem = await writer.ApplyAsync(tenantId,
            new(CatalogManagedEntity.Product, CatalogChangeAction.Deactivate,
                temporaryItem.Id), token);
        CatalogChangeResult disabledCategory = await writer.ApplyAsync(tenantId,
            new(CatalogManagedEntity.Category, CatalogChangeAction.Deactivate,
                temporaryCategory.Id), token);
        Assert.AreEqual(CatalogChangeStatus.Saved, disabledItem.Status);
        Assert.AreEqual(CatalogChangeStatus.Saved, disabledCategory.Status);
        CatalogChangeResult cannotReactivate = await writer.ApplyAsync(tenantId,
            new(CatalogManagedEntity.Product, CatalogChangeAction.Activate,
                temporaryItem.Id), token);
        Assert.AreEqual(CatalogChangeStatus.Invalid, cannotReactivate.Status);
        Assert.AreEqual("catalog.category.inactive_or_missing", cannotReactivate.Error);

        // Οι δύο παράλληλες SQL sessions δεν πρέπει να αποδώσουν ίδιο αριθμό.
        string connectionString = context.Database.GetConnectionString()!;
        await using var contextA = new AenaonDbContext(AenaonDbContextOptions.Create(connectionString, true));
        await using var contextB = new AenaonDbContext(AenaonDbContextOptions.Create(connectionString, true));
        Task<CatalogChangeResult> first = new SqlCatalogWriteStore(contextA).ApplyAsync(tenantId,
            new(CatalogManagedEntity.Category, CatalogChangeAction.Create,
                Name: "Ταυτόχρονη Α", AutoCode: true), token);
        Task<CatalogChangeResult> second = new SqlCatalogWriteStore(contextB).ApplyAsync(tenantId,
            new(CatalogManagedEntity.Category, CatalogChangeAction.Create,
                Name: "Ταυτόχρονη Β", AutoCode: true), token);
        CatalogChangeResult[] parallel = await Task.WhenAll(first, second);
        Assert.IsTrue(parallel.All(x => x.Status == CatalogChangeStatus.Saved));
        Assert.AreNotEqual(parallel[0].Code, parallel[1].Code);
    }

    private static async Task InsertCategoryAsync(SqlConnection connection, Guid tenantId,
        string code, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO dbo.CatalogCategories (Id, TenantId, Code, Name, IsActive)
            VALUES (@id, @tenantId, @code, @name, 1);
            """;
        command.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
        command.Parameters.Add("@tenantId", SqlDbType.UniqueIdentifier).Value = tenantId;
        command.Parameters.Add("@code", SqlDbType.NVarChar, 30).Value = code;
        command.Parameters.Add("@name", SqlDbType.NVarChar, 160).Value = "Δοκιμαστική κατηγορία";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertItemAsync(SqlConnection connection, Guid tenantId, Guid categoryId,
        string code, int kind, bool tracksStock, CancellationToken cancellationToken,
        string? barcode = null)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO dbo.CatalogItems
                (Id, TenantId, CategoryId, Code, Name, Kind, TracksStock, IsActive, Barcode)
            VALUES (@id, @tenantId, @categoryId, @code, @name, @kind, @tracksStock, 1, @barcode);
            """;
        command.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
        command.Parameters.Add("@tenantId", SqlDbType.UniqueIdentifier).Value = tenantId;
        command.Parameters.Add("@categoryId", SqlDbType.UniqueIdentifier).Value = categoryId;
        command.Parameters.Add("@code", SqlDbType.NVarChar, 40).Value = code;
        command.Parameters.Add("@name", SqlDbType.NVarChar, 200).Value = "Δοκιμαστικό είδος";
        command.Parameters.Add("@kind", SqlDbType.Int).Value = kind;
        command.Parameters.Add("@tracksStock", SqlDbType.Bit).Value = tracksStock;
        command.Parameters.Add("@barcode", SqlDbType.NVarChar, 40).Value =
            (object?)barcode ?? DBNull.Value;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
