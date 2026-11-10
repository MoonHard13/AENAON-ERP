using Aenaon.Domain.Catalog;
using Aenaon.Domain.Exceptions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Domain.Tests;

[TestClass]
public sealed class ProductCatalogTests
{
    [TestMethod]
    public void CreateCatalog_RejectsMissingTenant()
    {
        var error = Assert.ThrowsExactly<DomainRuleException>(
            () => ProductCatalog.Create(Guid.Empty));

        Assert.AreEqual("catalog.tenant.required", error.Code);
    }

    [TestMethod]
    public void AddCategory_AssignsTenantNormalizesCodeAndTrimsName()
    {
        Guid tenantId = Guid.NewGuid();
        var catalog = ProductCatalog.Create(tenantId);

        var category = catalog.AddCategory("  food_01 ", "  Φαγητά  ");

        Assert.AreEqual(tenantId, catalog.TenantId);
        Assert.AreEqual(tenantId, category.TenantId);
        Assert.AreNotEqual(Guid.Empty, category.Id);
        Assert.AreEqual("FOOD_01", category.Code);
        Assert.AreEqual("Φαγητά", category.Name);
        Assert.IsTrue(category.IsActive);
        Assert.AreSame(category, Assert.ContainsSingle(catalog.Categories));
    }

    [TestMethod]
    [DataRow("food")]
    [DataRow("FOOD")]
    [DataRow(" FOOD ")]
    public void AddCategory_RejectsDuplicateWithoutMutation(string code)
    {
        var catalog = ProductCatalog.Create(Guid.NewGuid());
        var original = catalog.AddCategory("FOOD", "Φαγητά");

        var error = Assert.ThrowsExactly<DomainRuleException>(
            () => catalog.AddCategory(code, "Δεύτερη κατηγορία"));

        Assert.AreEqual("catalog.category.code.duplicate", error.Code);
        Assert.AreSame(original, Assert.ContainsSingle(catalog.Categories));
    }

    [TestMethod]
    public void SeparateTenants_CanReuseCodesWithoutSharingCollections()
    {
        var first = ProductCatalog.Create(Guid.NewGuid());
        var second = ProductCatalog.Create(Guid.NewGuid());
        var firstCategory = first.AddCategory("FOOD", "Φαγητά");
        var secondCategory = second.AddCategory("FOOD", "Φαγητά");

        first.AddItem(firstCategory.Id, "SKU01", "Μαργαρίτα", CatalogItemKind.Product);
        second.AddItem(secondCategory.Id, "SKU01", "Μαργαρίτα", CatalogItemKind.Product);

        Assert.AreNotEqual(first.TenantId, second.TenantId);
        Assert.HasCount(1, first.Categories);
        Assert.HasCount(1, second.Categories);
        Assert.AreNotEqual(firstCategory.Id, secondCategory.Id);
        Assert.AreNotEqual(first.Items[0].Id, second.Items[0].Id);
        Assert.AreEqual(first.TenantId, first.Items[0].TenantId);
        Assert.AreEqual(second.TenantId, second.Items[0].TenantId);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("  ")]
    [DataRow("A B")]
    [DataRow("A/B")]
    [DataRow("ABC\nDEF")]
    public void AddCategory_RejectsInvalidCodeWithoutMutation(string? code)
    {
        var catalog = ProductCatalog.Create(Guid.NewGuid());

        var error = Assert.ThrowsExactly<DomainRuleException>(
            () => catalog.AddCategory(code, "Φαγητά"));

        Assert.AreEqual("catalog.category.code.invalid", error.Code);
        Assert.HasCount(0, catalog.Categories);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("   ")]
    [DataRow("Φαγητά\tΠοτά")]
    public void AddCategory_RejectsInvalidNameWithoutMutation(string? name)
    {
        var catalog = ProductCatalog.Create(Guid.NewGuid());

        var error = Assert.ThrowsExactly<DomainRuleException>(
            () => catalog.AddCategory("FOOD", name));

        Assert.AreEqual("catalog.category.name.invalid", error.Code);
        Assert.HasCount(0, catalog.Categories);
    }

    [TestMethod]
    public void CategoryLimits_AcceptsBoundaryRejectsExcess()
    {
        var catalog = ProductCatalog.Create(Guid.NewGuid());
        var category = catalog.AddCategory(new string('A', CatalogCategory.MaxCodeLength),
            new string('Α', CatalogCategory.MaxNameLength));

        Assert.AreEqual(CatalogCategory.MaxCodeLength, category.Code.Length);
        Assert.AreEqual(CatalogCategory.MaxNameLength, category.Name.Length);

        var error = Assert.ThrowsExactly<DomainRuleException>(() =>
            catalog.AddCategory(new string('B', CatalogCategory.MaxCodeLength + 1), "Φαγητά"));

        Assert.AreEqual("catalog.category.code.invalid", error.Code);
        Assert.HasCount(1, catalog.Categories);
    }

    [TestMethod]
    public void AddItem_ProductAndServiceHaveIndependentStockRules()
    {
        var catalog = ProductCatalog.Create(Guid.NewGuid());
        var category = catalog.AddCategory("GENERAL", "Γενικά");

        var product = catalog.AddItem(category.Id, "prod-01", "  Espresso  ",
            CatalogItemKind.Product, tracksStock: true);
        var service = catalog.AddItem(category.Id, "serv-01", "  Catering  ",
            CatalogItemKind.Service);

        Assert.AreEqual(catalog.TenantId, product.TenantId);
        Assert.AreEqual(category.Id, product.CategoryId);
        Assert.AreEqual("PROD-01", product.Code);
        Assert.AreEqual("Espresso", product.Name);
        Assert.AreEqual(CatalogItemKind.Product, product.Kind);
        Assert.IsTrue(product.TracksStock);
        Assert.IsTrue(product.IsActive);
        Assert.AreEqual(CatalogItemKind.Service, service.Kind);
        Assert.IsFalse(service.TracksStock);
        Assert.HasCount(2, catalog.Items);
    }

    [TestMethod]
    public void AddService_RejectsStockTrackingWithoutMutation()
    {
        var catalog = ProductCatalog.Create(Guid.NewGuid());
        var category = catalog.AddCategory("SERVICES", "Υπηρεσίες");

        var error = Assert.ThrowsExactly<DomainRuleException>(() =>
            catalog.AddItem(category.Id, "S01", "Catering", CatalogItemKind.Service, tracksStock: true));

        Assert.AreEqual("catalog.item.service.stock_forbidden", error.Code);
        Assert.HasCount(0, catalog.Items);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(99)]
    public void AddItem_RejectsUnknownKind(int kind)
    {
        var catalog = ProductCatalog.Create(Guid.NewGuid());
        var category = catalog.AddCategory("GEN", "Γενικά");

        var error = Assert.ThrowsExactly<DomainRuleException>(() =>
            catalog.AddItem(category.Id, "A01", "Είδος", (CatalogItemKind)kind));

        Assert.AreEqual("catalog.item.kind.invalid", error.Code);
        Assert.HasCount(0, catalog.Items);
    }

    [TestMethod]
    public void AddItem_RejectsDuplicateCodeAcrossProductAndService()
    {
        var catalog = ProductCatalog.Create(Guid.NewGuid());
        var category = catalog.AddCategory("GEN", "Γενικά");
        var original = catalog.AddItem(category.Id, "ITEM01", "Προϊόν", CatalogItemKind.Product);

        var error = Assert.ThrowsExactly<DomainRuleException>(() =>
            catalog.AddItem(category.Id, " item01 ", "Υπηρεσία", CatalogItemKind.Service));

        Assert.AreEqual("catalog.item.code.duplicate", error.Code);
        Assert.AreSame(original, Assert.ContainsSingle(catalog.Items));
    }

    [TestMethod]
    public void CrossTenantCategory_CannotBeUsed()
    {
        var first = ProductCatalog.Create(Guid.NewGuid());
        var second = ProductCatalog.Create(Guid.NewGuid());
        var foreignCategory = second.AddCategory("GEN", "Γενικά");

        var error = Assert.ThrowsExactly<DomainRuleException>(() =>
            first.AddItem(foreignCategory.Id, "ITEM01", "Προϊόν", CatalogItemKind.Product));

        Assert.AreEqual("catalog.category.not_found", error.Code);
        Assert.HasCount(0, first.Items);
    }

    [TestMethod]
    public void InactiveCategory_RejectsNewItemsAndMovesWithoutSideEffects()
    {
        var catalog = ProductCatalog.Create(Guid.NewGuid());
        var initialCategory = catalog.AddCategory("GEN", "Γενικά");
        var disabledCategory = catalog.AddCategory("OFF", "Ανενεργά");
        var item = catalog.AddItem(initialCategory.Id, "A01", "Προϊόν", CatalogItemKind.Product);
        catalog.SetCategoryActive(disabledCategory.Id, false);

        var addError = Assert.ThrowsExactly<DomainRuleException>(() =>
            catalog.AddItem(disabledCategory.Id, "A02", "Δεύτερο", CatalogItemKind.Product));
        var moveError = Assert.ThrowsExactly<DomainRuleException>(() =>
            catalog.ChangeItemCategory(item.Id, disabledCategory.Id));

        Assert.AreEqual("catalog.category.inactive", addError.Code);
        Assert.AreEqual("catalog.category.inactive", moveError.Code);
        Assert.AreEqual(initialCategory.Id, item.CategoryId);
        Assert.HasCount(1, catalog.Items);
    }

    [TestMethod]
    public void Changes_ValidateBeforeUpdatingAndPreserveIdentity()
    {
        var catalog = ProductCatalog.Create(Guid.NewGuid());
        var first = catalog.AddCategory("FIRST", "Πρώτη");
        var second = catalog.AddCategory("SECOND", "Δεύτερη");
        var item = catalog.AddItem(first.Id, "ITEM01", "Περιγραφή", CatalogItemKind.Product);

        Assert.AreEqual("catalog.category.code.duplicate",
            Assert.ThrowsExactly<DomainRuleException>(() =>
                catalog.ChangeCategoryCode(first.Id, "second")).Code);
        Assert.AreEqual("catalog.item.code.invalid",
            Assert.ThrowsExactly<DomainRuleException>(() =>
                catalog.ChangeItemCode(item.Id, "INVALID CODE")).Code);
        Assert.AreEqual("FIRST", first.Code);
        Assert.AreEqual("ITEM01", item.Code);

        catalog.ChangeCategoryCode(first.Id, "updated");
        catalog.RenameCategory(first.Id, "  Ενημερωμένη  ");
        catalog.ChangeItemCode(item.Id, "updated-01");
        catalog.RenameItem(item.Id, "  Νέα περιγραφή ");
        catalog.ChangeItemCategory(item.Id, second.Id);
        catalog.SetItemActive(item.Id, false);

        Assert.AreEqual("UPDATED", first.Code);
        Assert.AreEqual("Ενημερωμένη", first.Name);
        Assert.AreEqual("UPDATED-01", item.Code);
        Assert.AreEqual("Νέα περιγραφή", item.Name);
        Assert.AreEqual(second.Id, item.CategoryId);
        Assert.IsFalse(item.IsActive);
        Assert.AreEqual(catalog.TenantId, item.TenantId);
    }

    [TestMethod]
    public void SetStockTracking_RejectsServiceButAllowsProducts()
    {
        var catalog = ProductCatalog.Create(Guid.NewGuid());
        var category = catalog.AddCategory("GEN", "Γενικά");
        var service = catalog.AddItem(category.Id, "S01", "Υπηρεσία", CatalogItemKind.Service);
        var product = catalog.AddItem(category.Id, "P01", "Προϊόν", CatalogItemKind.Product);

        var error = Assert.ThrowsExactly<DomainRuleException>(() =>
            catalog.SetItemStockTracking(service.Id, true));

        Assert.AreEqual("catalog.item.service.stock_forbidden", error.Code);
        Assert.IsFalse(service.TracksStock);

        catalog.SetItemStockTracking(product.Id, true);
        Assert.IsTrue(product.TracksStock);
        catalog.SetItemStockTracking(product.Id, false);
        Assert.IsFalse(product.TracksStock);
    }

    [TestMethod]
    public void CatalogCollections_AreReadOnlyToCallers()
    {
        var catalog = ProductCatalog.Create(Guid.NewGuid());
        var category = catalog.AddCategory("GEN", "Γενικά");
        var item = catalog.AddItem(category.Id, "P01", "Προϊόν", CatalogItemKind.Product);

        Assert.ThrowsExactly<NotSupportedException>(() =>
            ((IList<CatalogCategory>)catalog.Categories).Clear());
        Assert.ThrowsExactly<NotSupportedException>(() =>
            ((IList<CatalogItem>)catalog.Items).Clear());

        Assert.AreSame(category, Assert.ContainsSingle(catalog.Categories));
        Assert.AreSame(item, Assert.ContainsSingle(catalog.Items));
    }

    [TestMethod]
    public void UnknownCategoryAndItem_AreRejected()
    {
        var catalog = ProductCatalog.Create(Guid.NewGuid());

        Assert.AreEqual("catalog.category.not_found",
            Assert.ThrowsExactly<DomainRuleException>(() =>
                catalog.SetCategoryActive(Guid.NewGuid(), false)).Code);
        Assert.AreEqual("catalog.item.not_found",
            Assert.ThrowsExactly<DomainRuleException>(() =>
                catalog.SetItemActive(Guid.NewGuid(), false)).Code);
        Assert.HasCount(0, catalog.Items);
    }

    [TestMethod]
    public void ItemLimitsAndInvalidNames_DoNotMutateCollection()
    {
        var catalog = ProductCatalog.Create(Guid.NewGuid());
        var category = catalog.AddCategory("GEN", "Γενικά");

        var item = catalog.AddItem(category.Id, new string('A', CatalogItem.MaxCodeLength),
            new string('Α', CatalogItem.MaxNameLength), CatalogItemKind.Product);
        Assert.AreEqual(CatalogItem.MaxCodeLength, item.Code.Length);
        Assert.AreEqual(CatalogItem.MaxNameLength, item.Name.Length);

        Assert.AreEqual("catalog.item.code.invalid",
            Assert.ThrowsExactly<DomainRuleException>(() => catalog.AddItem(category.Id,
                new string('B', CatalogItem.MaxCodeLength + 1), "Είδος", CatalogItemKind.Product)).Code);
        Assert.AreEqual("catalog.item.name.invalid",
            Assert.ThrowsExactly<DomainRuleException>(() => catalog.AddItem(category.Id,
                "B01", "Είδος\nδεύτερο", CatalogItemKind.Product)).Code);

        Assert.HasCount(1, catalog.Items);
    }
}
