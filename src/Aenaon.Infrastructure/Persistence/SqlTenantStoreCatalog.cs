using Aenaon.Application.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Aenaon.Infrastructure.Persistence;

/// <summary>Επιστρέφει αποκλειστικά καταστήματα συγκεκριμένου TenantId με parameterized EF Core query.</summary>
public sealed class SqlTenantStoreCatalog(AenaonDbContext context) : ITenantStoreCatalog
{
    public async Task<IReadOnlyList<TenantStoreSummary>> ListStoresAsync(Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            return [];
        }

        var result = await context.Stores.AsNoTracking()
            .Where(store => store.TenantId == tenantId)
            .OrderBy(store => store.Code)
            .ThenBy(store => store.Id)
            .Select(store => new TenantStoreSummary(store.Id, store.TenantId, store.Code, store.Name))
            .ToListAsync(cancellationToken);
        return result;
    }
}
