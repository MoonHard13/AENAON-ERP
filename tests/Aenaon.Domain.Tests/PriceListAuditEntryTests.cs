using Aenaon.Domain.Catalog;
using Aenaon.Domain.Exceptions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Domain.Tests;

/// <summary>Έλεγχοι μορφής και αμεταβλητότητας του εμπορικού ιστορικού.</summary>
[TestClass]
public sealed class PriceListAuditEntryTests
{
    [TestMethod]
    public void PriceChange_CapturesTenantActorAndFourDecimalAmounts()
    {
        Guid tenant = Guid.NewGuid();
        Guid list = Guid.NewGuid();
        Guid item = Guid.NewGuid();
        Guid actor = Guid.NewGuid();

        PriceListAuditEntry entry = PriceListAuditEntry.Create(
            tenant, list, item, actor, "CLI",
            PriceListAuditAction.UpdatePrice, beforePrice: 9.1234m, afterPrice: 10.5678m);

        Assert.AreEqual(tenant, entry.TenantId);
        Assert.AreEqual(list, entry.PriceListId);
        Assert.AreEqual(item, entry.CatalogItemId);
        Assert.AreEqual(actor, entry.ActorUserId);
        Assert.AreEqual("CLI", entry.Source);
        Assert.AreEqual(9.1234m, entry.BeforePrice);
        Assert.AreEqual(10.5678m, entry.AfterPrice);
        Assert.IsTrue(entry.OccurredUtc.Kind == DateTimeKind.Utc);
        Assert.IsTrue(entry.OccurredUtc <= DateTime.UtcNow);
    }

    [TestMethod]
    public void InvalidCrossTypeAndSource_AreRejected()
    {
        Guid tenant = Guid.NewGuid();
        Guid list = Guid.NewGuid();
        Guid item = Guid.NewGuid();

        Assert.ThrowsExactly<DomainRuleException>(() =>
            PriceListAuditEntry.Create(tenant, list, null, null,
                "Web", PriceListAuditAction.AddPrice, afterPrice: 1m));
        Assert.ThrowsExactly<DomainRuleException>(() =>
            PriceListAuditEntry.Create(tenant, list, item, null,
                "Web", PriceListAuditAction.UpdateList, beforePolicy: "old"));
        Assert.ThrowsExactly<DomainRuleException>(() =>
            PriceListAuditEntry.Create(tenant, list, null, null,
                "Injected", PriceListAuditAction.UpdateList));
        Assert.ThrowsExactly<DomainRuleException>(() =>
            PriceListAuditEntry.Create(Guid.Empty, list, null, null,
                "Web", PriceListAuditAction.CreateList));
    }
}
