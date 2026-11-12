using Aenaon.Application.Catalog;
using Aenaon.Application.Tenancy;
using Aenaon.Domain.Catalog;
using Aenaon.Domain.Tenancy;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Application.Tests;

/// <summary>Έλεγχος ασφάλειας εγγραφών καταλόγου ανεξάρτητα από SQL Server.</summary>
[TestClass]
public sealed class CatalogWriteServiceTests
{
    private static readonly Guid User = Guid.Parse("c4dbe076-0125-438d-9bf1-8e8ddc18f46f");
    private static readonly Guid Tenant = Guid.Parse("1fe9efcb-2aa9-4538-9530-254431b841a0");

    [TestMethod]
    public async Task Owner_CanCreateCategoryViaSharedService()
    {
        var fixture = new FakeStore();
        var service = CreateService(TenantMembershipRole.Owner, fixture);

        CatalogChangeResult result = await service.ApplyAsync(
            User, Tenant, new(CatalogManagedEntity.Category, CatalogChangeAction.Create,
                Code: "FOOD", Name: "Φαγητά"));

        Assert.AreEqual(CatalogChangeStatus.Saved, result.Status);
        Assert.AreEqual(1, fixture.ApplyCalls);
        Assert.AreEqual(Tenant, fixture.LastTenant);
    }

    [TestMethod]
    public async Task MemberAndForeignTenant_AreDeniedBeforeReachingStore()
    {
        var fixture = new FakeStore();
        var service = CreateService(TenantMembershipRole.Member, fixture);

        CatalogChangeResult member = await service.ApplyAsync(
            User, Tenant, new(CatalogManagedEntity.Category, CatalogChangeAction.Create,
                Code: "FOOD", Name: "Φαγητά"));
        CatalogChangeResult foreign = await service.ApplyAsync(
            User, Guid.NewGuid(), new(CatalogManagedEntity.Product, CatalogChangeAction.Create,
                Code: "ITEM", Name: "Προϊόν", CategoryId: Guid.NewGuid()));

        Assert.AreEqual(CatalogChangeStatus.Denied, member.Status);
        Assert.AreEqual(CatalogChangeStatus.Denied, foreign.Status);
        Assert.AreEqual(0, fixture.ApplyCalls);
        Assert.AreEqual(0, fixture.DetailCalls);
    }

    [TestMethod]
    public async Task InvalidCommands_NeverCallWriteStore()
    {
        var fixture = new FakeStore();
        var service = CreateService(TenantMembershipRole.Owner, fixture);
        CatalogChangeCommand[] invalid =
        [
            new(CatalogManagedEntity.Service, CatalogChangeAction.Create,
                Code: "SERVICE", Name: "Υπηρεσία", CategoryId: Guid.NewGuid(), TracksStock: true),
            new(CatalogManagedEntity.Product, CatalogChangeAction.Update,
                Code: "SKU", Name: "Προϊόν"),
            new(CatalogManagedEntity.Category, CatalogChangeAction.Create,
                Id: Guid.NewGuid(), Code: "FOOD", Name: "Φαγητά"),
            new((CatalogManagedEntity)99, CatalogChangeAction.Create,
                Code: "TEST", Name: "Δοκιμή")
        ];

        foreach (CatalogChangeCommand command in invalid)
        {
            CatalogChangeResult result = await service.ApplyAsync(User, Tenant, command);
            Assert.AreEqual(CatalogChangeStatus.Invalid, result.Status);
        }

        Assert.AreEqual(0, fixture.ApplyCalls);
    }

    [TestMethod]
    public async Task ManageDetails_RequireOwnerAndDoNotFallBackToForeignTenant()
    {
        var fixture = new FakeStore();
        var owner = CreateService(TenantMembershipRole.Owner, fixture);
        var member = CreateService(TenantMembershipRole.Member, fixture);

        CatalogManageResult allow = await owner.GetDetailsAsync(
            User, Tenant, CatalogManagedEntity.Product, null);
        CatalogManageResult deny = await member.GetDetailsAsync(
            User, Tenant, CatalogManagedEntity.Product, null);

        Assert.IsTrue(allow.IsAuthorized);
        Assert.IsFalse(deny.IsAuthorized);
        Assert.AreEqual(1, fixture.DetailCalls);
    }

    private static CatalogWriteService CreateService(
        TenantMembershipRole role, FakeStore store) =>
        new(new TenantAuthorizationService(new FakeAccess(role)), store);

    private sealed class FakeStore : ICatalogWriteStore
    {
        public int ApplyCalls { get; private set; }
        public int DetailCalls { get; private set; }
        public Guid LastTenant { get; private set; }

        public Task<CatalogChangeResult> ApplyAsync(Guid tenantId,
            CatalogChangeCommand command, CancellationToken cancellationToken = default)
        {
            ApplyCalls++;
            LastTenant = tenantId;
            return Task.FromResult(new CatalogChangeResult(CatalogChangeStatus.Saved, Guid.NewGuid()));
        }

        public Task<CatalogManageDetails> GetDetailsAsync(Guid tenantId,
            CatalogManagedEntity entity, Guid? id, CancellationToken cancellationToken = default)
        {
            DetailCalls++;
            return Task.FromResult(new CatalogManageDetails(null, []));
        }
    }

    private sealed class FakeAccess(TenantMembershipRole role) : ITenantAccessStore
    {
        public Task<TenantMembershipRole?> GetMembershipRoleAsync(
            Guid userId, Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<TenantMembershipRole?>(
                userId == User && tenantId == Tenant ? role : null);

        public Task<bool> StoreBelongsToTenantAsync(
            Guid tenantId, Guid storeId, CancellationToken cancellationToken = default) =>
            Task.FromResult(tenantId == Tenant);

        public Task<IReadOnlyList<AccessibleTenant>> ListUserTenantsAsync(
            Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AccessibleTenant>>(
                userId == User ? [new AccessibleTenant(Tenant, "Δοκιμή", role)] : []);
    }
}
