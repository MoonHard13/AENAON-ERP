using Aenaon.Application.Tenancy;
using Aenaon.Domain.Tenancy;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Application.Tests;

/// <summary>Παλινδρομικοί έλεγχοι για την επιλογή επιχείρησης/καταστήματος χωρίς διαρροή ή σιωπηλή μεταφορά.</summary>
[TestClass]
public sealed class TenantWorkspaceResolverTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();
    private static readonly Guid StoreA = Guid.NewGuid();
    private static readonly Guid StoreB = Guid.NewGuid();

    [TestMethod]
    public async Task AuthorizedUser_ResolvesDefaultTenantAndStore()
    {
        var accessStore = new ResolverAccessStore
        {
            Tenants = [new AccessibleTenant(TenantA, "A", TenantMembershipRole.Owner)]
        };
        var stores = new ResolverStores
        {
            Items = [new TenantStoreSummary(StoreA, TenantA, "MAIN", "Κεντρικό")]
        };
        var resolver = CreateResolver(accessStore, stores);

        TenantWorkspaceSnapshot result = await resolver.ResolveAsync(UserId, null, null);

        Assert.AreEqual(TenantWorkspaceStatus.Ready, result.Status);
        Assert.AreEqual(TenantA, result.Tenant?.TenantId);
        Assert.AreEqual(StoreA, result.Store?.StoreId);
        Assert.AreEqual(1, stores.Calls);
    }

    [TestMethod]
    public async Task ForeignTenant_IsDeniedWithoutReadingStores()
    {
        var accessStore = new ResolverAccessStore
        {
            Tenants = [new AccessibleTenant(TenantA, "A", TenantMembershipRole.Owner)]
        };
        var stores = new ResolverStores
        {
            Items = [new TenantStoreSummary(StoreA, TenantA, "MAIN", "Κεντρικό")]
        };
        var resolver = CreateResolver(accessStore, stores);

        TenantWorkspaceSnapshot result = await resolver.ResolveAsync(UserId, TenantB, null);

        Assert.AreEqual(TenantWorkspaceStatus.InvalidTenant, result.Status);
        Assert.IsNull(result.Tenant);
        Assert.IsNull(result.Store);
        Assert.HasCount(0, result.Stores);
        Assert.AreEqual(0, stores.Calls);
    }

    [TestMethod]
    public async Task ForeignStore_IsNotReplacedByFirstAvailableStore()
    {
        var accessStore = new ResolverAccessStore
        {
            Tenants = [new AccessibleTenant(TenantA, "A", TenantMembershipRole.Owner)]
        };
        var stores = new ResolverStores
        {
            Items =
            [
                new TenantStoreSummary(StoreA, TenantA, "MAIN", "Κεντρικό"),
                new TenantStoreSummary(StoreB, TenantA, "B", "Δεύτερο")
            ]
        };
        var resolver = CreateResolver(accessStore, stores);

        TenantWorkspaceSnapshot result = await resolver.ResolveAsync(UserId, TenantA, Guid.NewGuid());

        Assert.AreEqual(TenantWorkspaceStatus.InvalidStore, result.Status);
        Assert.IsNull(result.Store);
        Assert.AreEqual(TenantA, result.Tenant?.TenantId);
        Assert.HasCount(2, result.Stores);
    }

    [TestMethod]
    public async Task StoreWithoutExplicitTenant_IsDeniedWithoutStoreRead()
    {
        var accessStore = new ResolverAccessStore
        {
            Tenants = [new AccessibleTenant(TenantA, "A", TenantMembershipRole.Owner)]
        };
        var stores = new ResolverStores();
        var result = await CreateResolver(accessStore, stores).ResolveAsync(UserId, null, StoreA);

        Assert.AreEqual(TenantWorkspaceStatus.InvalidStore, result.Status);
        Assert.IsNull(result.Store);
        Assert.AreEqual(0, stores.Calls);
    }

    [TestMethod]
    public async Task ExplicitValidStore_IsSelectedById()
    {
        var accessStore = new ResolverAccessStore
        {
            Tenants = [new AccessibleTenant(TenantA, "A", TenantMembershipRole.Owner)]
        };
        var stores = new ResolverStores
        {
            Items =
            [
                new TenantStoreSummary(StoreA, TenantA, "MAIN", "Κεντρικό"),
                new TenantStoreSummary(StoreB, TenantA, "B", "Δεύτερο")
            ]
        };
        var result = await CreateResolver(accessStore, stores).ResolveAsync(UserId, TenantA, StoreB);

        Assert.AreEqual(TenantWorkspaceStatus.Ready, result.Status);
        Assert.AreEqual(StoreB, result.Store?.StoreId);
    }

    [TestMethod]
    public async Task EmptyStores_IsClearStatusNotFakeSelectedStore()
    {
        var accessStore = new ResolverAccessStore
        {
            Tenants = [new AccessibleTenant(TenantA, "A", TenantMembershipRole.Owner)]
        };
        var result = await CreateResolver(accessStore, new ResolverStores()).ResolveAsync(
            UserId, TenantA, null);

        Assert.AreEqual(TenantWorkspaceStatus.NoStores, result.Status);
        Assert.IsNull(result.Store);
    }

    [TestMethod]
    public async Task NoMemberships_EmitsNoTenantsAndNoStoreAccess()
    {
        var accessStore = new ResolverAccessStore();
        var stores = new ResolverStores();
        var result = await CreateResolver(accessStore, stores).ResolveAsync(UserId, TenantA, StoreA);

        Assert.AreEqual(TenantWorkspaceStatus.NoTenants, result.Status);
        Assert.HasCount(0, result.Tenants);
        Assert.AreEqual(0, stores.Calls);
    }

    [TestMethod]
    public async Task InvalidIdentity_DoesNotQueryDatabase()
    {
        var accessStore = new ResolverAccessStore();
        var stores = new ResolverStores();
        var result = await CreateResolver(accessStore, stores).ResolveAsync(Guid.Empty, TenantA, StoreA);

        Assert.AreEqual(TenantWorkspaceStatus.NoTenants, result.Status);
        Assert.AreEqual(0, accessStore.TenantReads);
        Assert.AreEqual(0, stores.Calls);
    }

    [TestMethod]
    public async Task Cancellation_StopsBeforeStorage()
    {
        var accessStore = new ResolverAccessStore();
        var stores = new ResolverStores();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            CreateResolver(accessStore, stores).ResolveAsync(UserId, TenantA, StoreA, cancellation.Token));
        Assert.AreEqual(0, accessStore.TenantReads);
    }

    private static TenantWorkspaceResolver CreateResolver(ResolverAccessStore store,
        ResolverStores catalog)
    {
        var authorization = new TenantAuthorizationService(store);
        return new TenantWorkspaceResolver(authorization, new TenantWorkspaceService(authorization, catalog));
    }

    private sealed class ResolverAccessStore : ITenantAccessStore
    {
        public IReadOnlyList<AccessibleTenant> Tenants { get; init; } = [];
        public int TenantReads { get; private set; }

        public Task<IReadOnlyList<AccessibleTenant>> ListUserTenantsAsync(Guid userId,
            CancellationToken cancellationToken = default)
        {
            TenantReads++;
            return Task.FromResult(Tenants);
        }

        public Task<TenantMembershipRole?> GetMembershipRoleAsync(Guid userId, Guid tenantId,
            CancellationToken cancellationToken = default)
        {
            TenantMembershipRole? role = Tenants.FirstOrDefault(tenant => tenant.TenantId == tenantId)?.Role;
            return Task.FromResult(role);
        }

        public Task<bool> StoreBelongsToTenantAsync(Guid tenantId, Guid storeId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
    }

    private sealed class ResolverStores : ITenantStoreCatalog
    {
        public IReadOnlyList<TenantStoreSummary> Items { get; init; } = [];
        public int Calls { get; private set; }

        public Task<IReadOnlyList<TenantStoreSummary>> ListStoresAsync(Guid tenantId,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            IReadOnlyList<TenantStoreSummary> matching = Items.Where(store => store.TenantId == tenantId)
                .ToArray();
            return Task.FromResult(matching);
        }
    }
}
