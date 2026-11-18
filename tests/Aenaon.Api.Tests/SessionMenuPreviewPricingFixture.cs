using Aenaon.Application.Menus;
using Aenaon.Domain.Catalog;

namespace Aenaon.Api.Tests;

/// <summary>Μη πραγματική SQL πηγή για απομονωμένα SSR tests της προεπισκόπησης.</summary>
internal sealed class SessionMenuPreviewPricingFixture : IMenuPreviewPricingStore
{
    public List<MenuPreviewPriceCandidate> Candidates { get; } = [];
    public int ReadCalls { get; private set; }
    public Guid LastTenant { get; private set; }
    public Guid LastStore { get; private set; }

    public Task<IReadOnlyList<MenuPreviewPriceCandidate>> FindCandidatesAsync(
        Guid tenantId, IReadOnlyCollection<Guid> catalogItemIds, Guid storeId,
        PriceListChannel channel, DateOnly businessDate,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ReadCalls++;
        LastTenant = tenantId;
        LastStore = storeId;
        return Task.FromResult<IReadOnlyList<MenuPreviewPriceCandidate>>(
            Candidates.Where(x => catalogItemIds.Contains(x.CatalogItemId)).ToArray());
    }
}
