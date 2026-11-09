using Aenaon.Domain.Exceptions;
using Aenaon.Domain.Tenancy;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aenaon.Domain.Tests;

[TestClass]
public sealed class TenantMembershipTests
{
    [TestMethod]
    public void NewMembership_DefaultsToMemberRatherThanOwner()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var membership = TenantMembership.Create(tenantId, userId);

        Assert.AreEqual(tenantId, membership.TenantId);
        Assert.AreEqual(userId, membership.UserId);
        Assert.AreEqual(TenantMembershipRole.Member, membership.Role);
    }

    [TestMethod]
    public void SameAccount_HasIndependentRolesInDifferentTenants()
    {
        var userId = Guid.NewGuid();
        var owner = TenantMembership.Create(Guid.NewGuid(), userId, TenantMembershipRole.Owner);
        var member = TenantMembership.Create(Guid.NewGuid(), userId);

        Assert.AreEqual(owner.UserId, member.UserId);
        Assert.AreNotEqual(owner.TenantId, member.TenantId);
        Assert.AreEqual(TenantMembershipRole.Owner, owner.Role);
        Assert.AreEqual(TenantMembershipRole.Member, member.Role);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void EmptyIdentity_IsRejected(bool emptyTenant)
    {
        var error = Assert.ThrowsExactly<DomainRuleException>(() => TenantMembership.Create(
            emptyTenant ? Guid.Empty : Guid.NewGuid(), emptyTenant ? Guid.NewGuid() : Guid.Empty));

        Assert.AreEqual("membership.identity.required", error.Code);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(99)]
    public void UnknownRole_IsRejected(int value)
    {
        var error = Assert.ThrowsExactly<DomainRuleException>(() => TenantMembership.Create(
            Guid.NewGuid(), Guid.NewGuid(), (TenantMembershipRole)value));

        Assert.AreEqual("membership.role.invalid", error.Code);
    }
}
