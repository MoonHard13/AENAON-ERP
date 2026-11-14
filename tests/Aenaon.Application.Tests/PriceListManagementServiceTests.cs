using Aenaon.Application.Catalog;
using Aenaon.Application.Tenancy;
using Aenaon.Domain.Catalog;
using Aenaon.Domain.Tenancy;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Application.Tests;

/// <summary>Ελέγχει πρόσβαση, επικύρωση και απομόνωση χωρίς σύνδεση SQL.</summary>
[TestClass]
public sealed class PriceListManagementServiceTests
{
    private static readonly Guid User = Guid.Parse("c4dbe076-0125-438d-9bf1-8e8ddc18f46f");
    private static readonly Guid Tenant = Guid.Parse("1fe9efcb-2aa9-4538-9530-254431b841a0");

    [TestMethod]
    public async Task Owner_UsesSharedStoreForListCreationAndPrices()
    {
        var store = new FakeStore();
        var service = Create(TenantMembershipRole.Owner, store);
        PriceListPage? list = await service.ListAsync(User, Tenant);
        PriceListResult created = await service.ApplyAsync(User, Tenant,
            new(PriceListAction.Create, Code: "RETAIL", Name: "Λιανική",
                Channel: PriceListChannel.Retail,
                ValidFrom: new DateOnly(2026, 11, 13)));
        PriceListResult prices = await service.SetPricesAsync(User, Tenant,
            Guid.NewGuid(), [new PriceListPriceChange(Guid.NewGuid(), 12.3456m)]);

        Assert.IsNotNull(list);
        Assert.AreEqual(PriceListStatus.Saved, created.Status);
        Assert.AreEqual(PriceListStatus.Saved, prices.Status);
        Assert.AreEqual(1, store.ReadCalls);
        Assert.AreEqual(1, store.ApplyCalls);
        Assert.AreEqual(1, store.PriceCalls);
        Assert.AreEqual(Tenant, store.LastTenant);
    }

    [TestMethod]
    public async Task MemberCanReadButCannotMutate()
    {
        var store = new FakeStore();
        var service = Create(TenantMembershipRole.Member, store);
        PriceListPage? view = await service.ListAsync(User, Tenant);
        PriceListResult create = await service.ApplyAsync(User, Tenant,
            new(PriceListAction.Create, Code: "A", Name: "A",
                ValidFrom: new DateOnly(2026, 11, 13)));
        PriceListResult prices = await service.SetPricesAsync(User, Tenant,
            Guid.NewGuid(), [new PriceListPriceChange(Guid.NewGuid(), 10m)]);

        Assert.IsNotNull(view);
        Assert.AreEqual(PriceListStatus.Denied, create.Status);
        Assert.AreEqual(PriceListStatus.Denied, prices.Status);
        Assert.AreEqual(0, store.ApplyCalls);
        Assert.AreEqual(0, store.PriceCalls);
        Assert.AreEqual(PriceListStatus.Denied,
            (await service.RemovePriceAsync(User, Tenant, Guid.NewGuid(),
                Guid.NewGuid(), 10m)).Status);
        Assert.AreEqual(0, store.RemoveCalls);
    }

    [TestMethod]
    public async Task ForeignTenantIsDeniedBeforeStore()
    {
        var store = new FakeStore();
        var service = Create(TenantMembershipRole.Owner, store);
        Assert.IsNull(await service.ListAsync(User, Guid.NewGuid()));
        Assert.IsNull(await service.GetAsync(User, Guid.NewGuid(), Guid.NewGuid()));
        Assert.AreEqual(PriceListStatus.Denied,
            (await service.SetPricesAsync(User, Guid.NewGuid(), Guid.NewGuid(),
                [new PriceListPriceChange(Guid.NewGuid(), 5m)])).Status);
        Assert.AreEqual(0, store.ReadCalls);
        Assert.AreEqual(0, store.PriceCalls);
    }

    [TestMethod]
    public async Task InvalidAmountsAndDuplicatesDoNotReachPersistence()
    {
        var store = new FakeStore();
        var service = Create(TenantMembershipRole.Owner, store);
        Guid item = Guid.NewGuid();
        PriceListPriceChange[][] invalid =
        [
            [new(item, -1m)],
            [new(item, 1.12345m)],
            [new(item, 2m, -1m, CheckExpected: true)],
            [new(item, 2m, 1.12345m, CheckExpected: true)],
            [new(item, 1m), new(item, 2m)],
            []
        ];
        foreach (PriceListPriceChange[] changes in invalid)
        {
            PriceListResult result = await service.SetPricesAsync(
                User, Tenant, Guid.NewGuid(), changes);
            Assert.AreEqual(PriceListStatus.Invalid, result.Status);
        }
        Assert.AreEqual(0, store.PriceCalls);
    }

    [TestMethod]
    public async Task InvalidScopeAndDatesDoNotWrite()
    {
        var store = new FakeStore();
        var service = Create(TenantMembershipRole.Owner, store);
        PriceListResult scope = await service.ApplyAsync(User, Tenant,
            new(PriceListAction.Create, Code: "P", Name: "P", StoreId: Guid.Empty,
                ValidFrom: new DateOnly(2026, 11, 13)));
        PriceListResult period = await service.ApplyAsync(User, Tenant,
            new(PriceListAction.Create, Code: "P", Name: "P",
                ValidFrom: new DateOnly(2026, 12, 30),
                ValidUntil: new DateOnly(2026, 1, 1)));
        Assert.AreEqual(PriceListStatus.Invalid, scope.Status);
        Assert.AreEqual(PriceListStatus.Invalid, period.Status);
        Assert.AreEqual(0, store.ApplyCalls);
    }

    [TestMethod]
    public async Task OwnerCanRemovePriceButInvalidExpectedPriceIsRejected()
    {
        var store = new FakeStore();
        var service = Create(TenantMembershipRole.Owner, store);
        PriceListResult denied = await service.RemovePriceAsync(User, Tenant,
            Guid.NewGuid(), Guid.NewGuid(), -1m);
        Assert.AreEqual(PriceListStatus.Invalid, denied.Status);
        Assert.AreEqual(0, store.RemoveCalls);

        PriceListResult removed = await service.RemovePriceAsync(User, Tenant,
            Guid.NewGuid(), Guid.NewGuid(), 12.5000m);
        Assert.AreEqual(PriceListStatus.Saved, removed.Status);
        Assert.AreEqual(1, store.RemoveCalls);
    }

    [TestMethod]
    public async Task HistoryIsReadOnlyTenantScopedAndAvailableToMember()
    {
        var store = new FakeStore();
        var member = Create(TenantMembershipRole.Member, store);
        PriceListHistoryPage? history = await member.ReadHistoryAsync(User, Tenant, Guid.NewGuid());
        Assert.IsNotNull(history);
        Assert.AreEqual(1, store.HistoryCalls);
        Assert.AreEqual(Tenant, store.LastTenant);
        Assert.IsNull(await member.ReadHistoryAsync(User, Guid.NewGuid(), Guid.NewGuid()));
        Assert.AreEqual(1, store.HistoryCalls);
        await Assert.ThrowsExactlyAsync<Aenaon.Domain.Exceptions.DomainRuleException>(
            async () => await member.ReadHistoryAsync(User, Tenant, Guid.NewGuid(), page: 0));
    }

    [TestMethod]
    public async Task MutationForwardsAuthenticatedActorAndOrigin()
    {
        var store = new FakeStore();
        var owner = Create(TenantMembershipRole.Owner, store);
        await owner.SetPricesAsync(User, Tenant, Guid.NewGuid(),
            [new(Guid.NewGuid(), 1.2500m)], source: "CLI");
        Assert.AreEqual(User, store.LastActor);
        Assert.AreEqual("CLI", store.LastSource);
        await owner.ApplyAsync(User, Tenant,
            new(PriceListAction.Create, Code: "A", Name: "A",
                ValidFrom: new DateOnly(2026, 11, 13)));
        Assert.AreEqual(User, store.LastActor);
        Assert.AreEqual("Web", store.LastSource);
    }

    [TestMethod]
    public async Task ItemSortPassesValidatedSelectionAndRejectsUnknownSort()
    {
        var store = new FakeStore();
        var service = Create(TenantMembershipRole.Member, store);
        Guid listId = Guid.NewGuid();

        PriceListItemsPage? rows = await service.ReadItemsAsync(
            User, Tenant, listId, page: 2, sortBy: "price", descending: true);
        Assert.IsNotNull(rows);
        Assert.AreEqual("price", store.LastItemsSort);
        Assert.IsTrue(store.LastItemsDescending);
        Assert.AreEqual(1, store.ReadItemCalls);

        await Assert.ThrowsExactlyAsync<Aenaon.Domain.Exceptions.DomainRuleException>(
            async () => await service.ReadItemsAsync(
                User, Tenant, listId, sortBy: "unsafe SQL ORDER BY"));
        Assert.AreEqual(1, store.ReadItemCalls);

        Assert.IsNull(await service.ReadItemsAsync(User, Guid.NewGuid(), listId,
            sortBy: "name"));
        Assert.AreEqual(1, store.ReadItemCalls);
    }

    private static PriceListManagementService Create(TenantMembershipRole role, FakeStore store) =>
        new(new TenantAuthorizationService(new FakeAccess(role)), store);

    private sealed class FakeStore : IPriceListManagementStore
    {
        public int ReadCalls;
        public int ApplyCalls;
        public int PriceCalls;
        public int RemoveCalls;
        public int ReadItemCalls;
        public string? LastItemsSort;
        public bool LastItemsDescending;
        public int HistoryCalls;
        public Guid LastTenant;
        public Guid? LastActor;
        public string? LastSource;

        public Task<PriceListPage> ListAsync(Guid tenantId, string? search,
            bool includeInactive, int page, int pageSize, CancellationToken token)
        {
            ReadCalls++;
            LastTenant = tenantId;
            return Task.FromResult(new PriceListPage([], page, false));
        }

        public Task<PriceListSummary?> GetAsync(Guid tenantId, Guid id, CancellationToken token) =>
            Task.FromResult<PriceListSummary?>(null);

        public Task<PriceListItemsPage?> ReadItemsAsync(Guid tenantId, Guid listId,
            string? search, int page, int pageSize, CancellationToken token,
            string sortBy = "code", bool descending = false)
        {
            ReadItemCalls++;
            LastItemsSort = sortBy;
            LastItemsDescending = descending;
            return Task.FromResult<PriceListItemsPage?>(new([], page, false));
        }

        public Task<PriceListHistoryPage?> ReadHistoryAsync(Guid tenantId, Guid priceListId,
            int page, int pageSize, CancellationToken token)
        {
            HistoryCalls++;
            LastTenant = tenantId;
            return Task.FromResult<PriceListHistoryPage?>(new PriceListHistoryPage([], page, false));
        }

        public Task<PriceListResult> ApplyAsync(Guid tenantId, PriceListChange change,
            CancellationToken token, Guid? actorUserId = null, string source = "System")
        {
            ApplyCalls++;
            LastActor = actorUserId;
            LastSource = source;
            return Task.FromResult(new PriceListResult(PriceListStatus.Saved, Guid.NewGuid()));
        }

        public Task<PriceListResult> SetPricesAsync(Guid tenantId, Guid priceListId,
            IReadOnlyList<PriceListPriceChange> changes, CancellationToken token, Guid? actorUserId = null, string source = "System")
        {
            PriceCalls++;
            LastActor = actorUserId;
            LastSource = source;
            return Task.FromResult(new PriceListResult(PriceListStatus.Saved, priceListId));
        }

        public Task<PriceListResult> RemovePriceAsync(Guid tenantId, Guid priceListId,
            Guid catalogItemId, decimal expectedUnitPrice, CancellationToken token, Guid? actorUserId = null, string source = "System")
        {
            RemoveCalls++;
            return Task.FromResult(new PriceListResult(PriceListStatus.Saved, priceListId));
        }
    }

    private sealed class FakeAccess(TenantMembershipRole role) : ITenantAccessStore
    {
        public Task<TenantMembershipRole?> GetMembershipRoleAsync(
            Guid userId, Guid tenantId, CancellationToken token = default) =>
            Task.FromResult<TenantMembershipRole?>(
                userId == User && tenantId == Tenant ? role : null);

        public Task<bool> StoreBelongsToTenantAsync(Guid tenantId, Guid storeId,
            CancellationToken token = default) => Task.FromResult(tenantId == Tenant);

        public Task<IReadOnlyList<AccessibleTenant>> ListUserTenantsAsync(
            Guid userId, CancellationToken token = default) =>
            Task.FromResult<IReadOnlyList<AccessibleTenant>>([]);
    }
}
