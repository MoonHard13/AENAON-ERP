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

        Guid actor = Guid.NewGuid();
        PriceListResult priced = await store.SetPricesAsync(tenant, created.Id.Value,
            [new PriceListPriceChange(itemId, 9.8765m, null, CheckExpected: true)],
            token, actor, "CLI");
        Assert.AreEqual(PriceListStatus.Saved, priced.Status);
        PriceListItemsPage? rows = await store.ReadItemsAsync(tenant, created.Id.Value, null, 1, 25, token);
        Assert.IsNotNull(rows);
        Assert.IsTrue(rows.Rows.Any(x => x.Id == itemId && x.UnitPrice == 9.8765m));

        // Ίδιο κατάστημα/κανάλι/ημέρα επιτρέπεται χωρίς κοινό είδος, όχι με κοινή τιμή.
        PriceListResult secondList = await store.ApplyAsync(tenant,
            new PriceListChange(PriceListAction.Create, Code: "PROMO-CI-2",
                Name: "Δεύτερη προωθητική", StoreId: storeId,
                Channel: PriceListChannel.Takeaway,
                ValidFrom: new DateOnly(2026, 11, 13)), token);
        Assert.AreEqual(PriceListStatus.Saved, secondList.Status);
        PriceListResult overlappingItem = await store.SetPricesAsync(tenant, secondList.Id!.Value,
            [new PriceListPriceChange(itemId, 8m)], token);
        Assert.AreEqual(PriceListStatus.Conflict, overlappingItem.Status);
        Assert.AreEqual("pricelist.overlap.conflict", overlappingItem.Error);
        Assert.AreEqual(0, await context.PriceListItems.AsNoTracking()
            .CountAsync(x => x.TenantId == tenant && x.PriceListId == secondList.Id.Value, token));
        Assert.AreEqual(1, await context.PriceListAuditEntries.AsNoTracking()
            .CountAsync(x => x.TenantId == tenant && x.PriceListId == secondList.Id.Value, token));

        PriceListResult modified = await store.SetPricesAsync(tenant, created.Id.Value,
            [new PriceListPriceChange(itemId, 12.5000m, 9.8765m, CheckExpected: true)], token);
        Assert.AreEqual(PriceListStatus.Saved, modified.Status);
        PriceListResult noOp = await store.SetPricesAsync(tenant, created.Id.Value,
            [new PriceListPriceChange(itemId, 12.5000m, 12.5000m, CheckExpected: true)], token);
        Assert.AreEqual(PriceListStatus.Saved, noOp.Status);
        Assert.AreEqual(12.5000m, await context.PriceListItems.AsNoTracking()
            .Where(x => x.TenantId == tenant && x.PriceListId == created.Id.Value &&
                x.CatalogItemId == itemId)
            .Select(x => x.UnitPrice).SingleAsync(token));

        // Η παρωχημένη αρχική τιμή δεν επιτρέπεται να αντικαταστήσει νεότερη αλλαγή.
        PriceListResult stale = await store.SetPricesAsync(tenant, created.Id.Value,
            [new PriceListPriceChange(itemId, 99m, 9.8765m, CheckExpected: true)], token);
        Assert.AreEqual(PriceListStatus.Conflict, stale.Status);
        Assert.AreEqual(12.5000m, await context.PriceListItems.AsNoTracking()
            .Where(x => x.TenantId == tenant && x.PriceListId == created.Id.Value &&
                x.CatalogItemId == itemId)
            .Select(x => x.UnitPrice).SingleAsync(token));

        PriceListResult changed = await store.ApplyAsync(tenant,
            new PriceListChange(PriceListAction.Update, created.Id, Name: "Νέα περιγραφή",
                ValidFrom: new DateOnly(2026, 11, 14)), token);
        Assert.AreEqual(PriceListStatus.Saved, changed.Status);
        Assert.AreEqual("Νέα περιγραφή", (await store.GetAsync(tenant, created.Id.Value, token))?.Name);

        // Απαρχαιωμένο όνομα/περίοδος δεν πρέπει να αντικαθιστούν νεότερα δεδομένα.
        PriceListResult staleDetails = await store.ApplyAsync(tenant,
            new PriceListChange(PriceListAction.Update, created.Id,
                Name: "Παλιά φόρμα", ValidFrom: new DateOnly(2026, 11, 20),
                ExpectedName: "Δοκιμαστικός", ExpectedValidFrom: new DateOnly(2026, 11, 13),
                CheckExpectedDetails: true), token);
        Assert.AreEqual(PriceListStatus.Conflict, staleDetails.Status);
        Assert.AreEqual("Νέα περιγραφή",
            (await store.GetAsync(tenant, created.Id.Value, token))?.Name);

        // Οι αλλαγές πολιτικής με υφιστάμενες τιμές απαιτούν ρητή επιβεβαίωση.
        PriceListChange policy = new(PriceListAction.Update, created.Id,
            Name: "Τιμοκατάλογος Dine In",
            StoreId: null, Channel: PriceListChannel.DineIn,
            PricesIncludeVat: false, ValidFrom: new DateOnly(2026, 11, 14),
            EditCommercialProfile: true,
            ExpectedStoreId: storeId,
            ExpectedChannel: PriceListChannel.Takeaway,
            ExpectedPricesIncludeVat: true);

        PriceListResult unconfirmed = await store.ApplyAsync(tenant, policy, token);
        Assert.AreEqual(PriceListStatus.Invalid, unconfirmed.Status);
        Assert.AreEqual("pricelist.policy.confirm_required", unconfirmed.Error);
        Assert.AreEqual(storeId, (await store.GetAsync(tenant, created.Id.Value, token))?.StoreId);

        PriceListResult updatedPolicy = await store.ApplyAsync(tenant,
            policy with { ConfirmPolicyChange = true }, token);
        Assert.AreEqual(PriceListStatus.Saved, updatedPolicy.Status);
        PriceListSummary? afterPolicy = await store.GetAsync(tenant, created.Id.Value, token);
        Assert.IsNotNull(afterPolicy);
        Assert.IsNull(afterPolicy.StoreId);
        Assert.AreEqual(PriceListChannel.DineIn, afterPolicy.Channel);
        Assert.IsFalse(afterPolicy.PricesIncludeVat);
        Assert.AreEqual(12.5000m, await context.PriceListItems.AsNoTracking()
            .Where(x => x.TenantId == tenant && x.PriceListId == created.Id.Value)
            .Select(x => x.UnitPrice).SingleAsync(token));

        // Η παλιά φόρμα δεν μπορεί να επαναφέρει εν αγνοία μας παλιά πεδία.
        PriceListResult staleProfile = await store.ApplyAsync(tenant,
            policy with { Name = "Απαρχαιωμένη φόρμα", ConfirmPolicyChange = true }, token);
        Assert.AreEqual(PriceListStatus.Conflict, staleProfile.Status);

        Guid foreignStoreId = await context.Stores.AsNoTracking()
            .Where(x => x.TenantId == foreignTenant)
            .Select(x => x.Id).FirstAsync(token);
        PriceListResult foreignStore = await store.ApplyAsync(tenant,
            policy with
            {
                Name = "Άλλης εταιρείας",
                StoreId = foreignStoreId,
                ExpectedStoreId = null,
                ExpectedChannel = PriceListChannel.DineIn,
                ExpectedPricesIncludeVat = false,
                ConfirmPolicyChange = true
            }, token);
        Assert.AreEqual(PriceListStatus.Invalid, foreignStore.Status);

        // Όταν ο πρώτος κατάλογος μετακινηθεί σε άλλο κανάλι, η δεύτερη τιμή είναι νόμιμη.
        PriceListResult allowedOther = await store.SetPricesAsync(tenant, secondList.Id.Value,
            [new PriceListPriceChange(itemId, 8m)], token);
        Assert.AreEqual(PriceListStatus.Saved, allowedOther.Status);

        // Η επιστροφή στο ίδιο κατάστημα/κανάλι και περίοδο θα δημιουργούσε ισοβαθμία.
        PriceListResult overlap = await store.ApplyAsync(tenant,
            policy with
            {
                Name = "Επικάλυψη RETAIL",
                StoreId = storeId,
                Channel = PriceListChannel.Takeaway,
                PricesIncludeVat = true,
                ExpectedStoreId = null,
                ExpectedChannel = PriceListChannel.DineIn,
                ExpectedPricesIncludeVat = false,
                ConfirmPolicyChange = true
            }, token);
        Assert.AreEqual(PriceListStatus.Conflict, overlap.Status);
        Assert.AreEqual("pricelist.overlap.conflict", overlap.Error);
        Assert.AreEqual(PriceListChannel.DineIn,
            (await store.GetAsync(tenant, created.Id.Value, token))?.Channel);

        PriceListResult restoredPolicy = await store.ApplyAsync(tenant,
            policy with
            {
                Name = "Επιστροφή",
                StoreId = storeId,
                Channel = PriceListChannel.Retail,
                PricesIncludeVat = true,
                ExpectedStoreId = null,
                ExpectedChannel = PriceListChannel.DineIn,
                ExpectedPricesIncludeVat = false,
                ConfirmPolicyChange = true
            }, token);
        Assert.AreEqual(PriceListStatus.Saved, restoredPolicy.Status);

        PriceListResult deactivated = await store.ApplyAsync(tenant,
            new PriceListChange(PriceListAction.Deactivate, created.Id), token);
        Assert.AreEqual(PriceListStatus.Saved, deactivated.Status);
        PriceListResult inactive = await store.SetPricesAsync(tenant, created.Id.Value,
            [new PriceListPriceChange(itemId, 20m)], token);
        Assert.AreEqual(PriceListStatus.Invalid, inactive.Status);
        Assert.AreEqual(12.5000m, await context.PriceListItems.AsNoTracking()
            .Where(x => x.TenantId == tenant && x.PriceListId == created.Id.Value)
            .Select(x => x.UnitPrice).SingleAsync(token));

        // Ανενεργός τιμοκατάλογος δεν επιτρέπει αφαίρεση τιμών.
        PriceListResult deniedRemoval = await store.RemovePriceAsync(
            tenant, created.Id.Value, itemId, 12.5000m, token);
        Assert.AreEqual(PriceListStatus.Invalid, deniedRemoval.Status);

        PriceListResult activated = await store.ApplyAsync(tenant,
            new PriceListChange(PriceListAction.Activate, created.Id), token);
        Assert.AreEqual(PriceListStatus.Saved, activated.Status);

        // Απαγορεύεται η διαγραφή νεότερης τιμής από παλιότερη φόρμα.
        PriceListResult staleRemoval = await store.RemovePriceAsync(
            tenant, created.Id.Value, itemId, 9.8765m, token);
        Assert.AreEqual(PriceListStatus.Conflict, staleRemoval.Status);

        PriceListResult foreignRemoval = await store.RemovePriceAsync(
            foreignTenant, created.Id.Value, itemId, 12.5000m, token);
        Assert.AreEqual(PriceListStatus.Missing, foreignRemoval.Status);

        PriceListResult removed = await store.RemovePriceAsync(
            tenant, created.Id.Value, itemId, 12.5000m, token);
        Assert.AreEqual(PriceListStatus.Saved, removed.Status);
        Assert.AreEqual(0, await context.PriceListItems.AsNoTracking()
            .CountAsync(x => x.TenantId == tenant && x.PriceListId == created.Id.Value &&
                x.CatalogItemId == itemId, token));

        PriceListHistoryPage? history = await store.ReadHistoryAsync(
            tenant, created.Id.Value, 1, 25, token);
        Assert.IsNotNull(history);
        Assert.AreEqual(9, history.Rows.Count);
        Assert.IsTrue(history.Rows.Any(x => x.Action == PriceListAuditAction.AddPrice &&
            x.Source == "CLI" && x.ActorUserId == actor &&
            x.CatalogItemId == itemId && x.AfterPrice == 9.8765m));
        Assert.IsTrue(history.Rows.Any(x => x.Action == PriceListAuditAction.RemovePrice &&
            x.CatalogItemId == itemId && x.BeforePrice == 12.5000m &&
            x.AfterPrice is null));
        Assert.IsTrue(history.Rows.Any(x => x.Action == PriceListAuditAction.UpdateList &&
            x.BeforePolicy is not null && x.AfterPolicy is not null));
        Assert.IsNull(await store.ReadHistoryAsync(foreignTenant, created.Id.Value, 1, 25, token));

        // Οι απορριφθείσες/αμετάβλητες εγγραφές δεν προσθέτουν ιστορικό.
        Assert.AreEqual(9, await context.PriceListAuditEntries.AsNoTracking()
            .CountAsync(x => x.TenantId == tenant && x.PriceListId == created.Id.Value, token));

        // Αποτρέπεται UPDATE του ιστορικού ακόμη και όταν γίνεται απευθείας σε SQL.
        Microsoft.Data.SqlClient.SqlException immutable =
            await Assert.ThrowsExactlyAsync<Microsoft.Data.SqlClient.SqlException>(
                async () => await context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE dbo.PriceListAuditEntries SET Source = 'Web' WHERE TenantId = {tenant} AND PriceListId = {created.Id.Value}",
                    token));
        Assert.AreEqual(51010, immutable.Number);

        // Το είδος παραμένει στον Κατάλογο και δεν διαγράφεται από άλλη επιχειρησιακή ενότητα.
        Assert.IsTrue(await context.CatalogItems.AsNoTracking()
            .AnyAsync(x => x.TenantId == tenant && x.Id == itemId, token));
    }
}
