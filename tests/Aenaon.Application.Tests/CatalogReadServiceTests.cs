using Aenaon.Application.Catalog;
using Aenaon.Application.Tenancy;
using Aenaon.Domain.Catalog;
using Aenaon.Domain.Exceptions;
using Aenaon.Domain.Tenancy;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Application.Tests;

/// <summary>Ελέγχει αυστηρή εξουσιοδότηση, όρια σελίδων και ελαχιστοποίηση δεδομένων.</summary>
[TestClass]
public sealed class CatalogReadServiceTests
{
    private static readonly Guid OwnerUser = Guid.Parse("b5ac8049-5c2e-4ea7-af55-921a280ee981");
    private static readonly Guid MemberUser = Guid.Parse("f6faf4c1-d873-4572-bbb1-f3c4e7d42826");
    private static readonly Guid Tenant = Guid.Parse("a17c2241-3b86-45e2-8a6d-f8bfcd276a60");
    private static readonly Guid Category = Guid.Parse("d1580e25-891a-4cd9-a950-9ffac7dac88c");

    [TestMethod]
    public async Task OwnerAndMember_CanReadSelectedTenantOnly()
    {
        var store = new FakeCatalogStore();
        var service = new CatalogReadService(
            new TenantAuthorizationService(new FakeTenantAccessStore()), store);

        CatalogReadResult<CatalogItemSummary> owner = await service.ReadItemsAsync(
            OwnerUser, Tenant, new CatalogReadQuery());
        CatalogReadResult<CatalogItemSummary> member = await service.ReadItemsAsync(
            MemberUser, Tenant, new CatalogReadQuery(2, 10, " SER "));

        Assert.IsTrue(owner.IsAuthorized);
        Assert.IsTrue(member.IsAuthorized);
        Assert.AreEqual(2, store.ItemReads);
        Assert.AreEqual(Tenant, store.LastTenantId);
        Assert.AreEqual("SER", store.LastQuery!.Search);
        Assert.AreEqual(10, store.LastQuery.PageSize);
        Assert.AreEqual(2, store.LastQuery.Page);
        Assert.HasCount(1, owner.Page!.Rows);
        Assert.AreEqual(CatalogItemKind.Service, owner.Page.Rows[0].Kind);
        Assert.IsFalse(owner.Page.Rows[0].TracksStock);
    }

    [TestMethod]
    public async Task ItemKindFilter_IsPassedToAuthorizedStore()
    {
        var store = new FakeCatalogStore();
        var service = new CatalogReadService(
            new TenantAuthorizationService(new FakeTenantAccessStore()), store);

        CatalogReadResult<CatalogItemSummary> result = await service.ReadItemsAsync(
            OwnerUser, Tenant, new CatalogReadQuery(1, 25, Kind: CatalogItemKind.Service));

        Assert.IsTrue(result.IsAuthorized);
        Assert.AreEqual(CatalogItemKind.Service, store.LastQuery!.Kind);
        Assert.AreEqual(1, store.ItemReads);
    }

    [TestMethod]
    public async Task ForeignTenantAndUnknownIdentity_AreDeniedWithoutDatabaseCatalogReads()
    {
        var store = new FakeCatalogStore();
        var service = new CatalogReadService(
            new TenantAuthorizationService(new FakeTenantAccessStore()), store);

        foreach (Guid user in new[] { OwnerUser, Guid.NewGuid(), Guid.Empty })
        {
            CatalogReadResult<CatalogCategorySummary> denied = await service.ReadCategoriesAsync(
                user, Guid.NewGuid(), new CatalogReadQuery());
            Assert.IsFalse(denied.IsAuthorized);
            Assert.IsNull(denied.Page);
        }

        CatalogReadResult<CatalogItemSummary> noTenant = await service.ReadItemsAsync(
            OwnerUser, Guid.Empty, new CatalogReadQuery());
        Assert.IsFalse(noTenant.IsAuthorized);
        Assert.AreEqual(0, store.CategoryReads);
        Assert.AreEqual(0, store.ItemReads);
    }

    [TestMethod]
    public async Task ValidCategoryQuery_GetsOnlyTenantScopedRead()
    {
        var store = new FakeCatalogStore();
        var service = new CatalogReadService(
            new TenantAuthorizationService(new FakeTenantAccessStore()), store);

        CatalogReadResult<CatalogCategorySummary> result = await service.ReadCategoriesAsync(
            OwnerUser, Tenant, new CatalogReadQuery(1, 25, null, true));
        Assert.IsTrue(result.IsAuthorized);
        Assert.AreEqual(1, store.CategoryReads);
        Assert.AreEqual(Tenant, store.LastTenantId);
        Assert.IsTrue(store.LastQuery!.IncludeInactive);
        Assert.HasCount(1, result.Page!.Rows);
    }

    [TestMethod]
    public async Task SortByAndDirection_ArePassedToTenantScopedReadStore()
    {
        var store = new FakeCatalogStore();
        var service = new CatalogReadService(
            new TenantAuthorizationService(new FakeTenantAccessStore()), store);

        CatalogReadResult<CatalogItemSummary> items = await service.ReadItemsAsync(
            MemberUser, Tenant, new CatalogReadQuery(
                Page: 2, PageSize: 10, Kind: CatalogItemKind.Product,
                SortBy: "name", Descending: true));
        Assert.IsTrue(items.IsAuthorized);
        Assert.AreEqual("name", store.LastQuery?.SortBy);
        Assert.IsTrue(store.LastQuery?.Descending);
        Assert.AreEqual(2, store.LastQuery?.Page);

        CatalogReadResult<CatalogCategorySummary> categories = await service.ReadCategoriesAsync(
            MemberUser, Tenant, new CatalogReadQuery(SortBy: "status"));
        Assert.IsTrue(categories.IsAuthorized);
        Assert.AreEqual("status", store.LastQuery?.SortBy);
        Assert.IsFalse(store.LastQuery?.Descending);
    }

    [TestMethod]
    public async Task InvalidQueries_FailBeforeAuthorizationOrSql()
    {
        var store = new FakeCatalogStore();
        var access = new FakeTenantAccessStore();
        var service = new CatalogReadService(new TenantAuthorizationService(access), store);

        CatalogReadQuery[] invalid =
        [
            new(0, 25),
            new(1001, 25),
            new(1, 0),
            new(1, 101),
            new(1, 25, new string('x', 81)),
            new(1, 25, "bad\nline"),
            new(1, 25, Kind: (CatalogItemKind)999),
            new(1, 25, SortBy: "name;DROP TABLE CatalogItems"),
            new(1, 25, SortBy: "unknown"),
            new(1, 25, SortBy: "category"),
            new(1, 25, SortBy: "stock")
        ];

        foreach (CatalogReadQuery query in invalid)
        {
            DomainRuleException exception = await Assert.ThrowsExactlyAsync<DomainRuleException>(
                () => service.ReadItemsAsync(OwnerUser, Tenant, query));
            Assert.AreEqual("catalog.read.query.invalid", exception.Code);
        }

        Assert.AreEqual(0, store.ItemReads);
        Assert.AreEqual(0, access.RoleReads);
    }

    [TestMethod]
    public async Task CancelledRequest_DoesNotPerformReads()
    {
        var store = new FakeCatalogStore();
        var service = new CatalogReadService(
            new TenantAuthorizationService(new FakeTenantAccessStore()), store);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            service.ReadItemsAsync(OwnerUser, Tenant, new CatalogReadQuery(), cancelled.Token));
        Assert.AreEqual(0, store.ItemReads);
    }

    private sealed class FakeCatalogStore : ICatalogReadStore
    {
        public int CategoryReads { get; private set; }
        public int ItemReads { get; private set; }
        public Guid LastTenantId { get; private set; }
        public CatalogReadQuery? LastQuery { get; private set; }

        public Task<CatalogReadPage<CatalogCategorySummary>> ReadCategoriesAsync(
            Guid tenantId, CatalogReadQuery query, CancellationToken cancellationToken = default)
        {
            CategoryReads++;
            LastTenantId = tenantId;
            LastQuery = query;
            return Task.FromResult(new CatalogReadPage<CatalogCategorySummary>(
                [new(Category, "SERVICES", "Υπηρεσίες", true)],
                query.Page, query.PageSize, false));
        }

        public Task<CatalogReadPage<CatalogItemSummary>> ReadItemsAsync(
            Guid tenantId, CatalogReadQuery query, CancellationToken cancellationToken = default)
        {
            ItemReads++;
            LastTenantId = tenantId;
            LastQuery = query;
            return Task.FromResult(new CatalogReadPage<CatalogItemSummary>(
                [new(Guid.NewGuid(), Category, "SERV01", "Catering", CatalogItemKind.Service, false, true)],
                query.Page, query.PageSize, false));
        }
    }

    private sealed class FakeTenantAccessStore : ITenantAccessStore
    {
        public int RoleReads { get; private set; }

        public Task<TenantMembershipRole?> GetMembershipRoleAsync(
            Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
        {
            RoleReads++;
            TenantMembershipRole? role = tenantId == Tenant
                ? userId == OwnerUser ? TenantMembershipRole.Owner
                    : userId == MemberUser ? TenantMembershipRole.Member : null
                : null;
            return Task.FromResult(role);
        }

        public Task<bool> StoreBelongsToTenantAsync(
            Guid tenantId, Guid storeId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<IReadOnlyList<AccessibleTenant>> ListUserTenantsAsync(
            Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AccessibleTenant>>([]);
    }
}
