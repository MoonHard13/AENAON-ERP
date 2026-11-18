using Aenaon.Application.Menus;
using Aenaon.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Aenaon.Infrastructure.Persistence;

/// <summary>
/// Ανακτά τιμές με bounded batch queries, μόνο για ρητά επιτρεπόμενο Tenant/Store.
/// Οι υποψήφιες τιμές αξιολογούνται από την κεντρική PriceResolutionPolicy.
/// </summary>
public sealed class SqlMenuPreviewPricingStore(AenaonDbContext context) : IMenuPreviewPricingStore
{
    public async Task<IReadOnlyList<MenuPreviewPriceCandidate>> FindCandidatesAsync(
        Guid tenantId, IReadOnlyCollection<Guid> catalogItemIds,
        Guid storeId, PriceListChannel channel, DateOnly businessDate,
        CancellationToken cancellationToken = default)
    {
        if (catalogItemIds.Count == 0)
        {
            return [];
        }

        Guid[] ids = catalogItemIds.Distinct().ToArray();
        var result = new List<MenuPreviewPriceCandidate>();
        // Το chunk περιορίζει SQL παραμέτρους και μνήμη ανεξάρτητα από το μέγεθος μενού.
        foreach (Guid[] chunk in ids.Chunk(400))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var query = context.PriceListItems.AsNoTracking()
                .Join(context.PriceLists.AsNoTracking(),
                    price => new { price.TenantId, Id = price.PriceListId },
                    list => new { list.TenantId, list.Id },
                    (price, list) => new { price, list })
                .Where(x => x.price.TenantId == tenantId &&
                    chunk.Contains(x.price.CatalogItemId) &&
                    x.list.IsActive &&
                    x.list.ValidFrom <= businessDate &&
                    (!x.list.ValidUntil.HasValue ||
                        x.list.ValidUntil.Value >= businessDate) &&
                    (x.list.Channel == channel ||
                        x.list.Channel == PriceListChannel.All) &&
                    (x.list.StoreId == null || x.list.StoreId == storeId));

            result.AddRange(await query.Select(x =>
                new MenuPreviewPriceCandidate(x.price.CatalogItemId,
                    new PriceResolutionCandidate(x.list.Id, x.list.Code,
                        x.list.StoreId, x.list.Channel, x.list.PricesIncludeVat,
                        x.list.Currency, x.list.ValidFrom, x.list.ValidUntil,
                        x.list.IsActive, x.price.UnitPrice))).ToListAsync(cancellationToken));
        }
        return result;
    }
}
