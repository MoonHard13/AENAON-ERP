using System.Data;
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
