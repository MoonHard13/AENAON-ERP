using Aenaon.Application.Catalog;
using Aenaon.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Aenaon.Infrastructure.Persistence;

/// <summary>Read-only SQL Server προβολή ενεργών τιμών με απόλυτη απομόνωση Tenant.</summary>
public sealed class SqlPriceResolutionStore(AenaonDbContext context) : IPriceResolutionStore
{
    public Task<bool> IsActiveCatalogItemAsync(
        Guid tenantId, Guid itemId, CancellationToken token) =>
        context.CatalogItems.AsNoTracking().AnyAsync(x =>
            x.TenantId == tenantId && x.Id == itemId && x.IsActive, token);

    public async Task<IReadOnlyList<PriceResolutionCandidate>> FindCandidatesAsync(
        Guid tenantId, Guid itemId, Guid? storeId, PriceListChannel channel,
        DateOnly businessDate, CancellationToken token)
    {
        var query = context.PriceListItems.AsNoTracking()
            .Join(context.PriceLists.AsNoTracking(),
                price => new { price.TenantId, Id = price.PriceListId },
                list => new { list.TenantId, list.Id },
                (price, list) => new { price, list })
            .Where(x => x.price.TenantId == tenantId &&
                x.price.CatalogItemId == itemId &&
                x.list.IsActive &&
                x.list.ValidFrom <= businessDate &&
                (!x.list.ValidUntil.HasValue || x.list.ValidUntil.Value >= businessDate) &&
                (x.list.Channel == channel || x.list.Channel == PriceListChannel.All));

        if (storeId.HasValue)
        {
            query = query.Where(x => x.list.StoreId == null ||
                x.list.StoreId == storeId.Value);
        }
        else
        {
            query = query.Where(x => x.list.StoreId == null);
        }

        return await query
            .Select(x => new PriceResolutionCandidate(
                x.list.Id, x.list.Code, x.list.StoreId,
                x.list.Channel, x.list.PricesIncludeVat, x.list.Currency,
                x.list.ValidFrom, x.list.ValidUntil, x.list.IsActive,
                x.price.UnitPrice))
            .ToListAsync(token);
    }
}
