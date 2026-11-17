using Aenaon.Application.Menus;
using Aenaon.Application.Tenancy;
using Aenaon.Domain.Tenancy;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Application.Tests;

/// <summary>Ελέγχει τα πραγματικά δικαιώματα και τα όρια του Application χωρίς SQL.</summary>
[TestClass]
public sealed class MenuDraftManagementServiceTests
{
    private static readonly Guid User = Guid.Parse("c4dbe076-0125-438d-9bf1-8e8ddc18f46f");
    private static readonly Guid Tenant = Guid.Parse("1fe9efcb-2aa9-4538-9530-254431b841a0");

    [TestMethod]
    public async Task Owner_CanCreateDraftThroughSharedService()
    {
        var store = new FakeMenuStore();
        MenuDraftManagementService service = CreateService(TenantMembershipRole.Owner, store);
        MenuDraftMutationResult result = await service.ApplyAsync(User, Tenant,
            new(MenuDraftAction.CreateMenu, Guid.NewGuid(), Code: "MAIN", Name: "Κυρίως"));

        Assert.AreEqual(MenuDraftStatus.Saved, result.Status);
        Assert.AreEqual(1, store.ApplyCalls);
        Assert.AreEqual(Tenant, store.LastTenant);
    }

    [TestMethod]
    public async Task MemberAndForeignTenant_DeniedBeforeStorage()
    {
        var store = new FakeMenuStore();
        MenuDraftManagementService member = CreateService(TenantMembershipRole.Member, store);

        MenuDraftMutationResult write = await member.ApplyAsync(User, Tenant,
            new(MenuDraftAction.CreateMenu, Guid.NewGuid(), Code: "MAIN", Name: "Κυρίως"));
        MenuDraftMutationResult other = await member.ApplyAsync(User, Guid.NewGuid(),
            new(MenuDraftAction.CreateMenu, Guid.NewGuid(), Code: "OTHER", Name: "Άλλο"));

        Assert.AreEqual(MenuDraftStatus.Denied, write.Status);
        Assert.AreEqual(MenuDraftStatus.Denied, other.Status);
        Assert.AreEqual(0, store.ApplyCalls);
    }

    [TestMethod]
    public async Task InvalidCommand_DoesNotReachWriteStore()
    {
        var store = new FakeMenuStore();
        MenuDraftManagementService service = CreateService(TenantMembershipRole.Owner, store);
        MenuDraftCommand[] commands =
        [
            new(MenuDraftAction.CreateMenu, Guid.Empty, Code: "M", Name: "Μενού"),
            new(MenuDraftAction.AddItem, Guid.NewGuid(), MenuId: Guid.NewGuid(),
                ExpectedRevision: 1, CategoryId: Guid.NewGuid()),
            new(MenuDraftAction.MoveCategory, Guid.NewGuid(), MenuId: Guid.NewGuid(),
                ExpectedRevision: 1, CategoryId: Guid.NewGuid(), TargetPosition: 0),
            new(MenuDraftAction.UpdateMenu, Guid.NewGuid(), MenuId: Guid.NewGuid(),
                ExpectedRevision: null, Name: "Name"),
            new((MenuDraftAction)42, Guid.NewGuid()),
        ];

        foreach (MenuDraftCommand command in commands)
        {
            MenuDraftMutationResult result = await service.ApplyAsync(User, Tenant, command);
            Assert.AreEqual(MenuDraftStatus.Invalid, result.Status);
        }
        Assert.AreEqual(0, store.ApplyCalls);
    }

    [TestMethod]
    public async Task Owner_ValidatesParentAndAllowsRootItems()
    {
        var store = new FakeMenuStore();
        MenuDraftManagementService service = CreateService(TenantMembershipRole.Owner, store);
        Guid menuId = Guid.NewGuid();
        Guid parentId = Guid.NewGuid();
        MenuDraftMutationResult nested = await service.ApplyAsync(User, Tenant,
            new(MenuDraftAction.AddCategory, Guid.NewGuid(), MenuId: menuId,
                ExpectedRevision: 1, Name: "Υποκατηγορία", ParentCategoryId: parentId));
        Assert.AreEqual(MenuDraftStatus.Saved, nested.Status);
        Assert.AreEqual(parentId, store.LastCommand?.ParentCategoryId);

        MenuDraftMutationResult root = await service.ApplyAsync(User, Tenant,
            new(MenuDraftAction.AddItem, Guid.NewGuid(), MenuId: menuId,
                ExpectedRevision: 1, CatalogItemId: Guid.NewGuid()));
        Assert.AreEqual(MenuDraftStatus.Saved, root.Status);
        Assert.IsNull(store.LastCommand?.CategoryId);

        int calls = store.ApplyCalls;
        MenuDraftMutationResult invalid = await service.ApplyAsync(User, Tenant,
            new(MenuDraftAction.AddCategory, Guid.NewGuid(), MenuId: menuId,
                ExpectedRevision: 1, Name: "Άκυρη", ParentCategoryId: Guid.Empty));
        Assert.AreEqual(MenuDraftStatus.Invalid, invalid.Status);
        Assert.AreEqual(calls, store.ApplyCalls);

        MenuDraftMutationResult wrongAction = await service.ApplyAsync(User, Tenant,
            new(MenuDraftAction.MoveItem, Guid.NewGuid(), MenuId: menuId,
                ExpectedRevision: 1, ItemId: Guid.NewGuid(), TargetPosition: 2,
                ParentCategoryId: parentId));
        Assert.AreEqual(MenuDraftStatus.Invalid, wrongAction.Status);
    }

    [TestMethod]
    public async Task Member_CanReadButForeignTenantCannot()
    {
        var store = new FakeMenuStore();
        MenuDraftManagementService service = CreateService(TenantMembershipRole.Member, store);
        MenuDraftReadResult allowed = await service.ReadAsync(User, Tenant, null, "ITEM");
        MenuDraftReadResult denied = await service.ReadAsync(User, Guid.NewGuid(), null, "ITEM");
        Assert.IsTrue(allowed.IsAuthorized);
        Assert.IsFalse(denied.IsAuthorized);
        Assert.AreEqual(1, store.ListCalls);
        Assert.AreEqual(1, store.CatalogCalls);
    }

    [TestMethod]
    public async Task CatalogImport_RequiresOwnerMenuAndExpectedRevision()
    {
        var store = new FakeMenuStore();
        MenuDraftManagementService owner = CreateService(TenantMembershipRole.Owner, store);
        MenuDraftManagementService member = CreateService(TenantMembershipRole.Member, store);
        Guid menuId = Guid.NewGuid();

        MenuDraftMutationResult missingRevision = await owner.ApplyAsync(User, Tenant,
            new(MenuDraftAction.ImportCatalogContents, Guid.NewGuid(), MenuId: menuId));
        Assert.AreEqual(MenuDraftStatus.Invalid, missingRevision.Status);

        MenuDraftMutationResult invalidExtra = await owner.ApplyAsync(User, Tenant,
            new(MenuDraftAction.ImportCatalogContents, Guid.NewGuid(), MenuId: menuId,
                ExpectedRevision: 1, CategoryId: Guid.NewGuid()));
        Assert.AreEqual(MenuDraftStatus.Invalid, invalidExtra.Status);
        Assert.AreEqual(0, store.ApplyCalls);

        MenuDraftCommand import = new(MenuDraftAction.ImportCatalogContents,
            Guid.NewGuid(), MenuId: menuId, ExpectedRevision: 1);
        MenuDraftMutationResult denied = await member.ApplyAsync(User, Tenant, import);
        Assert.AreEqual(MenuDraftStatus.Denied, denied.Status);
        Assert.AreEqual(0, store.ApplyCalls);

        MenuDraftMutationResult accepted = await owner.ApplyAsync(User, Tenant, import);
        Assert.AreEqual(MenuDraftStatus.Saved, accepted.Status);
        Assert.AreEqual(Tenant, store.LastTenant);
        Assert.AreEqual(1, store.ApplyCalls);
    }

    [TestMethod]
    public async Task DefaultStoreMenu_RequiresOwnerAndValidStoreScope()
    {
        var store = new FakeMenuStore();
        MenuDraftManagementService owner = CreateService(TenantMembershipRole.Owner, store);
        MenuDraftManagementService member = CreateService(TenantMembershipRole.Member, store);

        MenuDraftMutationResult missing = await owner.ApplyAsync(User, Tenant,
            new(MenuDraftAction.EnsureStoreDefault, Guid.NewGuid()));
        Assert.AreEqual(MenuDraftStatus.Invalid, missing.Status);

        Guid storeId = Guid.NewGuid();
        MenuDraftCommand command = new(MenuDraftAction.EnsureStoreDefault,
            Guid.NewGuid(), StoreId: storeId);
        MenuDraftMutationResult forbidden = await member.ApplyAsync(User, Tenant, command);
        Assert.AreEqual(MenuDraftStatus.Denied, forbidden.Status);
        Assert.AreEqual(0, store.ApplyCalls);

        MenuDraftMutationResult accepted = await owner.ApplyAsync(User, Tenant, command);
        Assert.AreEqual(MenuDraftStatus.Saved, accepted.Status);
        Assert.AreEqual(Tenant, store.LastTenant);
        Assert.AreEqual(1, store.ApplyCalls);
    }

    [TestMethod]
    public async Task Cancellation_StopsBeforeDatabase()
    {
        var store = new FakeMenuStore();
        MenuDraftManagementService service = CreateService(TenantMembershipRole.Owner, store);
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            service.ApplyAsync(User, Tenant,
                new(MenuDraftAction.CreateMenu, Guid.NewGuid(), Code: "MAIN", Name: "Menu"),
                source.Token));
        Assert.AreEqual(0, store.ApplyCalls);
    }

    private static MenuDraftManagementService CreateService(TenantMembershipRole role,
        FakeMenuStore store) =>
        new(new TenantAuthorizationService(new FakeAccessStore(role)), store);

    private sealed class FakeMenuStore : IMenuDraftStore
    {
        public int ApplyCalls { get; private set; }
        public int ListCalls { get; private set; }
        public int CatalogCalls { get; private set; }
        public Guid LastTenant { get; private set; }

        public Task<MenuDraftMutationResult> ApplyAsync(Guid tenantId, Guid userId,
            MenuDraftCommand command, CancellationToken cancellationToken = default)
        {
            ApplyCalls++;
            LastTenant = tenantId;
            return Task.FromResult(new MenuDraftMutationResult(MenuDraftStatus.Saved));
        }

        public Task<IReadOnlyList<MenuDraftSummary>> ListAsync(Guid tenantId,
            CancellationToken cancellationToken = default)
        {
            ListCalls++;
            return Task.FromResult<IReadOnlyList<MenuDraftSummary>>([]);
        }

        public Task<Guid?> GetDefaultMenuIdAsync(Guid tenantId, Guid storeId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Guid?>(null);

        public Task<MenuDraftDetails?> GetAsync(Guid tenantId, Guid menuId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<MenuDraftDetails?>(null);

        public Task<IReadOnlyList<MenuCatalogOption>> SearchCatalogAsync(Guid tenantId,
            string? search, CancellationToken cancellationToken = default)
        {
            CatalogCalls++;
            return Task.FromResult<IReadOnlyList<MenuCatalogOption>>([]);
        }
    }

    private sealed class FakeAccessStore(TenantMembershipRole role) : ITenantAccessStore
    {
        public Task<TenantMembershipRole?> GetMembershipRoleAsync(Guid userId,
            Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<TenantMembershipRole?>(
                userId == User && tenantId == Tenant ? role : null);

        public Task<bool> StoreBelongsToTenantAsync(Guid tenantId, Guid storeId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(tenantId == Tenant);

        public Task<IReadOnlyList<AccessibleTenant>> ListUserTenantsAsync(Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AccessibleTenant>>([]);
    }
}
