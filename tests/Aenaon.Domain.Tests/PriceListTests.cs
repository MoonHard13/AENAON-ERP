using Aenaon.Domain.Catalog;
using Aenaon.Domain.Exceptions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Domain.Tests;

/// <summary>Έλεγχοι απομόνωσης, διάρκειας ισχύος και ακριβών χρηματικών ποσών.</summary>
[TestClass]
public sealed class PriceListTests
{
    private static readonly Guid Tenant = Guid.Parse("11111111-1111-4111-8111-111111111111");

    [TestMethod]
    public void Create_NormalizesCodeAndPreservesCommercialMeaning()
    {
        var list = PriceList.Create(Tenant, null, " retail-1 ", " Λιανική ",
            PriceListChannel.Retail, pricesIncludeVat: true,
            new DateOnly(2026, 11, 13), new DateOnly(2027, 1, 31));

        Assert.AreEqual("RETAIL-1", list.Code);
        Assert.AreEqual("Λιανική", list.Name);
        Assert.AreEqual("EUR", list.Currency);
        Assert.IsNull(list.StoreId);
        Assert.IsTrue(list.PricesIncludeVat);
        Assert.IsTrue(list.IsValidOn(new DateOnly(2026, 11, 13)));
        Assert.IsTrue(list.IsValidOn(new DateOnly(2027, 1, 31)));
        Assert.IsFalse(list.IsValidOn(new DateOnly(2027, 2, 1)));

        list.ChangeActive(false);
        Assert.IsFalse(list.IsValidOn(new DateOnly(2026, 12, 1)));
        list.ChangeActive(true);
        list.Update("Νέα ονομασία", new DateOnly(2026, 11, 15), null);
        Assert.IsFalse(list.IsValidOn(new DateOnly(2026, 11, 14)));
        Assert.IsTrue(list.IsValidOn(new DateOnly(2030, 1, 1)));
        Assert.AreEqual("RETAIL-1", list.Code);
        Assert.AreEqual(PriceListChannel.Retail, list.Channel);
    }

    [TestMethod]
    public void Create_RejectsForeignOrEmptyScopeAndInvalidDates()
    {
        Assert.ThrowsExactly<DomainRuleException>(() =>
            PriceList.Create(Guid.Empty, null, "X", "X", PriceListChannel.All,
                false, new DateOnly(2026, 1, 1)));
        Assert.ThrowsExactly<DomainRuleException>(() =>
            PriceList.Create(Tenant, Guid.Empty, "X", "X", PriceListChannel.All,
                false, new DateOnly(2026, 1, 1)));
        Assert.ThrowsExactly<DomainRuleException>(() =>
            PriceList.Create(Tenant, null, "X", "X", (PriceListChannel)42,
                false, new DateOnly(2026, 1, 1)));
        Assert.ThrowsExactly<DomainRuleException>(() =>
            PriceList.Create(Tenant, null, "X", "X", PriceListChannel.All,
                false, new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 1)));
        Assert.ThrowsExactly<DomainRuleException>(() =>
            PriceList.Create(Tenant, null, "X", "X", PriceListChannel.All,
                false, default));
    }

    [TestMethod]
    public void PriceListItem_AcceptsFourDigitsAndRejectsInvalidPrice()
    {
        Guid list = Guid.NewGuid();
        Guid item = Guid.NewGuid();
        var value = PriceListItem.Create(Tenant, list, item, 12.3456m);
        Assert.AreEqual(12.3456m, value.UnitPrice);
        value.ChangePrice(0m);
        Assert.AreEqual(0m, value.UnitPrice);

        Assert.ThrowsExactly<DomainRuleException>(() => value.ChangePrice(-0.0001m));
        Assert.ThrowsExactly<DomainRuleException>(() => value.ChangePrice(1.23456m));
        Assert.ThrowsExactly<DomainRuleException>(() => value.ChangePrice(PriceListItem.MaxUnitPrice + 1m));
        Assert.ThrowsExactly<DomainRuleException>(() =>
            PriceListItem.Create(Tenant, Guid.Empty, item, 1m));
        Assert.ThrowsExactly<DomainRuleException>(() =>
            PriceListItem.Create(Tenant, list, Guid.Empty, 1m));
        Assert.AreEqual(0m, value.UnitPrice);
    }
}
