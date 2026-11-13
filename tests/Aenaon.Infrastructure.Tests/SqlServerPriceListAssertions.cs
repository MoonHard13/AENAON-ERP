using System.Data;
using Aenaon.Domain.Catalog;
using Aenaon.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Infrastructure.Tests;

/// <summary>Ολοκληρωμένοι έλεγχοι τιμοκαταλόγων σε απομονωμένο SQL Server 2022.</summary>
internal static class SqlServerPriceListAssertions
{
    public static async Task RunAsync(AenaonDbContext context,
        Guid tenantId, Guid otherTenantId, CancellationToken token)
    {
        Guid storeId = await context.Stores.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .Select(x => x.Id).FirstAsync(token);
        Guid otherStoreId = await context.Stores.AsNoTracking()
            .Where(x => x.TenantId == otherTenantId)
            .Select(x => x.Id).FirstAsync(token);
        Guid itemId = await context.CatalogItems.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .Select(x => x.Id).FirstAsync(token);
        Guid otherItemId = await context.CatalogItems.AsNoTracking()
            .Where(x => x.TenantId == otherTenantId)
            .Select(x => x.Id).FirstAsync(token);

        var retail = PriceList.Create(tenantId, storeId, "RETAIL",
            "Βασική λιανική", PriceListChannel.Retail, true,
            new DateOnly(2026, 11, 13));
        var delivery = PriceList.Create(tenantId, null, "DELIVERY",
            "Διανομή", PriceListChannel.Delivery, true,
            new DateOnly(2026, 11, 13), new DateOnly(2027, 12, 31));
        context.PriceLists.AddRange(retail, delivery);
        context.PriceListItems.Add(PriceListItem.Create(tenantId, retail.Id, itemId, 12.3456m));
        context.PriceListItems.Add(PriceListItem.Create(tenantId, delivery.Id, itemId, 13.5000m));
        await context.SaveChangesAsync(token);
        context.ChangeTracker.Clear();

        Assert.AreEqual(2, await context.PriceLists.AsNoTracking()
            .CountAsync(x => x.TenantId == tenantId, token));
        Assert.AreEqual(0, await context.PriceLists.AsNoTracking()
            .CountAsync(x => x.TenantId == otherTenantId, token));
        Assert.AreEqual(12.3456m, await context.PriceListItems.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.PriceListId == retail.Id)
            .Select(x => x.UnitPrice).SingleAsync(token));

        await using var connection = new SqlConnection(context.Database.GetConnectionString());
        await connection.OpenAsync(token);

        // Η SQL απαγορεύει επαναχρησιμοποίηση κωδικού με διαφορετικά πεζά/κεφαλαία.
        await using (var duplicate = connection.CreateCommand())
        {
            duplicate.CommandText = """
                INSERT INTO dbo.PriceLists
                (Id, TenantId, StoreId, Code, Name, Channel, Currency,
                 PricesIncludeVat, ValidFrom, ValidUntil, IsActive)
                VALUES
                (@id, @tenant, NULL, N'retail', N'Διπλό', 4, 'EUR',
                 1, '2026-11-13', NULL, 1);
                """;
            duplicate.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
            duplicate.Parameters.Add("@tenant", SqlDbType.UniqueIdentifier).Value = tenantId;
            SqlException rejected = await Assert.ThrowsExactlyAsync<SqlException>(
                async () => await duplicate.ExecuteNonQueryAsync(token));
            Assert.IsTrue(rejected.Number is 2601 or 2627);
        }

        // Το σύνθετο FK αποκλείει κατάστημα άλλης επιχείρησης.
        await using (var foreignStore = connection.CreateCommand())
        {
            foreignStore.CommandText = """
                INSERT INTO dbo.PriceLists
                (Id, TenantId, StoreId, Code, Name, Channel, Currency,
                 PricesIncludeVat, ValidFrom, ValidUntil, IsActive)
                VALUES
                (@id, @tenant, @store, 'BADSTORE', N'Ασυμβίβαστο', 0, 'EUR',
                 0, '2026-11-13', NULL, 1);
                """;
            foreignStore.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
            foreignStore.Parameters.Add("@tenant", SqlDbType.UniqueIdentifier).Value = tenantId;
            foreignStore.Parameters.Add("@store", SqlDbType.UniqueIdentifier).Value = otherStoreId;
            SqlException rejected = await Assert.ThrowsExactlyAsync<SqlException>(
                async () => await foreignStore.ExecuteNonQueryAsync(token));
            Assert.AreEqual(547, rejected.Number);
        }

        // Ο ίδιος Tenant απαιτείται για τιμοκατάλογο και είδος, χωρίς SQL injection.
        await using (var foreignItem = connection.CreateCommand())
        {
            foreignItem.CommandText = """
                INSERT INTO dbo.PriceListItems
                (Id, TenantId, PriceListId, CatalogItemId, UnitPrice)
                VALUES (@id, @tenant, @priceList, @item, 1.0000);
                """;
            foreignItem.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
            foreignItem.Parameters.Add("@tenant", SqlDbType.UniqueIdentifier).Value = tenantId;
            foreignItem.Parameters.Add("@priceList", SqlDbType.UniqueIdentifier).Value = retail.Id;
            foreignItem.Parameters.Add("@item", SqlDbType.UniqueIdentifier).Value = otherItemId;
            SqlException rejected = await Assert.ThrowsExactlyAsync<SqlException>(
                async () => await foreignItem.ExecuteNonQueryAsync(token));
            Assert.AreEqual(547, rejected.Number);
        }

        await using (var negative = connection.CreateCommand())
        {
            negative.CommandText = """
                INSERT INTO dbo.PriceListItems
                (Id, TenantId, PriceListId, CatalogItemId, UnitPrice)
                VALUES (@id, @tenant, @priceList, @item, -1.0000);
                """;
            negative.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
            negative.Parameters.Add("@tenant", SqlDbType.UniqueIdentifier).Value = tenantId;
            negative.Parameters.Add("@priceList", SqlDbType.UniqueIdentifier).Value = retail.Id;
            negative.Parameters.Add("@item", SqlDbType.UniqueIdentifier).Value = itemId;
            SqlException rejected = await Assert.ThrowsExactlyAsync<SqlException>(
                async () => await negative.ExecuteNonQueryAsync(token));
            Assert.AreEqual(547, rejected.Number);
        }
    }
}
