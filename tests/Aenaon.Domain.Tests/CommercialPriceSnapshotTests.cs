using Aenaon.Domain.Catalog;
using Aenaon.Domain.Exceptions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Domain.Tests;

/// <summary>Ελέγχει την αμεταβλητότητα και την εγκυρότητα εμπορικού snapshot.</summary>
[TestClass]
public sealed class CommercialPriceSnapshotTests
{
    private static readonly Guid Tenant = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid Store = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid Item = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid List = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly DateOnly Day = new(2026, 11, 15);
    private static readonly DateTimeOffset Captured = new(2026, 11, 15, 12, 30, 0, TimeSpan.Zero);

    [TestMethod]
    public void Capture_PreservesPriceContextAndVersion()
    {
        CommercialPriceSnapshot snapshot = Make(priceListStoreId: Store,
            priceListChannel: PriceListChannel.All, amount: 12.3456m);

        Assert.AreEqual(1, snapshot.ContractVersion);
        Assert.AreEqual(PriceResolutionPolicy.PolicyVersion, snapshot.SelectionPolicyVersion);
        Assert.AreEqual(Tenant, snapshot.TenantId);
        Assert.AreEqual(Store, snapshot.StoreId);
        Assert.AreEqual(Item, snapshot.CatalogItemId);
        Assert.AreEqual(List, snapshot.PriceListId);
        Assert.AreEqual("RETAIL", snapshot.PriceListCode);
        Assert.AreEqual(Store, snapshot.PriceListStoreId);
        Assert.AreEqual(PriceListChannel.All, snapshot.PriceListChannel);
        Assert.AreEqual(PriceListChannel.Delivery, snapshot.RequestedChannel);
        Assert.AreEqual(Day, snapshot.BusinessDate);
        Assert.AreEqual(12.3456m, snapshot.UnitPrice);
        Assert.IsTrue(snapshot.PricesIncludeVat);
        Assert.AreEqual("EUR", snapshot.Currency);
        Assert.AreEqual(Captured, snapshot.CapturedUtc);
    }

    [TestMethod]
    public void Capture_RejectsForeignStoreAndUnrelatedChannel()
    {
        Assert.ThrowsExactly<DomainRuleException>(() =>
            Make(priceListStoreId: Guid.NewGuid()));
        Assert.ThrowsExactly<DomainRuleException>(() =>
            Make(withoutStore: true, priceListStoreId: Store));
        Assert.ThrowsExactly<DomainRuleException>(() =>
            Make(priceListChannel: PriceListChannel.Takeaway));
        Assert.ThrowsExactly<DomainRuleException>(() =>
            Make(priceListChannel: (PriceListChannel)99));
        Assert.ThrowsExactly<DomainRuleException>(() =>
            Make(requestedChannel: (PriceListChannel)99));
    }

    [TestMethod]
    public void Capture_RejectsInvalidReferenceCurrencyPriceAndTimestamp()
    {
        Assert.ThrowsExactly<DomainRuleException>(() => Make(tenantId: Guid.Empty));
        Assert.ThrowsExactly<DomainRuleException>(() => Make(catalogItemId: Guid.Empty));
        Assert.ThrowsExactly<DomainRuleException>(() => Make(priceListId: Guid.Empty));
        Assert.ThrowsExactly<DomainRuleException>(() => Make(priceListCode: " "));
        Assert.ThrowsExactly<DomainRuleException>(() => Make(currency: "USD"));
        Assert.ThrowsExactly<DomainRuleException>(() => Make(amount: -1m));
        Assert.ThrowsExactly<DomainRuleException>(() => Make(amount: 1.23456m));
        Assert.ThrowsExactly<DomainRuleException>(() => Make(businessDate: DateOnly.MinValue));
        Assert.ThrowsExactly<DomainRuleException>(() =>
            Make(capturedUtc: new DateTimeOffset(2026, 11, 15, 14, 30, 0,
                TimeSpan.FromHours(2))));
    }

    [TestMethod]
    public void Capture_DoesNotChangeAfterSourcePriceListChanges()
    {
        PriceList list = PriceList.Create(Tenant, Store, "RETAIL", "Αρχικός",
            PriceListChannel.Delivery, true, Day);
        CommercialPriceSnapshot snapshot = CommercialPriceSnapshot.Capture(
            Tenant, Store, Item, list.Id, list.Code, list.StoreId, list.Channel,
            PriceListChannel.Delivery, Day, 10m, list.PricesIncludeVat, list.Currency,
            Captured);

        list.UpdateCommercialProfile("Νέος", null, PriceListChannel.All,
            false, Day.AddDays(1), null);
        list.ChangeActive(false);

        Assert.AreEqual(10m, snapshot.UnitPrice);
        Assert.AreEqual(Store, snapshot.PriceListStoreId);
        Assert.AreEqual(PriceListChannel.Delivery, snapshot.PriceListChannel);
        Assert.IsTrue(snapshot.PricesIncludeVat);
        Assert.AreEqual(Day, snapshot.BusinessDate);
        Assert.AreEqual(PriceResolutionPolicy.PolicyVersion, snapshot.SelectionPolicyVersion);
    }

    private static CommercialPriceSnapshot Make(Guid? tenantId = null,
        Guid? storeId = null, bool withoutStore = false, Guid? catalogItemId = null, Guid? priceListId = null,
        string? priceListCode = "RETAIL", Guid? priceListStoreId = null,
        PriceListChannel priceListChannel = PriceListChannel.Delivery,
        PriceListChannel requestedChannel = PriceListChannel.Delivery,
        DateOnly? businessDate = null, decimal amount = 10m,
        string? currency = "EUR", DateTimeOffset? capturedUtc = null) =>
        CommercialPriceSnapshot.Capture(tenantId ?? Tenant, withoutStore ? null : storeId ?? Store,
            catalogItemId ?? Item, priceListId ?? List, priceListCode,
            priceListStoreId, priceListChannel, requestedChannel,
            businessDate ?? Day, amount, true, currency, capturedUtc ?? Captured);
}
