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
        var result = await (from membership in context.TenantMemberships.AsNoTracking()
                join tenant in context.Tenants.AsNoTracking()
                    on membership.TenantId equals tenant.Id
                where membership.UserId == userId
                orderby tenant.Name, tenant.Id
                select new AccessibleTenant(tenant.Id, tenant.Name, membership.Role))
            .ToListAsync(cancellationToken);
        return result;
    }
}
