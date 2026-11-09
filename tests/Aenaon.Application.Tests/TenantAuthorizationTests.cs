using Aenaon.Application.Tenancy;
using Aenaon.Domain.Tenancy;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Application.Tests;

[TestClass]
public sealed class TenantAuthorizationTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid StoreId = Guid.NewGuid();

    [TestMethod]
    [DataRow(TenantMembershipRole.Owner, TenantPermission.Read, TenantAccessStatus.Granted)]
    [DataRow(TenantMembershipRole.Owner, TenantPermission.Manage, TenantAccessStatus.Granted)]
    [DataRow(TenantMembershipRole.Member, TenantPermission.Read, TenantAccessStatus.Granted)]
    [DataRow(TenantMembershipRole.Member, TenantPermission.Manage, TenantAccessStatus.Denied)]
    public async Task Authorization_EnforcesRolePermissions(
        TenantMembershipRole role, TenantPermission permission, TenantAccessStatus expected)
    {
        var store = new TenantAuthorizationFixture { Role = role, StoreIsInTenant = true };
        var result = await new TenantAuthorizationService(store)
            .CheckAsync(UserId, TenantId, StoreId, permission);

        Assert.AreEqual(expected, result.Status);
        Assert.AreEqual(expected == TenantAccessStatus.Granted ? role : null, result.Role);
        Assert.AreEqual(1, store.MembershipReads);
        Assert.AreEqual(expected == TenantAccessStatus.Granted, store.StoreChecks > 0);
    }

    [TestMethod]
    public async Task StoreOfAnotherTenant_IsDeniedEvenForOwner()
    {
        var store = new TenantAuthorizationFixture
        {
            Role = TenantMembershipRole.Owner,
            StoreIsInTenant = false
        };

        var result = await new TenantAuthorizationService(store)
            .CheckAsync(UserId, TenantId, StoreId, TenantPermission.Manage);

        Assert.AreEqual(TenantAccessStatus.Denied, result.Status);
        Assert.IsNull(result.Role);
    }

    [TestMethod]
    public async Task NonMember_IsDeniedWithoutQueryingStore()
    {
        var store = new TenantAuthorizationFixture();
        var result = await new TenantAuthorizationService(store)
            .CheckAsync(UserId, TenantId, StoreId, TenantPermission.Read);

        Assert.AreEqual(TenantAccessStatus.Denied, result.Status);
        Assert.AreEqual(0, store.StoreChecks);
    }

    [TestMethod]
    [DataRow("empty-user")]
    [DataRow("empty-tenant")]
    [DataRow("empty-store")]
    [DataRow("unknown-permission")]
    public async Task InvalidIdentityOrPermission_DeniedBeforeStorage(string scenario)
    {
        var store = new TenantAuthorizationFixture { Role = TenantMembershipRole.Owner };
        var result = await new TenantAuthorizationService(store).CheckAsync(
            scenario == "empty-user" ? Guid.Empty : UserId,
            scenario == "empty-tenant" ? Guid.Empty : TenantId,
            scenario == "empty-store" ? Guid.Empty : StoreId,
            scenario == "unknown-permission" ? (TenantPermission)99 : TenantPermission.Read);

        Assert.AreEqual(TenantAccessStatus.Denied, result.Status);
        Assert.AreEqual(0, store.MembershipReads);
    }

    [TestMethod]
    public async Task ListAccessibleTenants_UsesOnlyVerifiedUserId()
    {
        var store = new TenantAuthorizationFixture
        {
            Tenants = [new AccessibleTenant(TenantId, "Demo", TenantMembershipRole.Owner)]
        };
        var service = new TenantAuthorizationService(store);

        Assert.HasCount(0, await service.GetAccessibleTenantsAsync(Guid.Empty));
        var listed = await service.GetAccessibleTenantsAsync(UserId);
        Assert.HasCount(1, listed);
        Assert.AreEqual(TenantId, listed[0].TenantId);
        Assert.AreEqual(1, store.ListReads);
    }

    [TestMethod]
    public async Task Cancellation_StopsBeforeDatabase()
    {
        var store = new TenantAuthorizationFixture();
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            new TenantAuthorizationService(store).CheckAsync(
                UserId, TenantId, null, TenantPermission.Read, cancel.Token));
        Assert.AreEqual(0, store.MembershipReads);
    }
}

internal sealed class TenantAuthorizationFixture : ITenantAccessStore
{
    public TenantMembershipRole? Role { get; init; }
    public bool StoreIsInTenant { get; init; }
    public IReadOnlyList<AccessibleTenant> Tenants { get; init; } = [];
    public int MembershipReads { get; private set; }
    public int StoreChecks { get; private set; }
    public int ListReads { get; private set; }

    public Task<TenantMembershipRole?> GetMembershipRoleAsync(Guid userId, Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        MembershipReads++;
        return Task.FromResult(Role);
    }

    public Task<bool> StoreBelongsToTenantAsync(Guid tenantId, Guid storeId,
        CancellationToken cancellationToken = default)
    {
        StoreChecks++;
        return Task.FromResult(StoreIsInTenant);
    }

    public Task<IReadOnlyList<AccessibleTenant>> ListUserTenantsAsync(Guid userId,
        CancellationToken cancellationToken = default)
    {
        ListReads++;
        return Task.FromResult(Tenants);
    }
}
