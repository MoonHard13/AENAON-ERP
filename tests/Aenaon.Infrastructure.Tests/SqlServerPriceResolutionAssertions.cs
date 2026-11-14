using Aenaon.Domain.Catalog;
using Aenaon.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Infrastructure.Tests;

/// <summary>SQL Server 2022 πραγματικοί read-only έλεγχοι επίλυσης τιμής.</summary>
internal static class SqlServerPriceResolutionAssertions
{
    public static async Task RunAsync(AenaonDbContext context,
        Guid tenantId, Guid foreignTenantId, CancellationToken token)
    {
        var store = new SqlPriceResolutionStore(context);
        Guid storeId = await context.Stores.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .Select(x => x.Id).FirstAsync(token);
        Guid itemId = await context.CatalogItems.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .Select(x => x.Id).FirstAsync(token);

        Assert.IsTrue(await store.IsActiveCatalogItemAsync(tenantId, itemId, token));
        Assert.IsFalse(await store.IsActiveCatalogItemAsync(foreignTenantId, itemId, token));

        var day = new DateOnly(2026, 11, 15);
        IReadOnlyList<PriceResolutionCandidate> retail = await store.FindCandidatesAsync(
            tenantId, itemId, storeId, PriceListChannel.Retail, day, token);
        PriceSelectionResult chosen = PriceResolutionPolicy.Select(
            retail, storeId, PriceListChannel.Retail, day);

        Assert.AreEqual(PriceSelectionStatus.Unique, chosen.Status);
        Assert.AreEqual(12.3456m, chosen.Selected?.UnitPrice);
        Assert.AreEqual(PriceListChannel.Retail, chosen.Selected?.Channel);

        IReadOnlyList<PriceResolutionCandidate> delivery = await store.FindCandidatesAsync(
            tenantId, itemId, null, PriceListChannel.Delivery, day, token);
        PriceSelectionResult global = PriceResolutionPolicy.Select(
            delivery, null, PriceListChannel.Delivery, day);
        Assert.AreEqual(PriceSelectionStatus.Unique, global.Status);
        Assert.AreEqual(13.5000m, global.Selected?.UnitPrice);

        IReadOnlyList<PriceResolutionCandidate> expired = await store.FindCandidatesAsync(
            tenantId, itemId, null, PriceListChannel.Delivery,
            new DateOnly(2028, 1, 1), token);
        Assert.AreEqual(0, expired.Count);

        IReadOnlyList<PriceResolutionCandidate> forbidden = await store.FindCandidatesAsync(
            foreignTenantId, itemId, null, PriceListChannel.Delivery, day, token);
        Assert.AreEqual(0, forbidden.Count);

        // Δεύτερη ταυτόχρονη ισοδύναμη λίστα επιστρέφει αμφισημία, όχι τυχαία τιμή.
        var collision = PriceList.Create(tenantId, storeId, "RETAIL-COLLISION",
            "Δοκιμή αμφισημίας", PriceListChannel.Retail, true,
            new DateOnly(2026, 11, 14));
        context.PriceLists.Add(collision);
        context.PriceListItems.Add(PriceListItem.Create(
            tenantId, collision.Id, itemId, 99m));
        await context.SaveChangesAsync(token);
        context.ChangeTracker.Clear();

        retail = await store.FindCandidatesAsync(
            tenantId, itemId, storeId, PriceListChannel.Retail, day, token);
        chosen = PriceResolutionPolicy.Select(retail, storeId, PriceListChannel.Retail, day);
        Assert.AreEqual(PriceSelectionStatus.Ambiguous, chosen.Status);
        Assert.AreEqual(2, chosen.ConflictingPriceListIds?.Count);
        Assert.IsNull(chosen.Selected);
    }
}
