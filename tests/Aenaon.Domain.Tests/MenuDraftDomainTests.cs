using Aenaon.Domain.Exceptions;
using Aenaon.Domain.Menus;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Domain.Tests;

/// <summary>Επαληθεύει την ιδιοκτησία Tenant, την αμεταβλητότητα ταυτότητας και τα πρόχειρα δεδομένα μενού.</summary>
[TestClass]
public sealed class MenuDraftDomainTests
{
    private static readonly Guid Tenant = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly DateTime Timestamp = new(2026, 11, 17, 8, 0, 0, DateTimeKind.Utc);

    [TestMethod]
    public void Menu_CreatesTenantScopedImmutableCode()
    {
        RestaurantMenu menu = RestaurantMenu.Create(Tenant, " lunch-01 ", " Μεσημεριανό ");
        Assert.AreEqual(Tenant, menu.TenantId);
        Assert.AreEqual("LUNCH-01", menu.Code);
        Assert.AreEqual("Μεσημεριανό", menu.Name);
        Assert.IsFalse(menu.IsArchived);
        menu.Rename("Βραδινό");
        Assert.AreEqual("Βραδινό", menu.Name);
        Assert.AreEqual("LUNCH-01", menu.Code);
        Assert.ThrowsExactly<DomainRuleException>(() =>
            RestaurantMenu.Create(Guid.Empty, "X", "Name"));
        Assert.ThrowsExactly<DomainRuleException>(() =>
            RestaurantMenu.Create(Tenant, "", "Name"));
        Assert.ThrowsExactly<DomainRuleException>(() =>
            menu.Rename(new string('x', RestaurantMenu.MaxNameLength + 1)));
    }

    [TestMethod]
    public void Draft_RequiresUtcAndAdvancesRevision()
    {
        MenuDraft draft = MenuDraft.Create(Tenant, Guid.NewGuid(), Timestamp);
        Assert.AreEqual(1L, draft.Revision);
        draft.Touch(Timestamp.AddMinutes(2));
        Assert.AreEqual(2L, draft.Revision);
        Assert.AreEqual(Timestamp.AddMinutes(2), draft.EditedUtc);
        Assert.ThrowsExactly<DomainRuleException>(() =>
            draft.Touch(DateTime.SpecifyKind(Timestamp, DateTimeKind.Local)));
        Assert.AreEqual(2L, draft.Revision);
        Assert.ThrowsExactly<DomainRuleException>(() =>
            MenuDraft.Create(Tenant, Guid.Empty, Timestamp));
    }

    [TestMethod]
    public void Categories_ValidateTenantIdentityAndPosition()
    {
        Guid menu = Guid.NewGuid();
        MenuDraftCategory category = MenuDraftCategory.Create(Tenant, menu, "Ορεκτικά", 1);
        category.Update("Σαλάτες", false);
        category.SetPosition(3);
        Assert.AreEqual("Σαλάτες", category.Name);
        Assert.AreEqual(3, category.Position);
        Assert.IsFalse(category.IsVisible);
        Assert.AreEqual(menu, category.MenuId);
        Assert.ThrowsExactly<DomainRuleException>(() => category.SetPosition(0));
        Assert.ThrowsExactly<DomainRuleException>(() =>
            MenuDraftCategory.Create(Guid.Empty, menu, "Bad", 1));
        Assert.ThrowsExactly<DomainRuleException>(() =>
            category.Update(" ", true));
        Assert.AreEqual("Σαλάτες", category.Name);
    }

    [TestMethod]
    public void Item_ReferencesCatalogAndNeverOwnsPrice()
    {
        Guid menu = Guid.NewGuid();
        Guid category = Guid.NewGuid();
        Guid catalog = Guid.NewGuid();
        MenuDraftItem item = MenuDraftItem.Create(Tenant, menu, category, catalog, 1, " Χωριάτικη ");
        Assert.AreEqual(catalog, item.CatalogItemId);
        Assert.AreEqual(category, item.MenuCategoryId);
        Assert.AreEqual("Χωριάτικη", item.NameOverride);
        item.Rename(null);
        item.SetPosition(2);
        Assert.IsNull(item.NameOverride);
        Assert.AreEqual(2, item.Position);
        Assert.ThrowsExactly<DomainRuleException>(() =>
            MenuDraftItem.Create(Tenant, menu, category, Guid.Empty, 1));
        Assert.ThrowsExactly<DomainRuleException>(() =>
            item.Rename(new string('a', MenuDraftItem.MaxNameOverrideLength + 1)));
    }
}
