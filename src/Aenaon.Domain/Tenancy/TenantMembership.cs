using Aenaon.Domain.Exceptions;

namespace Aenaon.Domain.Tenancy;

public enum TenantMembershipRole
{
    Owner = 1,
    Member = 2
}

/// <summary>Συνδέει έναν λογαριασμό με συγκεκριμένη επιχείρηση και ρόλο μέσα σε αυτήν.</summary>
public sealed class TenantMembership
{
    private TenantMembership()
    {
    }

    private TenantMembership(Guid tenantId, Guid userId, TenantMembershipRole role)
    {
        if (tenantId == Guid.Empty || userId == Guid.Empty)
        {
            throw new DomainRuleException("membership.identity.required", "Απαιτούνται αναγνωριστικά επιχείρησης και χρήστη.");
        }
        if (!Enum.IsDefined(role))
        {
            throw new DomainRuleException("membership.role.invalid", "Ο ρόλος συμμετοχής δεν είναι έγκυρος.");
        }
        TenantId = tenantId;
        UserId = userId;
        Role = role;
    }

    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public TenantMembershipRole Role { get; private set; }

    public static TenantMembership Create(Guid tenantId, Guid userId, TenantMembershipRole role = TenantMembershipRole.Member)
    {
        return new TenantMembership(tenantId, userId, role);
    }
}
