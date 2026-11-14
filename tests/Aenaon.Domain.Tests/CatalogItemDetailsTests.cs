using Aenaon.Domain.Catalog;
using Aenaon.Domain.Exceptions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Domain.Tests;

/// <summary>Έλεγχοι GTIN, εσωτερικών barcodes, λεπτομερειών και απομόνωσης επιχείρησης.</summary>
[TestClass]
public sealed class CatalogItemDetailsTests
{
    [TestMethod]
    public void ProductAndService_AcceptOptionalDetailsAndNormalizeBarcode()
    {
        Guid tenant = Guid.NewGuid();
        Guid category = Guid.NewGuid();
        CatalogItem product = CatalogItem.Create(tenant, category, "P01", "Προϊόν",
            CatalogItemKind.Product, false,
            barcode: " 4006381333931 ", longDescription: "  Περιγραφή προϊόντος.  ");
        CatalogItem service = CatalogItem.Create(tenant, category, "S01", "Υπηρεσία",
            CatalogItemKind.Service, false,
            barcode: "srv-01", longDescription: "Πρώτη γραμμή\nΔεύτερη γραμμή");

        Assert.AreEqual("4006381333931", product.Barcode);
        Assert.AreEqual("Περιγραφή προϊόντος.", product.LongDescription);
        Assert.AreEqual("SRV-01", service.Barcode);
        Assert.Contains("Δεύτερη γραμμή", service.LongDescription!);
        Assert.IsNull(CatalogItem.Create(tenant, category, "P02", "Χωρίς barcode",
            CatalogItemKind.Product, false).Barcode);
    }

    [TestMethod]
    public void InvalidGtinOrDescription_DoesNotPartiallyMutate()
    {
        CatalogItem item = CatalogItem.Create(Guid.NewGuid(), Guid.NewGuid(), "P01",
            "Προϊόν", CatalogItemKind.Product, false,
            barcode: "ABC-001", longDescription: "Αρχικό κείμενο");

        DomainRuleException invalidGtin = Assert.ThrowsExactly<DomainRuleException>(() =>
            item.UpdateDetails("4006381333932", "Νέο κείμενο"));
        Assert.AreEqual("catalog.item.barcode.check_digit", invalidGtin.Code);
        Assert.AreEqual("ABC-001", item.Barcode);
        Assert.AreEqual("Αρχικό κείμενο", item.LongDescription);

        Assert.AreEqual("catalog.item.barcode.invalid",
            Assert.ThrowsExactly<DomainRuleException>(() =>
                item.UpdateDetails("barcode με κενά", "Άλλο")).Code);
        Assert.AreEqual("catalog.item.barcode.invalid",
            Assert.ThrowsExactly<DomainRuleException>(() =>
                item.UpdateDetails(new string('X', CatalogItemDetails.MaxBarcodeLength + 1), "Άλλο")).Code);
        Assert.AreEqual("catalog.item.description.invalid",
            Assert.ThrowsExactly<DomainRuleException>(() =>
                item.UpdateDetails("ABC-001", new string('α',
                    CatalogItemDetails.MaxLongDescriptionLength + 1))).Code);
        Assert.AreEqual("catalog.item.description.invalid",
            Assert.ThrowsExactly<DomainRuleException>(() =>
                item.UpdateDetails("ABC-001", "Μη αποδεκτό\0χαρακτήρα")).Code);
        Assert.AreEqual("Αρχικό κείμενο", item.LongDescription);
    }

    [TestMethod]
    public void ClearingDetails_IsExplicitAndKeepsCoreIdentity()
    {
        CatalogItem item = CatalogItem.Create(Guid.NewGuid(), Guid.NewGuid(), "P01",
            "Προϊόν", CatalogItemKind.Product, false, barcode: "INT-01",
            longDescription: "Λεπτομέρειες");
        Guid id = item.Id;
        item.UpdateDetails("   ", "");
        Assert.IsNull(item.Barcode);
        Assert.IsNull(item.LongDescription);
        Assert.AreEqual(id, item.Id);
        Assert.AreEqual("P01", item.Code);
        Assert.AreEqual(CatalogItemKind.Product, item.Kind);
    }

    [TestMethod]
    public void TenantCatalog_DoesNotAllowSameBarcodeAcrossProductAndService()
    {
        Guid category;
        ProductCatalog first = ProductCatalog.Create(Guid.NewGuid());
        category = first.AddCategory("GEN", "Γενικά").Id;
        CatalogItem product = first.AddItem(category, "P01", "Προϊόν",
            CatalogItemKind.Product, barcode: "int-01");
        Assert.AreEqual("INT-01", product.Barcode);
        Assert.AreEqual("catalog.item.barcode.duplicate",
            Assert.ThrowsExactly<DomainRuleException>(() => first.AddItem(
                category, "S01", "Υπηρεσία", CatalogItemKind.Service,
                barcode: "INT-01")).Code);
        CatalogItem service = first.AddItem(category, "S02", "Υπηρεσία",
            CatalogItemKind.Service, barcode: "OTH-02");
        Assert.AreEqual("catalog.item.barcode.duplicate",
            Assert.ThrowsExactly<DomainRuleException>(() => first.UpdateItemDetails(
                service.Id, "int-01", "Προσπάθεια διπλού")).Code);
        Assert.AreEqual("OTH-02", service.Barcode);
        Assert.HasCount(2, first.Items);

        ProductCatalog independent = ProductCatalog.Create(Guid.NewGuid());
        Guid otherCategory = independent.AddCategory("GEN", "Γενικά").Id;
        CatalogItem other = independent.AddItem(otherCategory, "X01", "Άλλο",
            CatalogItemKind.Product, barcode: "int-01");
        Assert.AreEqual("INT-01", other.Barcode);
    }
}
