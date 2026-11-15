using Aenaon.Application.Catalog;
using Aenaon.Domain.Catalog;
using Aenaon.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Infrastructure.Tests;

/// <summary>Αληθινή SQL δοκιμή δύο ταυτόχρονων αποθηκεύσεων της ίδιας τιμής.</summary>
internal static class SqlServerStalePriceConcurrencyAssertions
{
    public static async Task RunAsync(AenaonDbContext context, Guid tenantId,
        CancellationToken token)
    {
        Guid storeId = await context.Stores.AsNoTracking()
            .Where(x => x.TenantId == tenantId).Select(x => x.Id).FirstAsync(token);
        Guid[] items = await context.CatalogItems.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderBy(x => x.Id).Select(x => x.Id).Take(2).ToArrayAsync(token);
        Assert.HasCount(2, items);

        var store = new SqlPriceListManagementStore(context);
        PriceListResult created = await store.ApplyAsync(tenantId,
            new PriceListChange(PriceListAction.Create,
                Code: "QA-CONCURRENT-PRICE", Name: "Έλεγχος ταυτόχρονης τιμής",
                StoreId: storeId, Channel: PriceListChannel.DineIn,
                ValidFrom: new DateOnly(2035, 1, 1)), token);
        Assert.AreEqual(PriceListStatus.Saved, created.Status);
        Assert.IsNotNull(created.Id);
        Guid listId = created.Id.Value;

        PriceListResult initial = await store.SetPricesAsync(tenantId, listId,
            [new PriceListPriceChange(items[0], 5m, null, CheckExpected: true)], token);
        Assert.AreEqual(PriceListStatus.Saved, initial.Status);

        // Δύο ανεξάρτητα DbContexts διαβάζουν την ίδια αρχική τιμή 5 και προσπαθούν να γράψουν.
        var options = AenaonDbContextOptions.Create(context.Database.GetConnectionString()!, true);
        await using var firstContext = new AenaonDbContext(options);
        await using var secondContext = new AenaonDbContext(options);
        Task<PriceListResult> firstWrite = new SqlPriceListManagementStore(firstContext)
            .SetPricesAsync(tenantId, listId,
                [new PriceListPriceChange(items[0], 6m, 5m, CheckExpected: true)], token);
        Task<PriceListResult> secondWrite = new SqlPriceListManagementStore(secondContext)
            .SetPricesAsync(tenantId, listId,
                [new PriceListPriceChange(items[0], 7m, 5m, CheckExpected: true)], token);

        PriceListResult[] outcomes = await Task.WhenAll(firstWrite, secondWrite);
        Assert.AreEqual(1, outcomes.Count(x => x.Status == PriceListStatus.Saved));
        Assert.AreEqual(1, outcomes.Count(x => x.Status == PriceListStatus.Conflict &&
            x.Error == "pricelist.price.conflict"));

        decimal actual = await context.PriceListItems.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.PriceListId == listId &&
                x.CatalogItemId == items[0]).Select(x => x.UnitPrice).SingleAsync(token);
        Assert.IsTrue(actual is 6m or 7m);

        // Το απορριφθέν POST δεν αφήνει δεύτερη ενημέρωση ούτε πλασματικό audit.
        Assert.AreEqual(1, await context.PriceListAuditEntries.AsNoTracking()
            .CountAsync(x => x.TenantId == tenantId && x.PriceListId == listId &&
                x.Action == PriceListAuditAction.UpdatePrice, token));
        Assert.AreEqual(1, await context.PriceListAuditEntries.AsNoTracking()
            .CountAsync(x => x.TenantId == tenantId && x.PriceListId == listId &&
                x.Action == PriceListAuditAction.AddPrice, token));

        PriceListResult otherAdded = await store.SetPricesAsync(tenantId, listId,
            [new PriceListPriceChange(items[1], 8m, null, CheckExpected: true)], token);
        Assert.AreEqual(PriceListStatus.Saved, otherAdded.Status);
        long countBefore = await context.PriceListAuditEntries.AsNoTracking()
            .LongCountAsync(x => x.TenantId == tenantId && x.PriceListId == listId, token);

        // Αν μία γραμμή έχει stale expected, καμία άλλη τιμή του ίδιου batch δεν αλλάζει.
        PriceListResult rejectedBatch = await store.SetPricesAsync(tenantId, listId,
            [
                new PriceListPriceChange(items[0], 99m, 5m, CheckExpected: true),
                new PriceListPriceChange(items[1], 88m, 8m, CheckExpected: true)
            ], token);
        Assert.AreEqual(PriceListStatus.Conflict, rejectedBatch.Status);
        Assert.AreEqual("pricelist.price.conflict", rejectedBatch.Error);
        decimal secondPrice = await context.PriceListItems.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.PriceListId == listId &&
                x.CatalogItemId == items[1]).Select(x => x.UnitPrice).SingleAsync(token);
        Assert.AreEqual(8m, secondPrice);
        Assert.AreEqual(actual, await context.PriceListItems.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.PriceListId == listId &&
                x.CatalogItemId == items[0]).Select(x => x.UnitPrice).SingleAsync(token));
        Assert.AreEqual(countBefore, await context.PriceListAuditEntries.AsNoTracking()
            .LongCountAsync(x => x.TenantId == tenantId && x.PriceListId == listId, token));
    }
}
