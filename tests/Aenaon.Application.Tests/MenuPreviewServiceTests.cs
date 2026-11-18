using Aenaon.Application.Menus;
using Aenaon.Application.Tenancy;
using Aenaon.Domain.Catalog;
using Aenaon.Domain.Tenancy;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Application.Tests;

/// <summary>Fail-closed έλεγχοι Preview με απομονωμένα δεδομένα και χωρίς SQL writes.</summary>
[TestClass]
public sealed class MenuPreviewServiceTests
{
    private static readonly Guid User = Guid.Parse("a9cf0191-34a0-4e26-8994-4c7eb6300901");
    private static readonly Guid Tenant = Guid.Parse("ab9548de-a557-47ad-b69b-dcc12a5ab151");
    private static readonly Guid Store = Guid.Parse("b78f15cd-e47c-4718-8324-cd9ae9eb7558");
    private static readonly Guid Menu = Guid.Parse("b495e2c4-b2c1-4a45-bcb2-a275975ff138");
    private static readonly DateOnly Date = new(2026, 11, 18);

    [TestMethod]
    public async Task Member_SeesOnlyVisibleActiveItemsWithRealPrecedenceAndNoNPlusOne()
    {
        var menus = new FakeMenus();
        var prices = new FakePrices();
        Guid parent = Guid.NewGuid();
        Guid nested = Guid.NewGuid();
        Guid visibleItem = Guid.NewGuid();
        Guid hiddenItem = Guid.NewGuid();
        Guid inactiveItem = Guid.NewGuid();
        menus.Details = new(new(Menu, "MAIN", "Κυρίως", 3, DateTime.UtcNow),
            [new(parent, "Φαγητά", 1, true),
             new(nested, "Μη διαθέσιμα", 1, false, parent)],
            [new(Guid.NewGuid(), parent, visibleItem, "A", "Πίτσα", null, true, 1),
             new(Guid.NewGuid(), nested, hiddenItem, "B", "Σαλάτα", null, true, 1),
             new(Guid.NewGuid(), null, inactiveItem, "C", "Ανενεργό", null, false, 1)]);
        prices.Candidates.Add(new(visibleItem, Option(PriceListChannel.All, 10m)));
        prices.Candidates.Add(new(visibleItem, Option(PriceListChannel.DineIn, 12m)));
        var service = Create(TenantMembershipRole.Member, menus, prices);

        MenuPreviewResult result = await service.PreviewAsync(
            User, Tenant, Menu, Store, PriceListChannel.DineIn, Date);

        Assert.AreEqual(MenuPreviewStatus.Ready, result.Status);
        Assert.AreEqual(1, result.ResolvedCount);
        Assert.AreEqual(2, result.BlockedCount);
        Assert.AreEqual(1, prices.Calls);
        Assert.AreEqual(1, prices.RequestedCount);
        MenuPreviewItem good = result.Items!.Single(x => x.CatalogItemId == visibleItem);
        Assert.AreEqual(MenuPreviewItemStatus.Resolved, good.Status);
        Assert.AreEqual(12m, good.UnitPrice);
        Assert.AreEqual(PriceListChannel.DineIn, prices.LastChannel);
        Assert.AreEqual(Store, prices.LastStore);
        Assert.AreEqual(Tenant, prices.LastTenant);
        Assert.AreEqual(MenuPreviewItemStatus.HiddenCategory,
            result.Items!.Single(x => x.CatalogItemId == hiddenItem).Status);
        Assert.AreEqual(MenuPreviewItemStatus.InactiveCatalogItem,
            result.Items!.Single(x => x.CatalogItemId == inactiveItem).Status);
    }

    [TestMethod]
    public async Task MissingAndAmbiguousPricesAreBlockedAndNeverHaveUnitPrice()
    {
        var menus = new FakeMenus();
        var prices = new FakePrices();
        Guid missingItem = Guid.NewGuid();
        Guid ambiguousItem = Guid.NewGuid();
        menus.Details = new(new(Menu, "TEST", "Δοκιμή", 1, DateTime.UtcNow), [],
            [new(Guid.NewGuid(), null, missingItem, "A", "Χωρίς τιμή", null, true, 1),
             new(Guid.NewGuid(), null, ambiguousItem, "B", "Αμφίσημο", null, true, 2)]);
        prices.Candidates.Add(new(ambiguousItem, Option(PriceListChannel.DineIn, 10m)));
        prices.Candidates.Add(new(ambiguousItem, Option(PriceListChannel.DineIn, 20m)));

        MenuPreviewResult result = await Create(TenantMembershipRole.Owner, menus, prices)
            .PreviewAsync(User, Tenant, Menu, Store, PriceListChannel.DineIn, Date);

        Assert.AreEqual(MenuPreviewStatus.Ready, result.Status);
        Assert.AreEqual(2, result.BlockedCount);
        Assert.AreEqual(1, prices.Calls);
        Assert.AreEqual(2, prices.RequestedCount);
        Assert.AreEqual(MenuPreviewItemStatus.MissingPrice,
            result.Items!.Single(x => x.CatalogItemId == missingItem).Status);
        MenuPreviewItem ambiguous = result.Items!.Single(x =>
            x.CatalogItemId == ambiguousItem);
        Assert.AreEqual(MenuPreviewItemStatus.AmbiguousPrice, ambiguous.Status);
        Assert.IsNull(ambiguous.UnitPrice);
        Assert.AreEqual(2, ambiguous.ConflictingPriceListIds?.Count);
    }

    [TestMethod]
    public async Task DeniedInvalidMissingAndCorruptCategory_DoNotExposeOtherTenant()
    {
        var menus = new FakeMenus();
        var prices = new FakePrices();
        var owner = Create(TenantMembershipRole.Owner, menus, prices);
        var member = Create(TenantMembershipRole.Member, menus, prices);

        Assert.AreEqual(MenuPreviewStatus.Invalid,
            (await owner.PreviewAsync(User, Tenant, Menu, Store,
                PriceListChannel.All, Date)).Status);
        Assert.AreEqual(MenuPreviewStatus.Denied,
            (await member.PreviewAsync(User, Tenant, Menu, Guid.NewGuid(),
                PriceListChannel.DineIn, Date)).Status);
        Assert.AreEqual(0, prices.Calls);
        Assert.AreEqual(0, menus.ReadCalls);
        Assert.AreEqual(MenuPreviewStatus.Missing,
            (await owner.PreviewAsync(User, Tenant, Menu, Store,
                PriceListChannel.DineIn, Date)).Status);

        Guid nonexistentCategory = Guid.NewGuid();
        menus.Details = new(new(Menu, "TEST", "Δοκιμή", 1, DateTime.UtcNow), [],
            [new(Guid.NewGuid(), nonexistentCategory, Guid.NewGuid(),
                "A", "Ξένο επίπεδο", null, true, 1)]);
        MenuPreviewResult corrupt = await owner.PreviewAsync(
            User, Tenant, Menu, Store, PriceListChannel.DineIn, Date);
        Assert.AreEqual(MenuPreviewItemStatus.InvalidCategory,
            corrupt.Items!.Single().Status);
        Assert.AreEqual(0, prices.Calls);
    }

    private static PriceResolutionCandidate Option(PriceListChannel channel, decimal price) =>
        new(Guid.NewGuid(), "PL", null, channel, true, "EUR",
            Date.AddDays(-10), null, true, price);

    private static MenuPreviewService Create(TenantMembershipRole role,
        FakeMenus menus, FakePrices prices) =>
        new(new TenantAuthorizationService(new FakeAccess(role)), menus, prices);

    private sealed class FakeAccess(TenantMembershipRole role) : ITenantAccessStore
    {
        public Task<TenantMembershipRole?> GetMembershipRoleAsync(Guid userId,
            Guid tenantId, CancellationToken token = default) =>
            Task.FromResult<TenantMembershipRole?>(
                userId == User && tenantId == Tenant ? role : null);

        public Task<bool> StoreBelongsToTenantAsync(Guid tenantId, Guid storeId,
            CancellationToken token = default) =>
            Task.FromResult(tenantId == Tenant && storeId == Store);

        public Task<IReadOnlyList<AccessibleTenant>> ListUserTenantsAsync(
            Guid userId, CancellationToken token = default) =>
            Task.FromResult<IReadOnlyList<AccessibleTenant>>([]);
    }

    private sealed class FakeMenus : IMenuDraftStore
    {
        public MenuDraftDetails? Details;
        public int ReadCalls;

        public Task<MenuDraftDetails?> GetAsync(Guid tenantId, Guid menuId,
            CancellationToken cancellationToken = default)
        {
            ReadCalls++;
            return Task.FromResult(tenantId == Tenant && menuId == Menu ? Details : null);
        }

        public Task<MenuDraftMutationResult> ApplyAsync(Guid tenantId, Guid userId,
            MenuDraftCommand command, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Η προεπισκόπηση δεν πρέπει να γράφει.");

        public Task<IReadOnlyList<MenuDraftSummary>> ListAsync(Guid tenantId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MenuDraftSummary>>([]);

        public Task<Guid?> GetDefaultMenuIdAsync(Guid tenantId, Guid storeId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Guid?>(null);

        public Task<IReadOnlyList<MenuCatalogOption>> SearchCatalogAsync(Guid tenantId,
            string? search, CancellationToken cancellationToken = default,
            Guid? catalogCategoryId = null) =>
            Task.FromResult<IReadOnlyList<MenuCatalogOption>>([]);

        public Task<IReadOnlyList<MenuCatalogCategoryOption>> ListCatalogCategoriesAsync(
            Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MenuCatalogCategoryOption>>([]);
    }

    private sealed class FakePrices : IMenuPreviewPricingStore
    {
        public List<MenuPreviewPriceCandidate> Candidates = [];
        public int Calls;
        public int RequestedCount;
        public Guid LastTenant;
        public Guid LastStore;
        public PriceListChannel LastChannel;

        public Task<IReadOnlyList<MenuPreviewPriceCandidate>> FindCandidatesAsync(
            Guid tenantId, IReadOnlyCollection<Guid> catalogItemIds, Guid storeId,
            PriceListChannel channel, DateOnly businessDate,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            RequestedCount = catalogItemIds.Count;
            LastTenant = tenantId;
            LastStore = storeId;
            LastChannel = channel;
            return Task.FromResult<IReadOnlyList<MenuPreviewPriceCandidate>>(
                Candidates.Where(x => catalogItemIds.Contains(x.CatalogItemId)).ToArray());
        }
    }
}
