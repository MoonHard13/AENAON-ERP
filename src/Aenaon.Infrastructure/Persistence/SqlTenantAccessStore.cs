using Aenaon.Application.Tenancy;
using Aenaon.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Aenaon.Infrastructure.Persistence;

/// <summary>Υλοποιεί φιλτραρισμένες αναγνώσεις συμμετοχών και ιδιοκτησίας καταστημάτων μέσω EF Core.</summary>
public sealed class SqlTenantAccessStore(AenaonDbContext context) : ITenantAccessStore
{
    public async Task<TenantMembershipRole?> GetMembershipRoleAsync(Guid userId, Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        int? role = await context.TenantMemberships.AsNoTracking()
            .Where(item => item.UserId == userId && item.TenantId == tenantId)
            .Select(item => (int?)item.Role)
            .SingleOrDefaultAsync(cancellationToken);
        return role.HasValue ? (TenantMembershipRole)role.Value : null;
    }

    public Task<bool> StoreBelongsToTenantAsync(Guid tenantId, Guid storeId,
        CancellationToken cancellationToken = default)
    {
        return context.Stores.AsNoTracking()
            .AnyAsync(item => item.Id == storeId && item.TenantId == tenantId, cancellationToken);
    }

    public async Task<IReadOnlyList<AccessibleTenant>> ListUserTenantsAsync(Guid userId,
        CancellationToken cancellationToken = default)
    {
        var result = await context.TenantMemberships.AsNoTracking()
            .Where(membership => membership.UserId == userId)
            .Join(context.Tenants.AsNoTracking(),
                membership => membership.TenantId,
                tenant => tenant.Id,
                (membership, tenant) => new { tenant.Id, tenant.Name, membership.Role })
            .OrderBy(item => item.Name)
            .ThenBy(item => item.Id)
            .Select(item => new AccessibleTenant(item.Id, item.Name, item.Role))
            .ToListAsync(cancellationToken);
        return result;
    }
}
