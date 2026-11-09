using Aenaon.Domain.Tenancy;

namespace Aenaon.Application.Tenancy;

/// <summary>Περιορισμένες αναγνώσεις δεδομένων ιδιότητας επιχείρησης χωρίς έκθεση γενικού IQueryable.</summary>
public interface ITenantAccessStore
{
    Task<TenantMembershipRole?> GetMembershipRoleAsync(Guid userId, Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<bool> StoreBelongsToTenantAsync(Guid tenantId, Guid storeId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AccessibleTenant>> ListUserTenantsAsync(Guid userId,
        CancellationToken cancellationToken = default);
}

public sealed record AccessibleTenant(Guid TenantId, string Name, TenantMembershipRole Role);

public enum TenantPermission
{
    Read = 1,
    Manage = 2
}

public enum TenantAccessStatus
{
    Denied,
    Granted
}

public sealed record TenantAccessResult(TenantAccessStatus Status, TenantMembershipRole? Role = null);
