using Aenaon.Domain.Exceptions;
using Aenaon.Domain.Tenancy;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace Aenaon.Domain.Tests;

[TestClass]
public sealed class TenancyTests
{
    [TestMethod]
    public void CreateTenant_TrimsNameAndAssignsIdentity()
    {
        var tenant = Tenant.Create("  Επιχείρηση Demo  ");

        Assert.AreEqual("Επιχείρηση Demo", tenant.Name);
        Assert.AreNotEqual(Guid.Empty, tenant.Id);
        Assert.HasCount(0, tenant.Stores);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("Επιχείρηση\nDemo")]
    public void CreateTenant_RejectsInvalidName(string? name)
    {
        var error = Assert.ThrowsExactly<DomainRuleException>(() => Tenant.Create(name));

        Assert.AreEqual("tenant.name.invalid", error.Code);
    }

    [TestMethod]
    public void CreateTenant_AcceptsNameAtLimit()
    {
        var tenant = Tenant.Create(new string('Α', 160));

        Assert.AreEqual(160, tenant.Name.Length);
    }

    [TestMethod]
    public void CreateTenant_RejectsNameAboveLimit()
    {
        var error = Assert.ThrowsExactly<DomainRuleException>(() => Tenant.Create(new string('Α', 161)));

        Assert.AreEqual("tenant.name.invalid", error.Code);
    }

    [TestMethod]
    public void AddStore_NormalizesCodeAndAssignsOwner()
    {
        var tenant = Tenant.Create("Επιχείρηση Demo");

        var store = tenant.AddStore(" ath-01 ", "  Κατάστημα Αθήνας  ");

        Assert.AreNotEqual(Guid.Empty, store.Id);
        Assert.AreEqual(tenant.Id, store.TenantId);
        Assert.AreEqual("ATH-01", store.Code);
        Assert.AreEqual("Κατάστημα Αθήνας", store.Name);
        Assert.AreSame(store, Assert.ContainsSingle(tenant.Stores));
    }

    [TestMethod]
    [DataRow("ath-01")]
    [DataRow("ATH-01")]
    [DataRow(" ATH-01 ")]
    public void AddStore_RejectsDuplicateCodeWithoutChangingStores(string code)
    {
        var tenant = Tenant.Create("Επιχείρηση Demo");
        var original = tenant.AddStore("ATH-01", "Πρώτο κατάστημα");

        var error = Assert.ThrowsExactly<DomainRuleException>(() => tenant.AddStore(code, "Δεύτερο κατάστημα"));

        Assert.AreEqual("store.code.duplicate", error.Code);
        Assert.AreSame(original, Assert.ContainsSingle(tenant.Stores));
    }

    [TestMethod]
    public void AddStore_AllowsSameCodeInDifferentTenantsWithIndependentCollections()
    {
        var first = Tenant.Create("Πρώτη επιχείρηση");
        var second = Tenant.Create("Δεύτερη επιχείρηση");

        var firstStore = first.AddStore("MAIN", "Κεντρικό");
        var secondStore = second.AddStore("MAIN", "Κεντρικό");
        first.AddStore("SECOND", "Δεύτερο κατάστημα");

        Assert.AreNotEqual(first.Id, second.Id);
        Assert.AreNotEqual(firstStore.Id, secondStore.Id);
        Assert.AreEqual(first.Id, firstStore.TenantId);
        Assert.AreEqual(second.Id, secondStore.TenantId);
        Assert.HasCount(2, first.Stores);
        Assert.AreSame(secondStore, Assert.ContainsSingle(second.Stores));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("ATH 01")]
    [DataRow("ATH/01")]
    [DataRow("ATH\n01")]
    public void AddStore_RejectsInvalidCodeWithoutAddingStore(string? code)
    {
        var tenant = Tenant.Create("Επιχείρηση Demo");

        var error = Assert.ThrowsExactly<DomainRuleException>(() => tenant.AddStore(code, "Κατάστημα"));

        Assert.AreEqual("store.code.invalid", error.Code);
        Assert.HasCount(0, tenant.Stores);
    }

    [TestMethod]
    public void AddStore_RejectsCodeAboveLimit()
    {
        var tenant = Tenant.Create("Επιχείρηση Demo");

        var error = Assert.ThrowsExactly<DomainRuleException>(() => tenant.AddStore(new string('A', 31), "Κατάστημα"));

        Assert.AreEqual("store.code.invalid", error.Code);
        Assert.HasCount(0, tenant.Stores);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("Κατάστημα\tΑθήνας")]
    public void AddStore_RejectsInvalidNameWithoutAddingStore(string? name)
    {
        var tenant = Tenant.Create("Επιχείρηση Demo");

        var error = Assert.ThrowsExactly<DomainRuleException>(() => tenant.AddStore("ATH-01", name));

        Assert.AreEqual("store.name.invalid", error.Code);
        Assert.HasCount(0, tenant.Stores);
    }

    [TestMethod]
    public void AddStore_RejectsNameAboveLimitWithoutAddingStore()
    {
        var tenant = Tenant.Create("Επιχείρηση Demo");

        var error = Assert.ThrowsExactly<DomainRuleException>(() => tenant.AddStore("ATH-01", new string('Α', 161)));

        Assert.AreEqual("store.name.invalid", error.Code);
        Assert.HasCount(0, tenant.Stores);
    }

    [TestMethod]
    public void StoreCollection_RejectsExternalInsertionFromAnotherTenant()
    {
        var first = Tenant.Create("Πρώτη επιχείρηση");
        var second = Tenant.Create("Δεύτερη επιχείρηση");
        var store = second.AddStore("MAIN", "Κεντρικό");
        var exposedCollection = (IList<Store>)first.Stores;

        Assert.ThrowsExactly<NotSupportedException>(() => exposedCollection.Add(store));

        Assert.HasCount(0, first.Stores);
        Assert.AreSame(store, Assert.ContainsSingle(second.Stores));
    }
}
