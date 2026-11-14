using Aenaon.Application.Catalog;
using Aenaon.Application.Tenancy;
using Aenaon.Domain.Catalog;
using Aenaon.Domain.Tenancy;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Application.Tests;

/// <summary>Ελέγχει ότι η επιλογή τιμής είναι μόνο ανάγνωση και σέβεται τα δικαιώματα.</summary>
[TestClass]
public sealed class PriceResolutionServiceTests
{
    private static readonly Guid User = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid Tenant = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid Store = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid Item = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly DateOnly Date = new(2026, 11, 14);

    [TestMethod]
    public async Task AuthorizedRead_ReturnsCommercialAmountWithoutTaxCalculations()
    {
        var fake = new FakeStore();
        fake.Candidates.Add(Option(PriceListChannel.All, 10m));
        fake.Candidates.Add(Option(PriceListChannel.Delivery, 12.5000m));
        var service = Create(TenantMembershipRole.Member, fake);

        PriceResolutionResult result = await service.ResolveAsync(
            User, Tenant, Item, Store, PriceListChannel.Delivery, Date);

        Assert.AreEqual(PriceResolutionStatus.Resolved, result.Status);
        Assert.AreEqual(12.5000m, result.UnitPrice);
        Assert.IsTrue(result.PricesIncludeVat);
        Assert.AreEqual("EUR", result.Currency);
        Assert.AreEqual(1, fake.CandidateCalls);
        Assert.AreEqual(Tenant, fake.LastTenant);
    }

    [TestMethod]
    public async Task InvalidAndForeignScope_FailClosedBeforeStore()
    {
        var fake = new FakeStore();
        var service = Create(TenantMembershipRole.Owner, fake);
        PriceResolutionResult invalid = await service.ResolveAsync(
            User, Tenant, Item, Guid.Empty, PriceListChannel.All, Date);
        PriceResolutionResult invalidChannel = await service.ResolveAsync(
            User, Tenant, Item, Store, (PriceListChannel)88, Date);
        PriceResolutionResult foreignTenant = await service.ResolveAsync(
            User, Guid.NewGuid(), Item, Store, PriceListChannel.All, Date);
        PriceResolutionResult foreignStore = await service.ResolveAsync(
            User, Tenant, Item, Guid.NewGuid(), PriceListChannel.All, Date);

        Assert.AreEqual(PriceResolutionStatus.Invalid, invalid.Status);
        Assert.AreEqual(PriceResolutionStatus.Invalid, invalidChannel.Status);
        Assert.AreEqual(PriceResolutionStatus.Denied, foreignTenant.Status);
        Assert.AreEqual(PriceResolutionStatus.Denied, foreignStore.Status);
        Assert.AreEqual(0, fake.ActiveItemCalls);
        Assert.AreEqual(0, fake.CandidateCalls);
    }

    [TestMethod]
    public async Task MissingItemAndNoPriceRemainDistinct()
    {
        var fake = new FakeStore { Active = false };
        var service = Create(TenantMembershipRole.Owner, fake);
        PriceResolutionResult missing = await service.ResolveAsync(
            User, Tenant, Item, null, PriceListChannel.DineIn, Date);
        Assert.AreEqual(PriceResolutionStatus.ItemUnavailable, missing.Status);
        Assert.AreEqual(0, fake.CandidateCalls);

        fake.Active = true;
        PriceResolutionResult unpriced = await service.ResolveAsync(
            User, Tenant, Item, null, PriceListChannel.DineIn, Date);
        Assert.AreEqual(PriceResolutionStatus.NotPriced, unpriced.Status);
    }

    [TestMethod]
    public async Task DuplicateTopPriority_IsAmbiguousEvenWithDifferentAmounts()
    {
        var fake = new FakeStore();
        fake.Candidates.Add(Option(PriceListChannel.Retail, 10m));
        fake.Candidates.Add(Option(PriceListChannel.Retail, 20m));
        var service = Create(TenantMembershipRole.Owner, fake);
        PriceResolutionResult result = await service.ResolveAsync(
            User, Tenant, Item, null, PriceListChannel.Retail, Date);

        Assert.AreEqual(PriceResolutionStatus.Ambiguous, result.Status);
        Assert.IsNull(result.UnitPrice);
        Assert.HasCount(2, result.ConflictingPriceListIds!);
    }

    private static PriceResolutionCandidate Option(
        PriceListChannel channel, decimal price) =>
        new(Guid.NewGuid(), "P", null, channel, true, "EUR",
            Date.AddDays(-1), null, true, price);

    private static PriceResolutionService Create(TenantMembershipRole role, FakeStore store) =>
        new(new TenantAuthorizationService(new FakeAccess(role)), store);

    private sealed class FakeStore : IPriceResolutionStore
    {
        public bool Active = true;
        public List<PriceResolutionCandidate> Candidates { get; } = [];
        public int ActiveItemCalls;
        public int CandidateCalls;
        public Guid LastTenant;

        public Task<bool> IsActiveCatalogItemAsync(Guid tenantId, Guid itemId,
            CancellationToken token)
        {
            ActiveItemCalls++;
            return Task.FromResult(Active);
        }

        public Task<IReadOnlyList<PriceResolutionCandidate>> FindCandidatesAsync(
            Guid tenantId, Guid itemId, Guid? storeId, PriceListChannel channel,
            DateOnly businessDate, CancellationToken token)
        {
            CandidateCalls++;
            LastTenant = tenantId;
            return Task.FromResult<IReadOnlyList<PriceResolutionCandidate>>(Candidates);
        }
    }

    private sealed class FakeAccess(TenantMembershipRole role) : ITenantAccessStore
    {
        public Task<TenantMembershipRole?> GetMembershipRoleAsync(
            Guid userId, Guid tenantId, CancellationToken token = default) =>
            Task.FromResult<TenantMembershipRole?>(
                userId == User && tenantId == Tenant ? role : null);

        public Task<bool> StoreBelongsToTenantAsync(Guid tenantId, Guid storeId,
            CancellationToken token = default) =>
            Task.FromResult(tenantId == Tenant && storeId == Store);

        public Task<IReadOnlyList<AccessibleTenant>> ListUserTenantsAsync(
            Guid userId, CancellationToken token = default) =>
            Task.FromResult<IReadOnlyList<AccessibleTenant>>([]);
    }
}
