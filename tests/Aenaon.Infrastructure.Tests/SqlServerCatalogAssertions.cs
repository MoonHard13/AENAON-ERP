using System.Data;
using Aenaon.Application.Catalog;
using Aenaon.Domain.Catalog;
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
        var independentProduct = secondCatalog.AddItem(secondCategory.Id, "SKU01",
            "Δεύτερη επιχείρηση", CatalogItemKind.Product);

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
        Assert.AreEqual(CatalogItemKind.Product, firstProducts[1].Kind);
        Assert.IsTrue(firstProducts[1].TracksStock);
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
                TracksStock: true), token);
        Assert.AreEqual(CatalogChangeStatus.Saved, item.Status);
        Assert.IsNotNull(item.Id);

        CatalogManageDetails details = await writer.GetDetailsAsync(
            tenantId, CatalogManagedEntity.Product, item.Id, token);
        Assert.AreEqual("COLA001", details.Record?.Code);
        Assert.IsTrue(details.Categories.Any(x => x.Id == category.Id));

        CatalogChangeResult edited = await writer.ApplyAsync(tenantId,
            new(CatalogManagedEntity.Product, CatalogChangeAction.Update,
                item.Id, "COLA002", "Αναψυκτικό 330ml", categoryId, false), token);
        Assert.AreEqual(CatalogChangeStatus.Saved, edited.Status);

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

        CatalogChangeResult serviceStock = await writer.ApplyAsync(tenantId,
            new(CatalogManagedEntity.Service, CatalogChangeAction.Create,
                Code: "STOCKSERVICE", Name: "Λάθος υπηρεσία",
                CategoryId: category.Id, TracksStock: true), token);
        Assert.AreEqual(CatalogChangeStatus.Invalid, serviceStock.Status);
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
        string code, int kind, bool tracksStock, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO dbo.CatalogItems
                (Id, TenantId, CategoryId, Code, Name, Kind, TracksStock, IsActive)
            VALUES (@id, @tenantId, @categoryId, @code, @name, @kind, @tracksStock, 1);
            """;
        command.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
        command.Parameters.Add("@tenantId", SqlDbType.UniqueIdentifier).Value = tenantId;
        command.Parameters.Add("@categoryId", SqlDbType.UniqueIdentifier).Value = categoryId;
        command.Parameters.Add("@code", SqlDbType.NVarChar, 40).Value = code;
        command.Parameters.Add("@name", SqlDbType.NVarChar, 200).Value = "Δοκιμαστικό είδος";
        command.Parameters.Add("@kind", SqlDbType.Int).Value = kind;
        command.Parameters.Add("@tracksStock", SqlDbType.Bit).Value = tracksStock;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
