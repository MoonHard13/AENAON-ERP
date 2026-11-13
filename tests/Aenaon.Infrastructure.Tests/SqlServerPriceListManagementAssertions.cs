using Aenaon.Application.Catalog;
using Aenaon.Domain.Catalog;
using Aenaon.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Infrastructure.Tests;

/// <summary>Έλεγχος πραγματικών συναλλαγών τιμοκαταλόγου σε απομονωμένο SQL Server CI.</summary>
internal static class SqlServerPriceListManagementAssertions
{
    public static async Task RunAsync(AenaonDbContext context, Guid tenant,
        Guid foreignTenant, CancellationToken token)
    {
        var store = new SqlPriceListManagementStore(context);
        Guid storeId = await context.Stores.AsNoTracking()
            .Where(x => x.TenantId == tenant).Select(x => x.Id).FirstAsync(token);
        Guid itemId = await context.CatalogItems.AsNoTracking()
            .Where(x => x.TenantId == tenant && x.IsActive).Select(x => x.Id).FirstAsync(token);
        Guid foreignItemId = await context.CatalogItems.AsNoTracking()
            .Where(x => x.TenantId == foreignTenant).Select(x => x.Id).FirstAsync(token);

        PriceListResult created = await store.ApplyAsync(tenant,
            new PriceListChange(PriceListAction.Create, Code: "PROMO-CI", Name: "Δοκιμαστικός",
                StoreId: storeId, Channel: PriceListChannel.Takeaway,
                PricesIncludeVat: true, ValidFrom: new DateOnly(2026, 11, 13)), token);
        Assert.AreEqual(PriceListStatus.Saved, created.Status);
        Assert.IsNotNull(created.Id);

        PriceListSummary? list = await store.GetAsync(tenant, created.Id.Value, token);
        Assert.IsNotNull(list);
        Assert.AreEqual(storeId, list.StoreId);
        Assert.AreEqual(PriceListChannel.Takeaway, list.Channel);
        Assert.IsNull(await store.GetAsync(foreignTenant, created.Id.Value, token));

        PriceListResult duplicate = await store.ApplyAsync(tenant,
            new PriceListChange(PriceListAction.Create, Code: "promo-ci", Name: "Διπλός",
                ValidFrom: new DateOnly(2026, 11, 13)), token);
        Assert.AreEqual(PriceListStatus.Duplicate, duplicate.Status);

        // Οι αναφορές από διαφορετικό Tenant απορρίπτονται πριν την εγγραφή.
        PriceListResult foreign = await store.SetPricesAsync(tenant, created.Id.Value,
            [new PriceListPriceChange(foreignItemId, 50m)], token);
        Assert.AreEqual(PriceListStatus.Invalid, foreign.Status);

        PriceListResult priced = await store.SetPricesAsync(tenant, created.Id.Value,
            [new PriceListPriceChange(itemId, 9.8765m)], token);
        Assert.AreEqual(PriceListStatus.Saved, priced.Status);
        PriceListItemsPage? rows = await store.ReadItemsAsync(tenant, created.Id.Value, null, 1, 25, token);
        Assert.IsNotNull(rows);
        Assert.IsTrue(rows.Rows.Any(x => x.Id == itemId && x.UnitPrice == 9.8765m));

        PriceListResult modified = await store.SetPricesAsync(tenant, created.Id.Value,
            [new PriceListPriceChange(itemId, 12.5000m)], token);
        Assert.AreEqual(PriceListStatus.Saved, modified.Status);
        Assert.AreEqual(12.5000m, await context.PriceListItems.AsNoTracking()
            .Where(x => x.TenantId == tenant && x.PriceListId == created.Id.Value &&
                x.CatalogItemId == itemId)
            .Select(x => x.UnitPrice).SingleAsync(token));

        PriceListResult changed = await store.ApplyAsync(tenant,
            new PriceListChange(PriceListAction.Update, created.Id, Name: "Νέα περιγραφή",
                ValidFrom: new DateOnly(2026, 11, 14)), token);
        Assert.AreEqual(PriceListStatus.Saved, changed.Status);
        Assert.AreEqual("Νέα περιγραφή", (await store.GetAsync(tenant, created.Id.Value, token))?.Name);

        PriceListResult deactivated = await store.ApplyAsync(tenant,
            new PriceListChange(PriceListAction.Deactivate, created.Id), token);
        Assert.AreEqual(PriceListStatus.Saved, deactivated.Status);
        PriceListResult inactive = await store.SetPricesAsync(tenant, created.Id.Value,
            [new PriceListPriceChange(itemId, 20m)], token);
        Assert.AreEqual(PriceListStatus.Invalid, inactive.Status);
        Assert.AreEqual(12.5000m, await context.PriceListItems.AsNoTracking()
            .Where(x => x.TenantId == tenant && x.PriceListId == created.Id.Value)
            .Select(x => x.UnitPrice).SingleAsync(token));
    }
}
