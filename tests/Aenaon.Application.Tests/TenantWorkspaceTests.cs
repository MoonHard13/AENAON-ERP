using Aenaon.Application.Tenancy;
using Aenaon.Domain.Tenancy;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Application.Tests;

/// <summary>Ασφαλής ανάγνωση καταστημάτων μόνο κατόπιν επιτυχούς εξουσιοδότησης.</summary>
[TestClass]
public sealed class TenantWorkspaceTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();

    [TestMethod]
    public async Task AuthorizedOwner_ReadsOnlySelectedTenantStoreCatalog()
    {
        var permissions = new TenantAuthorizationFixture { Role = TenantMembershipRole.Owner };
        var catalog = new TrackingStoreCatalog();
        var workspace = new TenantWorkspaceService(new TenantAuthorizationService(permissions), catalog);

        var stores = await workspace.ListAuthorizedStoresAsync(UserId, TenantId);

        Assert.HasCount(1, stores);
        Assert.AreEqual(TenantId, stores[0].TenantId);
        Assert.AreEqual(1, catalog.Calls);
        Assert.AreEqual(TenantId, catalog.LastTenantId);
    }

    [TestMethod]
    public async Task NonMember_NeverReadsCatalog()
    {
        var permissions = new TenantAuthorizationFixture();
        var catalog = new TrackingStoreCatalog();
        var workspace = new TenantWorkspaceService(new TenantAuthorizationService(permissions), catalog);

        var stores = await workspace.ListAuthorizedStoresAsync(UserId, TenantId);

        Assert.HasCount(0, stores);
        Assert.AreEqual(0, catalog.Calls);
    }

    [TestMethod]
    public async Task EmptyUser_IsRejectedBeforeStorage()
    {
        var permissions = new TenantAuthorizationFixture { Role = TenantMembershipRole.Owner };
        var catalog = new TrackingStoreCatalog();
        var workspace = new TenantWorkspaceService(new TenantAuthorizationService(permissions), catalog);

        var stores = await workspace.ListAuthorizedStoresAsync(Guid.Empty, TenantId);

        Assert.HasCount(0, stores);
        Assert.AreEqual(0, permissions.MembershipReads);
        Assert.AreEqual(0, catalog.Calls);
    }
}

internal sealed class TrackingStoreCatalog : ITenantStoreCatalog
{
    public int Calls { get; private set; }
    public Guid LastTenantId { get; private set; }

    public Task<IReadOnlyList<TenantStoreSummary>> ListStoresAsync(Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        Calls++;
        LastTenantId = tenantId;
        IReadOnlyList<TenantStoreSummary> stores =
            [new TenantStoreSummary(Guid.NewGuid(), tenantId, "MAIN", "Κεντρικό")];
        return Task.FromResult(stores);
    }
}
